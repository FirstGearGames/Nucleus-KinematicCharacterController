using KinematicCharacterController;
using Nucleus.Components;
using Nucleus.Integrations.Unity.Prediction;
using Nucleus.Systems;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;

namespace Nucleus.Integrations.KinematicCharacterController
{
    /// <summary>
    /// The validated state of a <see cref="KinematicCharacterMotor"/>: everything in its
    /// <see cref="KinematicCharacterMotorState"/> plus the capsule's size. Carried by the system
    /// <see cref="ValidatedKinematicCharacterMotor"/> declares for itself, and kept in step with the motor by that behaviour.
    /// </summary>
    /// <remarks>
    /// The motor's state is what KCC itself names as the whole of a motor's simulation state, so restoring it and replaying the motor's
    /// controller reproduces the same moves. The capsule is added because a controller that crouches changes it, and a replay has to
    /// start from the size the restored tick had.
    /// </remarks>
    public partial class ValidatedKinematicCharacterMotorComponent : NetworkComponent
    {
        /// <summary>
        /// The motor's position.
        /// </summary>
        public readonly NetworkMember<NumericsVector3> Position = new(role: MemberRole.Validate, accuracy: ValidatedTransformComponent.PositionAccuracy, resultTolerance: ValidatedTransformComponent.PositionResultTolerance);
        /// <summary>
        /// The motor's rotation.
        /// </summary>
        public readonly NetworkMember<NumericsQuaternion> Rotation = new(role: MemberRole.Validate, resultTolerance: ValidatedTransformComponent.RotationResultTolerance);
        /// <summary>
        /// The velocity the character's controller moves the motor by, before any velocity an attached rigidbody adds.
        /// </summary>
        public readonly NetworkMember<NumericsVector3> BaseVelocity = new(role: MemberRole.Validate, accuracy: VelocityAccuracy, resultTolerance: VelocityResultTolerance);
        /// <summary>
        /// The velocity the rigidbody the motor stands on gave it on the last update.
        /// </summary>
        public readonly NetworkMember<NumericsVector3> AttachedRigidbodyVelocity = new(role: MemberRole.Validate, accuracy: VelocityAccuracy, resultTolerance: VelocityResultTolerance);
        /// <summary>
        /// The system Id of the networked mover the motor stands on, or <see cref="NetworkSystem.UnsetId"/> when it stands on none.
        /// </summary>
        public readonly NetworkMember<uint> AttachedMoverSystemId = new(role: MemberRole.Validate);
        /// <summary>
        /// True when the motor must skip ground probing on its next update, as it does just after a jump.
        /// </summary>
        public readonly NetworkMember<bool> IsUngroundForced = new(role: MemberRole.Validate);
        /// <summary>
        /// The seconds left during which the motor keeps skipping ground probing.
        /// </summary>
        public readonly NetworkMember<float> UngroundTimeRemaining = new(role: MemberRole.Validate, accuracy: TimeAccuracy);
        /// <summary>
        /// True when the last movement iteration of the motor's update found any ground.
        /// </summary>
        public readonly NetworkMember<bool> IsGroundFoundInLastMovementIteration = new(role: MemberRole.Validate);
        /// <summary>
        /// True when the motor's last grounding found any ground, stable or not.
        /// </summary>
        public readonly NetworkMember<bool> IsAnyGroundFound = new(role: MemberRole.Validate);
        /// <summary>
        /// True when the motor's last grounding found stable ground.
        /// </summary>
        public readonly NetworkMember<bool> IsStableOnGround = new(role: MemberRole.Validate);
        /// <summary>
        /// True when the motor's last grounding prevented snapping to the ground.
        /// </summary>
        public readonly NetworkMember<bool> IsSnappingPrevented = new(role: MemberRole.Validate);
        /// <summary>
        /// The normal of the ground the motor's last grounding found.
        /// </summary>
        public readonly NetworkMember<NumericsVector3> GroundNormal = new(role: MemberRole.Validate, resultTolerance: NormalResultTolerance);
        /// <summary>
        /// The normal of the inner side of the step or ledge the motor's last grounding found.
        /// </summary>
        public readonly NetworkMember<NumericsVector3> InnerGroundNormal = new(role: MemberRole.Validate, resultTolerance: NormalResultTolerance);
        /// <summary>
        /// The normal of the outer side of the step or ledge the motor's last grounding found.
        /// </summary>
        public readonly NetworkMember<NumericsVector3> OuterGroundNormal = new(role: MemberRole.Validate, resultTolerance: NormalResultTolerance);
        /// <summary>
        /// The capsule's radius.
        /// </summary>
        public readonly NetworkMember<float> CapsuleRadius = new(role: MemberRole.Validate);
        /// <summary>
        /// The capsule's height.
        /// </summary>
        public readonly NetworkMember<float> CapsuleHeight = new(role: MemberRole.Validate);
        /// <summary>
        /// The capsule's vertical offset from the motor's position.
        /// </summary>
        public readonly NetworkMember<float> CapsuleYOffset = new(role: MemberRole.Validate);

        /// <summary>
        /// The accuracy velocities are rounded to on every write, in metres per second.
        /// </summary>
        public const float VelocityAccuracy = 0.001f;
        /// <summary>
        /// The accuracy times are rounded to on every write, in seconds.
        /// </summary>
        public const float TimeAccuracy = 0.001f;
        /// <summary>
        /// How far, in metres per second, each component of the controller's velocity may sit from the server's before it counts as a
        /// misprediction.
        /// </summary>
        public const float VelocityResultTolerance = 0.01f;
        /// <summary>
        /// How far each component of a ground normal may sit from the server's before it counts as a misprediction: a normal comes from a
        /// physics query, and a hit a hair away on either peer can round to a neighbouring value.
        /// </summary>
        public const float NormalResultTolerance = 0.01f;
    }
}
