using CodeBoost.Logging;
using KinematicCharacterController;
using Nucleus.Components;
using Nucleus.Integrations.Unity.Prediction;
using Nucleus.Integrations.Unity.Serializers;
using Nucleus.Integrations.Unity.Systems;
using Nucleus.Managers.NetworkLoop;
using Nucleus.Systems;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController
{
    /// <summary>
    /// Keeps a <see cref="KinematicCharacterMotor"/> in step with a <see cref="ValidatedKinematicCharacterMotorComponent"/>: its whole
    /// simulation state and its capsule, predicted by the client that controls the object, checked by the server, and rewound and replayed
    /// by a reconcile with the rest of the object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game moves the character by handing its controller input on <c>OnEarlyFixedUpdate</c>, exactly as it would hand KCC input
    /// between fixed updates. This enrolls the motor with the scene's <see cref="NetworkKinematicCharacterSystem"/> on every tick this peer
    /// simulates, which steps it in KCC's own phase order, and captures where it ended on the late fixed update. A peer that only observes
    /// the object applies the received state each tick instead, and while a replay holds the object, it stands at the held tick's state.
    /// </para>
    /// <para>
    /// The rigidbody the motor stands on travels as the system Id of its <see cref="ValidatedPhysicsMover"/>, so a rigidbody that is not
    /// a networked mover reads as none after a restore. Declares a system of its own, so it is added to the character and nothing else.
    /// Draw the character through a <see cref="Unity.Components.GraphicalSmoother"/> child, since KCC's interpolation is off.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(KinematicCharacterMotor))]
    public class ValidatedKinematicCharacterMotor : ValidatedBehaviour<ValidatedKinematicCharacterMotorComponent>
    {
        /// <summary>
        /// The motor kept in step.
        /// </summary>
        public KinematicCharacterMotor Motor { get; private set; }
        /// <summary>
        /// The scene's simulation, which steps the motor and resolves the mover it stands on.
        /// </summary>
        private NetworkKinematicCharacterSystem _networkKinematicCharacterSystem;

        /// <inheritdoc/>
        /// <remarks>Declares the motor's state and the client state that makes the system predicted.</remarks>
        protected override bool TryDeclareSystem(NetworkSystemAcquiredHandler networkSystemAcquiredHandler, NetworkSystemReleasedHandler networkSystemReleasedHandler)
        {
            NetworkSystemObjectPool.RequireSystem<NetworkSystem, ValidatedKinematicCharacterMotorComponent, ValidatedClientStateComponent>(this, networkSystemAcquiredHandler, networkSystemReleasedHandler);

            return true;
        }

        /// <inheritdoc/>
        protected override void Awake()
        {
            Motor = GetComponent<KinematicCharacterMotor>();
            _networkKinematicCharacterSystem = FindAnyObjectByType<NetworkKinematicCharacterSystem>();

            if (_networkKinematicCharacterSystem == null)
                Logger<ValidatedKinematicCharacterMotor>.LogError($"[{name}] found no [{nameof(NetworkKinematicCharacterSystem)}] in the scene, so its motor is never stepped.");

            base.Awake();
        }

        /// <inheritdoc/>
        protected override void ApplySendInterval(SendInterval sendInterval)
        {
            ValidatedKinematicCharacterMotorComponent validatedKinematicCharacterMotorComponent = Component;
            validatedKinematicCharacterMotorComponent.Position.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.Rotation.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.BaseVelocity.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.AttachedRigidbodyVelocity.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.AttachedMoverSystemId.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.IsUngroundForced.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.UngroundTimeRemaining.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.IsGroundFoundInLastMovementIteration.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.IsAnyGroundFound.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.IsStableOnGround.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.IsSnappingPrevented.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.GroundNormal.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.InnerGroundNormal.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.OuterGroundNormal.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.CapsuleRadius.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.CapsuleHeight.SetSendInterval(sendInterval);
            validatedKinematicCharacterMotorComponent.CapsuleYOffset.SetSendInterval(sendInterval);
        }

        /// <inheritdoc/>
        /// <remarks>Enrolls the motor in the tick, which the scene's simulation steps once every system's early fixed update has run.</remarks>
        protected override void OnSimulatedEarlyFixedUpdate(StepDelta stepDelta)
        {
            if (_networkKinematicCharacterSystem != null)
                _networkKinematicCharacterSystem.Enroll(Motor);
        }

        /// <inheritdoc/>
        protected override void Capture()
        {
            ValidatedKinematicCharacterMotorComponent validatedKinematicCharacterMotorComponent = Component;
            KinematicCharacterMotor kinematicCharacterMotor = Motor;
            KinematicCharacterMotorState kinematicCharacterMotorState = kinematicCharacterMotor.GetState();
            CharacterTransientGroundingReport characterTransientGroundingReport = kinematicCharacterMotorState.GroundingStatus;
            CapsuleCollider capsuleCollider = kinematicCharacterMotor.Capsule;

            validatedKinematicCharacterMotorComponent.Position.Value = kinematicCharacterMotorState.Position.ToNative();
            validatedKinematicCharacterMotorComponent.Rotation.Value = kinematicCharacterMotorState.Rotation.ToNative();
            validatedKinematicCharacterMotorComponent.BaseVelocity.Value = kinematicCharacterMotorState.BaseVelocity.ToNative();
            validatedKinematicCharacterMotorComponent.AttachedRigidbodyVelocity.Value = kinematicCharacterMotorState.AttachedRigidbodyVelocity.ToNative();
            validatedKinematicCharacterMotorComponent.IsUngroundForced.Value = kinematicCharacterMotorState.MustUnground;
            validatedKinematicCharacterMotorComponent.UngroundTimeRemaining.Value = kinematicCharacterMotorState.MustUngroundTime;
            validatedKinematicCharacterMotorComponent.IsGroundFoundInLastMovementIteration.Value = kinematicCharacterMotorState.LastMovementIterationFoundAnyGround;
            validatedKinematicCharacterMotorComponent.IsAnyGroundFound.Value = characterTransientGroundingReport.FoundAnyGround;
            validatedKinematicCharacterMotorComponent.IsStableOnGround.Value = characterTransientGroundingReport.IsStableOnGround;
            validatedKinematicCharacterMotorComponent.IsSnappingPrevented.Value = characterTransientGroundingReport.SnappingPrevented;
            validatedKinematicCharacterMotorComponent.GroundNormal.Value = characterTransientGroundingReport.GroundNormal.ToNative();
            validatedKinematicCharacterMotorComponent.InnerGroundNormal.Value = characterTransientGroundingReport.InnerGroundNormal.ToNative();
            validatedKinematicCharacterMotorComponent.OuterGroundNormal.Value = characterTransientGroundingReport.OuterGroundNormal.ToNative();
            validatedKinematicCharacterMotorComponent.CapsuleRadius.Value = capsuleCollider.radius;
            validatedKinematicCharacterMotorComponent.CapsuleHeight.Value = capsuleCollider.height;
            validatedKinematicCharacterMotorComponent.CapsuleYOffset.Value = capsuleCollider.center.y;

            if (_networkKinematicCharacterSystem != null)
            {
                _networkKinematicCharacterSystem.TryGetMoverSystemId(kinematicCharacterMotorState.AttachedRigidbody, out uint attachedMoverSystemId);
                validatedKinematicCharacterMotorComponent.AttachedMoverSystemId.Value = attachedMoverSystemId;
            }
        }

        /// <inheritdoc/>
        /// <remarks>The capsule is sized first, since the motor's state is only meaningful for the capsule it was captured with.</remarks>
        protected override void Apply()
        {
            ValidatedKinematicCharacterMotorComponent validatedKinematicCharacterMotorComponent = Component;
            KinematicCharacterMotor kinematicCharacterMotor = Motor;
            float capsuleRadius = validatedKinematicCharacterMotorComponent.CapsuleRadius.Value;
            float capsuleHeight = validatedKinematicCharacterMotorComponent.CapsuleHeight.Value;
            float capsuleYOffset = validatedKinematicCharacterMotorComponent.CapsuleYOffset.Value;

            CapsuleCollider capsuleCollider = kinematicCharacterMotor.Capsule;

            // The motor keeps its dimensions private and writes them onto its capsule, so the capsule is what is read back.
            if (capsuleRadius != capsuleCollider.radius || capsuleHeight != capsuleCollider.height || capsuleYOffset != capsuleCollider.center.y)
                kinematicCharacterMotor.SetCapsuleDimensions(capsuleRadius, capsuleHeight, capsuleYOffset);

            Rigidbody attachedRigidbody = null;

            if (_networkKinematicCharacterSystem != null)
                _networkKinematicCharacterSystem.TryGetMoverRigidbody(validatedKinematicCharacterMotorComponent.AttachedMoverSystemId.Value, out attachedRigidbody);

            KinematicCharacterMotorState kinematicCharacterMotorState = new()
            {
                Position = validatedKinematicCharacterMotorComponent.Position.Value.ToUnity(),
                Rotation = validatedKinematicCharacterMotorComponent.Rotation.Value.ToUnity(),
                BaseVelocity = validatedKinematicCharacterMotorComponent.BaseVelocity.Value.ToUnity(),
                AttachedRigidbodyVelocity = validatedKinematicCharacterMotorComponent.AttachedRigidbodyVelocity.Value.ToUnity(),
                MustUnground = validatedKinematicCharacterMotorComponent.IsUngroundForced.Value,
                MustUngroundTime = validatedKinematicCharacterMotorComponent.UngroundTimeRemaining.Value,
                LastMovementIterationFoundAnyGround = validatedKinematicCharacterMotorComponent.IsGroundFoundInLastMovementIteration.Value,
                GroundingStatus = new()
                {
                    FoundAnyGround = validatedKinematicCharacterMotorComponent.IsAnyGroundFound.Value,
                    IsStableOnGround = validatedKinematicCharacterMotorComponent.IsStableOnGround.Value,
                    SnappingPrevented = validatedKinematicCharacterMotorComponent.IsSnappingPrevented.Value,
                    GroundNormal = validatedKinematicCharacterMotorComponent.GroundNormal.Value.ToUnity(),
                    InnerGroundNormal = validatedKinematicCharacterMotorComponent.InnerGroundNormal.Value.ToUnity(),
                    OuterGroundNormal = validatedKinematicCharacterMotorComponent.OuterGroundNormal.Value.ToUnity()
                },
                AttachedRigidbody = attachedRigidbody
            };

            kinematicCharacterMotor.ApplyState(kinematicCharacterMotorState, bypassInterpolation: true);
        }

        /// <inheritdoc/>
        /// <remarks>Stands the character at the held tick's state, which is where the server saw it when the replay's first tick ran.</remarks>
        protected override void Hold() => Apply();

        /// <inheritdoc/>
        /// <remarks>Returns the character to its latest state.</remarks>
        protected override void Release() => Apply();
    }
}
