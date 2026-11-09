using KinematicCharacterController;
using KinematicCharacterController.Examples;
using Nucleus.Connections;
using Nucleus.Integrations.Unity.Serializers;
using Nucleus.Integrations.Unity.Systems;
using Nucleus.Managers.NetworkLoop;
using Nucleus.Systems;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// Drives a <see cref="NetworkExampleCharacterController"/> from client state: the player that controls the character writes its input
    /// on each live tick, and every peer that simulates the character, the server included, hands that input to the controller on the
    /// same tick. The controller's own state is validated beside the motor's, so a reconcile replays the character exactly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The character also carries a <see cref="ValidatedKinematicCharacterMotor"/>, which declares the motor's system and steps the motor;
    /// this script declares only the client state and the controller state. Everything that changes how the character moves happens
    /// here, on the early fixed update of a simulated tick, before the motor steps: the playground's teleporters and the planet's gravity
    /// are worked out from the motor's pose and recorded as validated state, where KCC's example did them in trigger callbacks and from the
    /// planet's mover, which a replay could neither repeat nor rewind.
    /// </para>
    /// <para>
    /// The local player is the scene's <see cref="NetworkExamplePlayer"/>, which reads the devices and the camera. It is handed this
    /// character when this peer's client controls it, which covers a host playing its own character too.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(NetworkExampleCharacterController))]
    [RequireComponent(typeof(ValidatedKinematicCharacterMotor))]
    public class NetworkExampleCharacter : NucleusBehaviour<NetworkExampleCharacterComponent>
    {
        /// <summary>
        /// The controller driven.
        /// </summary>
        public NetworkExampleCharacterController Character { get; private set; }
        /// <summary>
        /// True while this peer's player controls the character and the scene's player has been handed it.
        /// </summary>
        public bool IsLocalPlayer { get; private set; }
        /// <summary>
        /// The gravity the character was authored with, used whenever it is not on the planet.
        /// </summary>
        private Vector3 _defaultGravity;
        /// <summary>
        /// The scene's local player, or null in a scene without one, such as a dedicated server's.
        /// </summary>
        private NetworkExamplePlayer _networkExamplePlayer;
        /// <summary>
        /// Every teleporter in the scene.
        /// </summary>
        private NetworkTeleporter[] _networkTeleporters;
        /// <summary>
        /// The scene's planet, or null in a scene without one.
        /// </summary>
        private NetworkPlanet _networkPlanet;
        /// <summary>
        /// The smallest squared length a camera rotation may have and still be taken as one.
        /// </summary>
        private const float MinimumRotationSquaredLength = 0.5f;

        /// <inheritdoc/>
        protected override void Awake()
        {
            Character = GetComponent<NetworkExampleCharacterController>();
            _defaultGravity = Character.Gravity;
            _networkExamplePlayer = FindAnyObjectByType<NetworkExamplePlayer>();
            _networkTeleporters = FindObjectsByType<NetworkTeleporter>(FindObjectsInactive.Exclude);
            _networkPlanet = FindAnyObjectByType<NetworkPlanet>();

            base.Awake();
        }

        /// <inheritdoc/>
        /// <remarks>Linking raises no controller change, so whether this peer's player controls the character is read here too.</remarks>
        protected override void OnSystemLinked() => RefreshLocalPlayer();

        /// <inheritdoc/>
        protected override void OnControllerChanged(Connection previousControllerConnection, Connection currentControllerConnection) => RefreshLocalPlayer();

        /// <inheritdoc/>
        protected override void OnSystemUnlinked() => ReleaseLocalPlayer();

        /// <inheritdoc/>
        /// <remarks>
        /// On a simulated tick, live or replayed: the controlling player's live tick writes the client state, a tick whose client state
        /// never reached the server carries the last one on without its jump, and then teleporters, gravity and the input are handed to the
        /// controller ahead of the motor's step.
        /// </remarks>
        protected override void OnEarlyFixedUpdate(StepDelta stepDelta, SimulationContext simulationContext)
        {
            if (simulationContext.Type is SimulationType.None)
                return;

            NetworkExampleCharacterComponent networkExampleCharacterComponent = Component;

            if (simulationContext.Type is SimulationType.Forward && IsLocalPlayer)
                _networkExamplePlayer.WriteClientState(networkExampleCharacterComponent);

            if (stepDelta.IsClientStateMissing)
                networkExampleCharacterComponent.TryGetLastState(isEveryMemberCarried: false);

            Teleport(networkExampleCharacterComponent);
            Character.Gravity = ResolveGravity(networkExampleCharacterComponent);

            PlayerCharacterInputs playerCharacterInputs = new()
            {
                MoveAxisForward = networkExampleCharacterComponent.MoveAxisForward.Value,
                MoveAxisRight = networkExampleCharacterComponent.MoveAxisRight.Value,
                CameraRotation = ResolveCameraRotation(networkExampleCharacterComponent),
                JumpDown = networkExampleCharacterComponent.IsJumpPressed.Value,
                CrouchDown = networkExampleCharacterComponent.IsCrouchHeld.Value,
                CrouchUp = !networkExampleCharacterComponent.IsCrouchHeld.Value
            };

            Character.SetInputs(in playerCharacterInputs);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The motor has stepped by now. A simulating peer records the controller's state, and every peer then puts the recorded values
        /// back, so all of them carry on from the numbers the wire carries and an observer shows the server's crouch.
        /// </remarks>
        protected override void OnLateFixedUpdate(StepDelta stepDelta, SimulationContext simulationContext)
        {
            if (simulationContext.Type is not SimulationType.None)
                CaptureCharacterState();

            Character.ApplyState(ReadCharacterState());
        }

        /// <inheritdoc/>
        /// <remarks>A replayed character starts from the restored tick's controller state; its motor is restored by its own behaviour.</remarks>
        protected override void OnReplayStarting(ReplayReport replayReport)
        {
            if (NetworkSystem.IsPredicting)
                Character.ApplyState(ReadCharacterState());
        }

        /// <summary>
        /// Hands the character to the scene's player when this peer's client controls it, and takes it back when it no longer does.
        /// </summary>
        private void RefreshLocalPlayer()
        {
            bool isLocalPlayer = _networkExamplePlayer != null && IsController(ControllerType.Client);

            if (isLocalPlayer == IsLocalPlayer)
                return;

            if (isLocalPlayer)
            {
                IsLocalPlayer = true;
                _networkExamplePlayer.SetCharacter(this);
            }
            else
            {
                ReleaseLocalPlayer();
            }
        }

        /// <summary>
        /// Takes the character back from the scene's player, if it was handed one.
        /// </summary>
        private void ReleaseLocalPlayer()
        {
            if (!IsLocalPlayer)
                return;

            IsLocalPlayer = false;
            _networkExamplePlayer.ClearCharacter(this);
        }

        /// <summary>
        /// Sends the character through the teleporter it has walked into, recording the zone it stands in so it is not sent straight back.
        /// </summary>
        /// <param name="networkExampleCharacterComponent">The character's component.</param>
        /// <remarks>
        /// Tested against the motor's own pose with <see cref="UnityEngine.Physics.ComputePenetration(Collider, Vector3, Quaternion, Collider, Vector3, Quaternion, out Vector3, out float)"/>,
        /// which needs no transform sync, so a replayed tick finds exactly what its live tick found.
        /// </remarks>
        private void Teleport(NetworkExampleCharacterComponent networkExampleCharacterComponent)
        {
            KinematicCharacterMotor kinematicCharacterMotor = Character.Motor;
            NetworkTeleporter enteredNetworkTeleporter = FindOverlappedTeleporter(kinematicCharacterMotor);
            byte teleporterZoneId = enteredNetworkTeleporter == null ? NetworkTeleporter.UnsetZoneId : enteredNetworkTeleporter.ZoneId;

            if (enteredNetworkTeleporter != null && networkExampleCharacterComponent.TeleporterZoneId.Value != teleporterZoneId)
            {
                NetworkTeleporter destinationNetworkTeleporter = enteredNetworkTeleporter.Destination;
                destinationNetworkTeleporter.transform.GetPositionAndRotation(out Vector3 destinationPosition, out Quaternion destinationRotation);

                kinematicCharacterMotor.SetPositionAndRotation(destinationPosition, destinationRotation);
                networkExampleCharacterComponent.IsOnPlanet.Value = destinationNetworkTeleporter.IsOnPlanet;
                teleporterZoneId = destinationNetworkTeleporter.ZoneId;
            }

            networkExampleCharacterComponent.TeleporterZoneId.Value = teleporterZoneId;
        }

        /// <summary>
        /// Finds the teleporter the character's capsule overlaps.
        /// </summary>
        /// <param name="kinematicCharacterMotor">The character's motor, whose pose is tested.</param>
        /// <returns>The overlapped teleporter, or null when the capsule overlaps none.</returns>
        private NetworkTeleporter FindOverlappedTeleporter(KinematicCharacterMotor kinematicCharacterMotor)
        {
            foreach (NetworkTeleporter networkTeleporter in _networkTeleporters)
            {
                Collider teleporterCollider = networkTeleporter.Collider;

                // A teleporter that is only ever arrived at has no zone of its own.
                if (teleporterCollider == null)
                    continue;

                teleporterCollider.transform.GetPositionAndRotation(out Vector3 teleporterPosition, out Quaternion teleporterRotation);

                if (UnityEngine.Physics.ComputePenetration(kinematicCharacterMotor.Capsule, kinematicCharacterMotor.TransientPosition, kinematicCharacterMotor.TransientRotation, teleporterCollider, teleporterPosition, teleporterRotation, out _, out _))
                    return networkTeleporter;
            }

            return null;
        }

        /// <summary>
        /// Returns the gravity the character falls by this tick: towards the planet's centre while on the planet, otherwise its own.
        /// </summary>
        /// <param name="networkExampleCharacterComponent">The character's component.</param>
        /// <returns>The gravity to fall by.</returns>
        private Vector3 ResolveGravity(NetworkExampleCharacterComponent networkExampleCharacterComponent)
        {
            if (!networkExampleCharacterComponent.IsOnPlanet.Value || _networkPlanet == null)
                return _defaultGravity;

            return (_networkPlanet.Center - Character.Motor.TransientPosition).normalized * _networkPlanet.GravityStrength;
        }

        /// <summary>
        /// Returns the camera rotation the move axes are relative to, falling back to the character's own rotation for a value that is not a
        /// rotation, such as the zero a member holds before any client state has arrived.
        /// </summary>
        /// <param name="networkExampleCharacterComponent">The character's component.</param>
        /// <returns>A rotation the controller can project onto the character's plane.</returns>
        private Quaternion ResolveCameraRotation(NetworkExampleCharacterComponent networkExampleCharacterComponent)
        {
            Quaternion cameraRotation = networkExampleCharacterComponent.CameraRotation.Value.ToUnity();

            return Quaternion.Dot(cameraRotation, cameraRotation) < MinimumRotationSquaredLength ? Character.Motor.TransientRotation : cameraRotation;
        }

        /// <summary>
        /// Writes the controller's state into the validated members, which round it to their accuracy.
        /// </summary>
        private void CaptureCharacterState()
        {
            NetworkExampleCharacterComponent networkExampleCharacterComponent = Component;
            NetworkExampleCharacterControllerState networkExampleCharacterControllerState = Character.GetState();

            networkExampleCharacterComponent.IsJumpRequested.Value = networkExampleCharacterControllerState.IsJumpRequested;
            networkExampleCharacterComponent.IsJumpConsumed.Value = networkExampleCharacterControllerState.IsJumpConsumed;
            networkExampleCharacterComponent.IsJumpedThisTick.Value = networkExampleCharacterControllerState.IsJumpedThisTick;
            networkExampleCharacterComponent.TimeSinceJumpRequested.Value = Mathf.Min(networkExampleCharacterControllerState.TimeSinceJumpRequested, NetworkExampleCharacterComponent.MaximumTrackedSeconds);
            networkExampleCharacterComponent.TimeSinceLastAbleToJump.Value = Mathf.Min(networkExampleCharacterControllerState.TimeSinceLastAbleToJump, NetworkExampleCharacterComponent.MaximumTrackedSeconds);
            networkExampleCharacterComponent.InternalVelocityAdd.Value = networkExampleCharacterControllerState.InternalVelocityAdd.ToNative();
            networkExampleCharacterComponent.IsCrouchRequested.Value = networkExampleCharacterControllerState.IsCrouchRequested;
            networkExampleCharacterComponent.IsCrouching.Value = networkExampleCharacterControllerState.IsCrouching;
        }

        /// <summary>
        /// Reads the controller's state from the validated members.
        /// </summary>
        /// <returns>The state the members hold.</returns>
        private NetworkExampleCharacterControllerState ReadCharacterState()
        {
            NetworkExampleCharacterComponent networkExampleCharacterComponent = Component;

            return new()
            {
                IsJumpRequested = networkExampleCharacterComponent.IsJumpRequested.Value,
                IsJumpConsumed = networkExampleCharacterComponent.IsJumpConsumed.Value,
                IsJumpedThisTick = networkExampleCharacterComponent.IsJumpedThisTick.Value,
                TimeSinceJumpRequested = networkExampleCharacterComponent.TimeSinceJumpRequested.Value,
                TimeSinceLastAbleToJump = networkExampleCharacterComponent.TimeSinceLastAbleToJump.Value,
                InternalVelocityAdd = networkExampleCharacterComponent.InternalVelocityAdd.Value.ToUnity(),
                IsCrouchRequested = networkExampleCharacterComponent.IsCrouchRequested.Value,
                IsCrouching = networkExampleCharacterComponent.IsCrouching.Value
            };
        }
    }
}
