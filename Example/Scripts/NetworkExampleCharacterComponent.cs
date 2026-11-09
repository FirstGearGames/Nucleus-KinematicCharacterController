using Nucleus.Components;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// The client state a player sends for an example character every tick, and the character controller's own state beside its motor's,
    /// validated by the server and restored by a reconcile.
    /// </summary>
    /// <remarks>
    /// The client state is exactly what KCC's example player hands its character: the two move axes, the camera's rotation, a jump press and
    /// whether crouch is held. Crouch travels as held rather than as KCC's press and release, so a tick lost on the way still leaves the
    /// server crouching for as long as the player held the key. The validated half is everything in
    /// <see cref="NetworkExampleCharacterControllerState"/>, plus the two facts the networked
    /// playground adds: whether the character is under the planet's gravity, and which teleporter zone it stands in.
    /// </remarks>
    public partial class NetworkExampleCharacterComponent : NetworkComponent
    {
        /// <summary>
        /// How far forward or back the player is moving, from -1 to 1.
        /// </summary>
        public readonly NetworkMember<float> MoveAxisForward = new(role: MemberRole.Predict, accuracy: AxisAccuracy);
        /// <summary>
        /// How far right or left the player is moving, from -1 to 1.
        /// </summary>
        public readonly NetworkMember<float> MoveAxisRight = new(role: MemberRole.Predict, accuracy: AxisAccuracy);
        /// <summary>
        /// The rotation of the player's camera, which the move axes are relative to.
        /// </summary>
        public readonly NetworkMember<NumericsQuaternion> CameraRotation = new(role: MemberRole.Predict);
        /// <summary>
        /// True on the tick the player pressed jump.
        /// </summary>
        /// <remarks>Not carried forward, so a tick of client state lost on the way is not taken as another press.</remarks>
        public readonly NetworkMember<bool> IsJumpPressed = new(role: MemberRole.Predict, isCarriedForward: false);
        /// <summary>
        /// True while the player holds crouch.
        /// </summary>
        public readonly NetworkMember<bool> IsCrouchHeld = new(role: MemberRole.Predict);
        /// <summary>
        /// True while a jump has been asked for and not yet made.
        /// </summary>
        public readonly NetworkMember<bool> IsJumpRequested = new(role: MemberRole.Validate);
        /// <summary>
        /// True once a jump has been made and the character has not stood on stable ground since.
        /// </summary>
        public readonly NetworkMember<bool> IsJumpConsumed = new(role: MemberRole.Validate);
        /// <summary>
        /// True on the tick the character jumped.
        /// </summary>
        public readonly NetworkMember<bool> IsJumpedThisTick = new(role: MemberRole.Validate);
        /// <summary>
        /// The seconds since jump was last asked for, held at <see cref="MaximumTrackedSeconds"/>.
        /// </summary>
        public readonly NetworkMember<float> TimeSinceJumpRequested = new(role: MemberRole.Validate, accuracy: TimeAccuracy);
        /// <summary>
        /// The seconds since the character was last able to jump, held at <see cref="MaximumTrackedSeconds"/>.
        /// </summary>
        public readonly NetworkMember<float> TimeSinceLastAbleToJump = new(role: MemberRole.Validate, accuracy: TimeAccuracy);
        /// <summary>
        /// The velocity added to the character from outside its own movement, waiting for its next update.
        /// </summary>
        public readonly NetworkMember<NumericsVector3> InternalVelocityAdd = new(role: MemberRole.Validate, accuracy: ValidatedKinematicCharacterMotorComponent.VelocityAccuracy);
        /// <summary>
        /// True while the character wants to crouch.
        /// </summary>
        public readonly NetworkMember<bool> IsCrouchRequested = new(role: MemberRole.Validate);
        /// <summary>
        /// True while the character is crouched.
        /// </summary>
        public readonly NetworkMember<bool> IsCrouching = new(role: MemberRole.Validate);
        /// <summary>
        /// True while the character is under the planet's gravity rather than its own.
        /// </summary>
        public readonly NetworkMember<bool> IsOnPlanet = new(role: MemberRole.Validate);
        /// <summary>
        /// The zone of the teleporter the character stands in, or <see cref="NetworkTeleporter.UnsetZoneId"/> when it stands in none.
        /// </summary>
        /// <remarks>Arriving through a teleporter records the destination's zone, so the character is not sent straight back.</remarks>
        public readonly NetworkMember<byte> TeleporterZoneId = new(role: MemberRole.Validate);

        /// <summary>
        /// The accuracy the move axes are rounded to.
        /// </summary>
        public const float AxisAccuracy = 0.01f;
        /// <summary>
        /// The accuracy the jump timers are rounded to, in seconds.
        /// </summary>
        public const float TimeAccuracy = 0.001f;
        /// <summary>
        /// The most seconds a jump timer records. The controller starts one at infinity, which no member can hold, and only ever compares
        /// them against grace periods a fraction of a second long.
        /// </summary>
        public const float MaximumTrackedSeconds = 1000f;
    }
}
