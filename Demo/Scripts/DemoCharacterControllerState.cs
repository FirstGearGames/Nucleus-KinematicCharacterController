namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// Everything a <see cref="DemoCharacterController"/> carries from one tick to the next, beside its motor's own state.
    /// </summary>
    public struct DemoCharacterControllerState
    {
        /// <summary>
        /// The seconds a pressed jump is still kept for, or zero when none is waiting.
        /// </summary>
        public float JumpBufferRemaining;
        /// <summary>
        /// The seconds left in which a jump is still allowed after leaving the ground.
        /// </summary>
        public float CoyoteRemaining;
        /// <summary>
        /// True once a jump has been made and the character has not stood on stable ground since.
        /// </summary>
        public bool IsJumpConsumed;
        /// <summary>
        /// True while the character is crouched.
        /// </summary>
        public bool IsCrouching;
    }
}
