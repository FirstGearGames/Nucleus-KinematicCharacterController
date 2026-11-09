using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// The state a <see cref="NetworkExampleCharacterController"/> keeps between updates, beside its motor's own state. A replay puts it
    /// back with <see cref="NetworkExampleCharacterController.ApplyState"/> to repeat a move.
    /// </summary>
    public struct NetworkExampleCharacterControllerState
    {
        /// <summary>
        /// True while a jump has been asked for and not yet made.
        /// </summary>
        public bool IsJumpRequested;
        /// <summary>
        /// True once a jump has been made and the character has not stood on jumpable ground since.
        /// </summary>
        public bool IsJumpConsumed;
        /// <summary>
        /// True on the update the character jumped.
        /// </summary>
        public bool IsJumpedThisTick;
        /// <summary>
        /// The seconds since jump was last asked for.
        /// </summary>
        public float TimeSinceJumpRequested;
        /// <summary>
        /// The seconds since the character was last able to jump.
        /// </summary>
        public float TimeSinceLastAbleToJump;
        /// <summary>
        /// The velocity added from outside the character's own movement, waiting for its next update.
        /// </summary>
        public Vector3 InternalVelocityAdd;
        /// <summary>
        /// True while the character wants to crouch.
        /// </summary>
        public bool IsCrouchRequested;
        /// <summary>
        /// True while the character is crouched.
        /// </summary>
        public bool IsCrouching;
    }
}
