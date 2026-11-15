using KinematicCharacterController;
using Nucleus.Connections;
using Nucleus.Integrations.Unity.Systems;
using Nucleus.Integrations.Unity;
using Nucleus.Managers.NetworkLoop;
using Nucleus.Systems;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// Drives a <see cref="DemoCharacterController"/> from client state: the player that controls the character writes its input on each
    /// live tick, and every peer that simulates the character, the server included, hands that input to the controller on the same tick.
    /// The controller's own state is validated beside the motor's, so a reconcile replays the character exactly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The character also carries a <see cref="ValidatedKinematicCharacterMotor"/>, which declares the motor's system and steps the motor;
    /// this script declares only the client state and the controller state. Teleporters are decided here too, on the early fixed update of
    /// a simulated tick and from the motor's own pose, because a trigger callback could not be repeated by a replay.
    /// </para>
    /// <para>
    /// The local player is the scene's <see cref="DemoPlayer"/>, which reads the devices and the camera. It is handed this character when
    /// this peer's client controls it, which covers a host playing its own character too.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(DemoCharacterController))]
    [RequireComponent(typeof(ValidatedKinematicCharacterMotor))]
    [NucleusHelpUrl(typeof(NetworkDemoCharacter))]
    public class NetworkDemoCharacter : NucleusBehaviour<NetworkDemoCharacterComponent>
    {
        /// <summary>
        /// The controller driven.
        /// </summary>
        public DemoCharacterController Character { get; private set; }
        /// <summary>
        /// True while this peer's player controls the character and the scene's player has been handed it.
        /// </summary>
        public bool IsLocalPlayer { get; private set; }
        /// <summary>
        /// The scene's local player, or null in a scene without one, such as a dedicated server's.
        /// </summary>
        private DemoPlayer _demoPlayer;
        /// <summary>
        /// Every teleporter in the scene.
        /// </summary>
        private NetworkTeleporter[] _networkTeleporters;

        /// <inheritdoc/>
        protected override void Awake()
        {
            Character = GetComponent<DemoCharacterController>();
            _demoPlayer = FindAnyObjectByType<DemoPlayer>();
            _networkTeleporters = FindObjectsByType<NetworkTeleporter>(FindObjectsInactive.Exclude);

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
        /// never reached the server carries the last one on without its jump, then teleporters and the input are handed to the controller
        /// ahead of the motor's step.
        /// </remarks>
        protected override void OnEarlyFixedUpdate(StepDelta stepDelta, SimulationContext simulationContext)
        {
            if (simulationContext.Type is SimulationType.None)
                return;

            NetworkDemoCharacterComponent networkDemoCharacterComponent = Component;

            if (simulationContext.Type is SimulationType.Forward && IsLocalPlayer)
                _demoPlayer.WriteClientState(networkDemoCharacterComponent);

            if (stepDelta.IsClientStateMissing)
                networkDemoCharacterComponent.TryGetLastState(isEveryMemberCarried: false);

            Teleport(networkDemoCharacterComponent);

            DemoCharacterInputs demoCharacterInputs = new()
            {
                MoveAxisForward = networkDemoCharacterComponent.MoveAxisForward.Value,
                MoveAxisRight = networkDemoCharacterComponent.MoveAxisRight.Value,
                CameraYaw = networkDemoCharacterComponent.CameraYaw.Value,
                IsJumpPressed = networkDemoCharacterComponent.IsJumpPressed.Value,
                IsCrouchHeld = networkDemoCharacterComponent.IsCrouchHeld.Value
            };

            Character.SetInputs(in demoCharacterInputs);
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
            bool isLocalPlayer = _demoPlayer != null && IsController(ControllerType.Client);

            if (isLocalPlayer == IsLocalPlayer)
                return;

            if (isLocalPlayer)
            {
                IsLocalPlayer = true;
                _demoPlayer.SetCharacter(this);
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
            _demoPlayer.ClearCharacter(this);
        }

        /// <summary>
        /// Sends the character through the teleporter it has walked into, recording the zone it stands in so it is not sent straight back.
        /// </summary>
        /// <param name="networkDemoCharacterComponent">The character's component.</param>
        /// <remarks>Tested against the motor's own pose, which needs no transform sync, so a replayed tick finds exactly what its live tick found.</remarks>
        private void Teleport(NetworkDemoCharacterComponent networkDemoCharacterComponent)
        {
            KinematicCharacterMotor kinematicCharacterMotor = Character.Motor;
            NetworkTeleporter enteredNetworkTeleporter = FindOverlappedTeleporter(kinematicCharacterMotor);
            byte teleporterZoneId = enteredNetworkTeleporter == null ? NetworkTeleporter.UnsetZoneId : enteredNetworkTeleporter.ZoneId;

            if (enteredNetworkTeleporter != null && networkDemoCharacterComponent.TeleporterZoneId.Value != teleporterZoneId)
            {
                NetworkTeleporter destinationNetworkTeleporter = enteredNetworkTeleporter.Destination;
                destinationNetworkTeleporter.transform.GetPositionAndRotation(out Vector3 destinationPosition, out Quaternion destinationRotation);

                kinematicCharacterMotor.SetPositionAndRotation(destinationPosition, destinationRotation);
                teleporterZoneId = destinationNetworkTeleporter.ZoneId;
            }

            networkDemoCharacterComponent.TeleporterZoneId.Value = teleporterZoneId;
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
        /// Writes the controller's state into the validated members, which round it to their accuracy.
        /// </summary>
        private void CaptureCharacterState()
        {
            NetworkDemoCharacterComponent networkDemoCharacterComponent = Component;
            DemoCharacterControllerState demoCharacterControllerState = Character.GetState();

            networkDemoCharacterComponent.JumpBufferRemaining.Value = demoCharacterControllerState.JumpBufferRemaining;
            networkDemoCharacterComponent.CoyoteRemaining.Value = demoCharacterControllerState.CoyoteRemaining;
            networkDemoCharacterComponent.IsJumpConsumed.Value = demoCharacterControllerState.IsJumpConsumed;
            networkDemoCharacterComponent.IsCrouching.Value = demoCharacterControllerState.IsCrouching;
        }

        /// <summary>
        /// Reads the controller's state from the validated members.
        /// </summary>
        /// <returns>The state the members hold.</returns>
        private DemoCharacterControllerState ReadCharacterState()
        {
            NetworkDemoCharacterComponent networkDemoCharacterComponent = Component;

            return new()
            {
                JumpBufferRemaining = networkDemoCharacterComponent.JumpBufferRemaining.Value,
                CoyoteRemaining = networkDemoCharacterComponent.CoyoteRemaining.Value,
                IsJumpConsumed = networkDemoCharacterComponent.IsJumpConsumed.Value,
                IsCrouching = networkDemoCharacterComponent.IsCrouching.Value
            };
        }
    }
}
