using CodeBoost.Logging;
using KinematicCharacterController;
using Nucleus.Components;
using Nucleus.Integrations.Unity.Prediction;
using Nucleus.Integrations.Unity.Serializers;
using Nucleus.Integrations.Unity.Systems;
using Nucleus.Integrations.Unity;
using Nucleus.Managers.NetworkLoop;
using Nucleus.Systems;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController
{
    /// <summary>
    /// Keeps a <see cref="PhysicsMover"/> in step with a <see cref="ValidatedPhysicsMoverComponent"/>, and gives its
    /// <see cref="IMoverController"/> a clock that every peer agrees on: <see cref="SimulationTime"/>, the seconds the mover has been
    /// simulated for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A character standing on a mover is only predicted right when the mover is predicted with it, so this turns on observer prediction
    /// for the mover's system: a client simulates the mover on its own ticks, ahead of the server as its own character is, and a reconcile
    /// restores and replays the mover with the character. The server simulates it as the uncontrolled object it is.
    /// </para>
    /// <para>
    /// A mover's controller therefore has to compute its goal from <see cref="SimulationTime"/> or from the mover's own state, never from
    /// <c>Time.time</c>, which differs between peers and is not rewound by a replay. Declares a system of its own, so it is added to the
    /// mover and nothing else.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(PhysicsMover))]
    [NucleusHelpUrl(typeof(ValidatedPhysicsMover))]
    public class ValidatedPhysicsMover : ValidatedBehaviour<ValidatedPhysicsMoverComponent>
    {
        /// <summary>
        /// The mover kept in step.
        /// </summary>
        public PhysicsMover PhysicsMover { get; private set; }
        /// <summary>
        /// The seconds the mover has been simulated for, counting the tick being simulated, or zero before its system links.
        /// </summary>
        /// <remarks>Read it from <see cref="IMoverController.UpdateMovement"/>: it advances on every tick this peer simulates the mover, replayed ticks included.</remarks>
        public float SimulationTime => Component is null ? 0f : Component.SimulatedTickCount.Value / (float)NetworkSystem.CoreManager.NetworkLoopManager.TickRate;
        /// <summary>
        /// The scene's simulation, which steps the mover and resolves it for the motors standing on it.
        /// </summary>
        private NetworkKinematicCharacterSystem _networkKinematicCharacterSystem;

        /// <inheritdoc/>
        /// <remarks>Declares the mover's state and the client state that makes the system predicted.</remarks>
        protected override bool TryDeclareSystem(NetworkSystemAcquiredHandler networkSystemAcquiredHandler, NetworkSystemReleasedHandler networkSystemReleasedHandler)
        {
            NetworkSystemObjectPool.RequireSystem<NetworkSystem, ValidatedPhysicsMoverComponent, ValidatedClientStateComponent>(this, networkSystemAcquiredHandler, networkSystemReleasedHandler);

            return true;
        }

        /// <inheritdoc/>
        protected override void Awake()
        {
            PhysicsMover = GetComponent<PhysicsMover>();
            _networkKinematicCharacterSystem = FindAnyObjectByType<NetworkKinematicCharacterSystem>();

            if (_networkKinematicCharacterSystem == null)
                Logger<ValidatedPhysicsMover>.LogError($"[{name}] found no [{nameof(NetworkKinematicCharacterSystem)}] in the scene, so its mover is never stepped.");

            base.Awake();
        }

        /// <inheritdoc/>
        /// <remarks>Turns on observer prediction and registers the mover, so a character can stand on it, before seeding or applying as the base does.</remarks>
        protected override void OnBound()
        {
            NetworkSystem.ObserverPredictionEnabled = true;

            if (_networkKinematicCharacterSystem != null)
                _networkKinematicCharacterSystem.RegisterMover(this);

            base.OnBound();
        }

        /// <inheritdoc/>
        protected override void OnUnbound()
        {
            if (_networkKinematicCharacterSystem != null)
                _networkKinematicCharacterSystem.UnregisterMover(this);

            base.OnUnbound();
        }

        /// <inheritdoc/>
        protected override void ApplySendInterval(SendInterval sendInterval)
        {
            ValidatedPhysicsMoverComponent validatedPhysicsMoverComponent = Component;
            validatedPhysicsMoverComponent.Position.SetSendInterval(sendInterval);
            validatedPhysicsMoverComponent.Rotation.SetSendInterval(sendInterval);
            validatedPhysicsMoverComponent.Velocity.SetSendInterval(sendInterval);
            validatedPhysicsMoverComponent.AngularVelocity.SetSendInterval(sendInterval);
            validatedPhysicsMoverComponent.SimulatedTickCount.SetSendInterval(sendInterval);
        }

        /// <inheritdoc/>
        /// <remarks>Advances the clock to the tick being simulated, then enrolls the mover, which the scene's simulation steps once every system's early fixed update has run.</remarks>
        protected override void OnSimulatedEarlyFixedUpdate(StepDelta stepDelta)
        {
            Component.SimulatedTickCount.Value++;

            if (_networkKinematicCharacterSystem != null)
                _networkKinematicCharacterSystem.Enroll(PhysicsMover);
        }

        /// <inheritdoc/>
        protected override void Capture()
        {
            ValidatedPhysicsMoverComponent validatedPhysicsMoverComponent = Component;
            PhysicsMoverState physicsMoverState = PhysicsMover.GetState();

            validatedPhysicsMoverComponent.Position.Value = physicsMoverState.Position.ToNative();
            validatedPhysicsMoverComponent.Rotation.Value = physicsMoverState.Rotation.ToNative();
            validatedPhysicsMoverComponent.Velocity.Value = physicsMoverState.Velocity.ToNative();
            validatedPhysicsMoverComponent.AngularVelocity.Value = physicsMoverState.AngularVelocity.ToNative();
        }

        /// <inheritdoc/>
        protected override void Apply()
        {
            ValidatedPhysicsMoverComponent validatedPhysicsMoverComponent = Component;
            PhysicsMoverState physicsMoverState = new()
            {
                Position = validatedPhysicsMoverComponent.Position.Value.ToUnity(),
                Rotation = validatedPhysicsMoverComponent.Rotation.Value.ToUnity(),
                Velocity = validatedPhysicsMoverComponent.Velocity.Value.ToUnity(),
                AngularVelocity = validatedPhysicsMoverComponent.AngularVelocity.Value.ToUnity()
            };

            PhysicsMover.ApplyState(physicsMoverState);
        }
    }
}
