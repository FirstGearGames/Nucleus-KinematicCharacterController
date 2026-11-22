using KinematicCharacterController;
using Nucleus.Components;
using Nucleus.Integrations.Unity.Prediction;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;

namespace Nucleus.Integrations.KinematicCharacterController
{
    /// <summary>
    /// The validated state of a <see cref="PhysicsMover"/>: everything in its <see cref="PhysicsMoverState"/> plus how many ticks it has
    /// been simulated for, the clock its <see cref="IMoverController"/> reads instead of Unity's time. Carried by the system
    /// <see cref="ValidatedPhysicsMover"/> declares for itself, and kept in step with the mover by that behaviour.
    /// </summary>
    public partial class ValidatedPhysicsMoverComponent : NetworkComponent
    {
        /// <summary>
        /// The mover's position.
        /// </summary>
        public readonly NetworkMember<NumericsVector3> Position = new(role: PredictionRole.Validate, accuracy: ValidatedTransformComponent.PositionAccuracy, resultTolerance: ValidatedTransformComponent.PositionResultTolerance);
        /// <summary>
        /// The mover's rotation.
        /// </summary>
        public readonly NetworkMember<NumericsQuaternion> Rotation = new(role: PredictionRole.Validate, resultTolerance: ValidatedTransformComponent.RotationResultTolerance);
        /// <summary>
        /// The velocity the mover's last update gave it, which carries the characters standing on it.
        /// </summary>
        public readonly NetworkMember<NumericsVector3> Velocity = new(role: PredictionRole.Validate, accuracy: ValidatedKinematicCharacterMotorComponent.VelocityAccuracy, resultTolerance: ValidatedKinematicCharacterMotorComponent.VelocityResultTolerance);
        /// <summary>
        /// The angular velocity the mover's last update gave it, in radians per second, which turns the characters standing on it.
        /// </summary>
        public readonly NetworkMember<NumericsVector3> AngularVelocity = new(role: PredictionRole.Validate, accuracy: ValidatedKinematicCharacterMotorComponent.VelocityAccuracy, resultTolerance: ValidatedKinematicCharacterMotorComponent.VelocityResultTolerance);
        /// <summary>
        /// How many ticks the mover has been simulated for since it spawned, counting the tick being simulated.
        /// </summary>
        public readonly NetworkMember<uint> SimulatedTickCount = new(role: PredictionRole.Validate);
    }
}
