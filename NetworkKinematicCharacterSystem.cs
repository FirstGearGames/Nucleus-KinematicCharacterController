using System.Collections.Generic;
using CodeBoost.Logging;
using KinematicCharacterController;
using Nucleus.Integrations.Unity;
using Nucleus.Managers.Core;
using Nucleus.Managers.NetworkLoop;
using Nucleus.Systems;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController
{
    /// <summary>
    /// Steps Kinematic Character Controller on the Nucleus tick instead of on Unity's fixed update: once per tick, live or replayed, for
    /// exactly the motors and movers this peer simulates on that tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// KCC steps every motor and mover together in phases: every mover's velocity, then every motor's first phase, then every mover's
    /// displacement, then every motor's second phase. That order is what carries a character standing on a platform by the platform's move
    /// of the same tick. Nucleus runs each system's simulation on its own, so the order is kept here instead: each
    /// <see cref="ValidatedKinematicCharacterMotor"/> and <see cref="ValidatedPhysicsMover"/> enrolls on its system's early fixed update, and
    /// one <see cref="KinematicCharacterSystem.Simulate"/> runs over the enrolled set after every early fixed update of the tick and before
    /// any late one. On a live tick that is the <see cref="NetworkLoopSteps.LateFixedUpdate"/> loop callback, which runs before the
    /// systems' late fixed updates; on a replayed tick it is <see cref="Managers.Systems.SystemManager.ReplayEarlyFixedUpdated"/>.
    /// </para>
    /// <para>
    /// Only the systems the engine simulates on this peer enroll, so the server steps every character, a client steps the one it controls
    /// and the movers it predicts, and a replay steps exactly what it replays. KCC's own simulation and interpolation are turned off when
    /// this wakes; draw characters and movers through a <see cref="Unity.Components.GraphicalSmoother"/> instead.
    /// </para>
    /// <para>
    /// One per scene that holds networked characters. The scene keeps it, so nothing outlives the scene that owns it.
    /// </para>
    /// </remarks>
    [NucleusHelpUrl(typeof(NetworkKinematicCharacterSystem))]
    public class NetworkKinematicCharacterSystem : MonoBehaviour, INetworkLoopStepCallback
    {
        /// <summary>
        /// The tick the latest simulation stepped, live or replayed, or <see cref="NetworkLoopManager.UnsetTick"/> before the first.
        /// </summary>
        public uint LastSimulatedTick { get; private set; } = NetworkLoopManager.UnsetTick;
        /// <summary>
        /// How many motors the latest simulation stepped.
        /// </summary>
        public int LastSimulatedMotorCount { get; private set; }
        /// <summary>
        /// How many movers the latest simulation stepped.
        /// </summary>
        public int LastSimulatedMoverCount { get; private set; }
        /// <summary>
        /// How many replayed ticks this has stepped since it woke.
        /// </summary>
        public uint ReplayedTickCount { get; private set; }
        /// <summary>
        /// The CoreManager whose loop this steps on, or null when the scene has no bound CoreManager.
        /// </summary>
        private CoreManager _coreManager;
        /// <summary>
        /// The motors whose systems ran their early fixed update this tick, stepped and cleared by <see cref="Simulate"/>.
        /// </summary>
        private readonly List<KinematicCharacterMotor> _enrolledMotors = new();
        /// <summary>
        /// The movers whose systems ran their early fixed update this tick, stepped and cleared by <see cref="Simulate"/>.
        /// </summary>
        private readonly List<PhysicsMover> _enrolledMovers = new();
        /// <summary>
        /// Every networked mover in the scene, keyed by its rigidbody, so a motor's attached rigidbody can travel as its system Id.
        /// </summary>
        private readonly Dictionary<Rigidbody, ValidatedPhysicsMover> _validatedPhysicsMoversByRigidbody = new();
        /// <summary>
        /// Every networked mover in the scene, searched by system Id when an Id read from the wire is resolved back to a rigidbody.
        /// </summary>
        /// <remarks>
        /// Searched rather than keyed by Id: a client binds a scene object before the server's spawn gives its system an Id, so an Id
        /// recorded when a mover binds can still be unset. A scene holds a handful of movers, and only a motor that stands on one looks.
        /// </remarks>
        private readonly List<ValidatedPhysicsMover> _validatedPhysicsMovers = new();

        /// <summary>
        /// Adds a motor to the tick being simulated. Called from the early fixed update of a system this peer simulates.
        /// </summary>
        /// <param name="kinematicCharacterMotor">The motor to step this tick.</param>
        public void Enroll(KinematicCharacterMotor kinematicCharacterMotor) => _enrolledMotors.Add(kinematicCharacterMotor);

        /// <summary>
        /// Adds a mover to the tick being simulated. Called from the early fixed update of a system this peer simulates.
        /// </summary>
        /// <param name="physicsMover">The mover to step this tick.</param>
        public void Enroll(PhysicsMover physicsMover) => _enrolledMovers.Add(physicsMover);

        /// <summary>
        /// Records a networked mover so a motor standing on it can name it by system Id.
        /// </summary>
        /// <param name="validatedPhysicsMover">The mover.</param>
        public void RegisterMover(ValidatedPhysicsMover validatedPhysicsMover)
        {
            _validatedPhysicsMoversByRigidbody[validatedPhysicsMover.PhysicsMover.Rigidbody] = validatedPhysicsMover;
            _validatedPhysicsMovers.Add(validatedPhysicsMover);
        }

        /// <summary>
        /// Forgets a networked mover recorded by <see cref="RegisterMover"/>.
        /// </summary>
        /// <param name="validatedPhysicsMover">The mover.</param>
        public void UnregisterMover(ValidatedPhysicsMover validatedPhysicsMover)
        {
            _validatedPhysicsMoversByRigidbody.Remove(validatedPhysicsMover.PhysicsMover.Rigidbody);
            _validatedPhysicsMovers.Remove(validatedPhysicsMover);
        }

        /// <summary>
        /// Finds the system Id of the networked mover owning a rigidbody.
        /// </summary>
        /// <param name="rigidbody">The rigidbody a motor is attached to, or null.</param>
        /// <param name="systemId">The mover's system Id when found, otherwise <see cref="NetworkSystem.UnsetId"/>.</param>
        /// <returns>True when <paramref name="rigidbody"/> belongs to a networked mover whose system has an Id.</returns>
        public bool TryGetMoverSystemId(Rigidbody rigidbody, out uint systemId)
        {
            if (rigidbody != null && _validatedPhysicsMoversByRigidbody.TryGetValue(rigidbody, out ValidatedPhysicsMover validatedPhysicsMover) && validatedPhysicsMover.NetworkSystem is { Id: not NetworkSystem.UnsetId } networkSystem)
            {
                systemId = networkSystem.Id;

                return true;
            }

            systemId = NetworkSystem.UnsetId;

            return false;
        }

        /// <summary>
        /// Finds the rigidbody of the networked mover with a system Id.
        /// </summary>
        /// <param name="systemId">The mover's system Id, or <see cref="NetworkSystem.UnsetId"/>.</param>
        /// <param name="rigidbody">The mover's rigidbody when found, otherwise null.</param>
        /// <returns>True when a networked mover has that system Id.</returns>
        public bool TryGetMoverRigidbody(uint systemId, out Rigidbody rigidbody)
        {
            if (systemId is not NetworkSystem.UnsetId)
            {
                foreach (ValidatedPhysicsMover validatedPhysicsMover in _validatedPhysicsMovers)
                {
                    if (validatedPhysicsMover.NetworkSystem is not { } networkSystem || networkSystem.Id != systemId)
                        continue;

                    rigidbody = validatedPhysicsMover.PhysicsMover.Rigidbody;

                    return true;
                }
            }

            rigidbody = null;

            return false;
        }

        /// <inheritdoc/>
        NetworkLoopSteps INetworkLoopStepCallback.GetNetworkLoopSteps() => NetworkLoopSteps.LateFixedUpdate;

        /// <inheritdoc/>
        /// <remarks>Steps the live tick: the loop's late fixed update callbacks run after every system's early fixed update and before any late one.</remarks>
        void INetworkLoopStepCallback.OnNetworkLoopStep(NetworkLoopSteps networkLoopStep, StepDelta stepDelta) => Simulate(stepDelta);

        /// <summary>
        /// Turns KCC's own simulation and interpolation off and joins the loop and its replays.
        /// </summary>
        private void Awake()
        {
            _coreManager = NucleusUnity.BoundCoreManager;

            if (_coreManager is null)
            {
                Logger<NetworkKinematicCharacterSystem>.LogError($"[{nameof(NetworkKinematicCharacterSystem)}] on [{name}] found no bound CoreManager; the scene needs a UnityCoreManager.");
                enabled = false;

                return;
            }

            // Motors and movers create the system as they enable, possibly before this; either way nothing has stepped yet, since Unity's fixed update follows every Awake.
            KinematicCharacterSystem.EnsureCreation();
            KinematicCharacterSystem.Settings.AutoSimulation = false;
            KinematicCharacterSystem.Settings.Interpolate = false;

            _coreManager.NetworkLoopManager.RegisterNetworkLoopStepCallbacks(this);
            _coreManager.SystemManager.ReplayEarlyFixedUpdated += SystemManager_OnReplayEarlyFixedUpdated;
        }

        /// <summary>
        /// Leaves the loop and its replays.
        /// </summary>
        private void OnDestroy()
        {
            if (_coreManager is null)
                return;

            _coreManager.NetworkLoopManager.UnregisterNetworkLoopStepCallbacks(this);
            _coreManager.SystemManager.ReplayEarlyFixedUpdated -= SystemManager_OnReplayEarlyFixedUpdated;
        }

        /// <summary>
        /// Steps a replayed tick, between the replayed systems' early and late fixed updates.
        /// </summary>
        /// <param name="stepDelta">The replayed tick's timing.</param>
        private void SystemManager_OnReplayEarlyFixedUpdated(StepDelta stepDelta)
        {
            if (Simulate(stepDelta))
                ReplayedTickCount++;
        }

        /// <summary>
        /// Steps every enrolled motor and mover by one tick in KCC's own phase order, then clears the enrollment.
        /// </summary>
        /// <param name="stepDelta">The tick's timing.</param>
        /// <returns>True when anything was enrolled and stepped.</returns>
        /// <remarks>
        /// Transforms are synced into physics first: a system's early fixed update may have moved one, such as a teleport, and the physics
        /// step that would sync it runs from a callback in no defined order with this one. Syncing keeps a live tick and its replay
        /// querying the same world.
        /// </remarks>
        private bool Simulate(StepDelta stepDelta)
        {
            if (_enrolledMotors.Count == 0 && _enrolledMovers.Count == 0)
                return false;

            UnityEngine.Physics.SyncTransforms();
            KinematicCharacterSystem.Simulate(1f / _coreManager.NetworkLoopManager.TickRate, _enrolledMotors, _enrolledMovers);

            LastSimulatedTick = stepDelta.Tick;
            LastSimulatedMotorCount = _enrolledMotors.Count;
            LastSimulatedMoverCount = _enrolledMovers.Count;

            _enrolledMotors.Clear();
            _enrolledMovers.Clear();

            return true;
        }
    }
}
