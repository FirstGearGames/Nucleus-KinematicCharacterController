namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// One tick's input for a <see cref="DemoCharacterController"/>.
    /// </summary>
    public struct DemoCharacterInputs
    {
        /// <summary>
        /// How far forward or back to walk, from -1 to 1.
        /// </summary>
        public float MoveAxisForward;
        /// <summary>
        /// How far right or left to walk, from -1 to 1.
        /// </summary>
        public float MoveAxisRight;
        /// <summary>
        /// The camera's heading about the world's up axis, in degrees, which the move axes are relative to.
        /// </summary>
        public float CameraYaw;
        /// <summary>
        /// True on the tick jump was pressed.
        /// </summary>
        public bool IsJumpPressed;
        /// <summary>
        /// True while crouch is held.
        /// </summary>
        public bool IsCrouchHeld;
    }
}
