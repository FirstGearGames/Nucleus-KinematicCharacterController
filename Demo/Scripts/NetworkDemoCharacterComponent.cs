using Nucleus.Components;

namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// The client state a player sends for a demo character every tick, and the character controller's own state beside its motor's,
    /// validated by the server and restored by a reconcile.
    /// </summary>
    /// <remarks>
    /// The client state is <see cref="DemoCharacterInputs"/>: two move axes, the camera's heading, a jump press and whether crouch is held.
    /// Crouch travels as held rather than as a press and a release, so a tick lost on the way still leaves the server crouching for as long
    /// as the player held the key. The validated half is <see cref="DemoCharacterControllerState"/> plus the teleporter zone the character
    /// stands in.
    /// </remarks>
    public partial class NetworkDemoCharacterComponent : NetworkComponent
    {
        /// <summary>
        /// How far forward or back the player is moving, from -1 to 1.
        /// </summary>
        public readonly NetworkMember<float> MoveAxisForward = new(role: PredictionRole.Predict, accuracy: AxisAccuracy);
        /// <summary>
        /// How far right or left the player is moving, from -1 to 1.
        /// </summary>
        public readonly NetworkMember<float> MoveAxisRight = new(role: PredictionRole.Predict, accuracy: AxisAccuracy);
        /// <summary>
        /// The camera's heading about the world's up axis, in degrees, which the move axes are relative to.
        /// </summary>
        public readonly NetworkMember<float> CameraYaw = new(role: PredictionRole.Predict, accuracy: AxisAccuracy);
        /// <summary>
        /// True on the tick the player pressed jump. The controlling peer sets it whenever the key goes down, and its next live tick reads it.
        /// </summary>
        /// <remarks>Not carried forward, so a tick of client state lost on the way is not taken as another press.</remarks>
        public readonly NetworkTrigger IsJumpPressed = new();
        /// <summary>
        /// True while the player holds crouch.
        /// </summary>
        public readonly NetworkMember<bool> IsCrouchHeld = new(role: PredictionRole.Predict);
        /// <summary>
        /// The seconds a pressed jump is still kept for.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="IsJumpPressed"/>, which is true on the press's tick alone: the character simulates this from the press
        /// and counts it down over the ticks after, so a press made just before landing still jumps.
        /// </remarks>
        public readonly NetworkMember<float> JumpBufferRemaining = new(role: PredictionRole.Validate, accuracy: TimeAccuracy);
        /// <summary>
        /// The seconds left in which a jump is still allowed after leaving the ground.
        /// </summary>
        /// <remarks>
        /// Separate from the motor's <see cref="ValidatedKinematicCharacterMotorComponent.IsStableOnGround"/>, which is false from the tick
        /// the character leaves the ground: this keeps a jump allowed for a moment after.
        /// </remarks>
        public readonly NetworkMember<float> CoyoteRemaining = new(role: PredictionRole.Validate, accuracy: TimeAccuracy);
        /// <summary>
        /// True once a jump has been made and the character has not stood on stable ground since.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="IsJumpPressed"/>, which says only that jump was pressed: this says a jump was made, and stays true until
        /// the character lands, so a press in the air after a jump does not jump again.
        /// </remarks>
        public readonly NetworkMember<bool> IsJumpConsumed = new(role: PredictionRole.Validate);
        /// <summary>
        /// True while the character is crouched.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="IsCrouchHeld"/>, the player's input: something above the character keeps it crouched after crouch is
        /// let go, so the character simulates whether it is crouched itself.
        /// </remarks>
        public readonly NetworkMember<bool> IsCrouching = new(role: PredictionRole.Validate);
        /// <summary>
        /// The zone of the teleporter the character stands in, or <see cref="NetworkTeleporter.UnsetZoneId"/> when it stands in none.
        /// </summary>
        /// <remarks>Arriving through a teleporter records the destination's zone, so the character is not sent straight back.</remarks>
        public readonly NetworkMember<byte> TeleporterZoneId = new(role: PredictionRole.Validate);

        /// <summary>
        /// The accuracy the move axes and the camera heading are rounded to.
        /// </summary>
        public const float AxisAccuracy = 0.01f;
        /// <summary>
        /// The accuracy the jump timers are rounded to, in seconds.
        /// </summary>
        public const float TimeAccuracy = 0.001f;
    }
}
