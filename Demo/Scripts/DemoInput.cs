#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// The keyboard and mouse reads the demo drives from: through the Input System when the project's active input handling enables it,
    /// and through the legacy input class otherwise, so the demo runs under either setting.
    /// </summary>
    /// <remarks>
    /// Under the Input System a device that is absent, such as the keyboard in a batch-mode run, reads as no input. Mouse movement and the
    /// scroll wheel are scaled to what the legacy axes give, so the camera turns and zooms the same under either.
    /// </remarks>
    public static class DemoInput
    {
#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// The legacy mouse axes' sensitivity, which scales the pointer's movement in pixels to the same turn.
        /// </summary>
        private const float MouseDeltaScale = 0.1f;
        /// <summary>
        /// What one notch of the scroll wheel reads as on the legacy axis. The wheel is clamped to one notch a frame first, because its
        /// raw range differs by platform and by the Input System's settings.
        /// </summary>
        private const float ScrollPerNotch = 0.1f;
#else
        /// <summary>
        /// The input axis for looking left and right.
        /// </summary>
        private const string MouseXInput = "Mouse X";
        /// <summary>
        /// The input axis for looking up and down.
        /// </summary>
        private const string MouseYInput = "Mouse Y";
        /// <summary>
        /// The input axis for zooming the camera.
        /// </summary>
        private const string MouseScrollInput = "Mouse ScrollWheel";
        /// <summary>
        /// The input axis for moving right and left.
        /// </summary>
        private const string HorizontalInput = "Horizontal";
        /// <summary>
        /// The input axis for moving forward and back.
        /// </summary>
        private const string VerticalInput = "Vertical";
#endif

        /// <summary>
        /// Reads the forward and back move axis: W or the up arrow forward, S or the down arrow back.
        /// </summary>
        /// <returns>1 forward, -1 back, 0 for neither or both.</returns>
        public static float ReadMoveForward()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current is { } keyboard ? ReadAxis(keyboard.sKey, keyboard.downArrowKey, keyboard.wKey, keyboard.upArrowKey) : 0f;
#else
            return Input.GetAxisRaw(VerticalInput);
#endif
        }

        /// <summary>
        /// Reads the right and left move axis: D or the right arrow right, A or the left arrow left.
        /// </summary>
        /// <returns>1 right, -1 left, 0 for neither or both.</returns>
        public static float ReadMoveRight()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current is { } keyboard ? ReadAxis(keyboard.aKey, keyboard.leftArrowKey, keyboard.dKey, keyboard.rightArrowKey) : 0f;
#else
            return Input.GetAxisRaw(HorizontalInput);
#endif
        }

        /// <summary>
        /// Whether crouch, C, is held.
        /// </summary>
        /// <returns>True while C is held.</returns>
        public static bool IsCrouchHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current is { } keyboard && keyboard.cKey.isPressed;
#else
            return Input.GetKey(KeyCode.C);
#endif
        }

        /// <summary>
        /// Whether jump, Space, was pressed this frame.
        /// </summary>
        /// <returns>True on the frame Space goes down.</returns>
        public static bool WasJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current is { } keyboard && keyboard.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        /// <summary>
        /// Whether launch, Return, was pressed this frame.
        /// </summary>
        /// <returns>True on the frame Return goes down.</returns>
        public static bool WasLaunchPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current is { } keyboard && keyboard.enterKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Return);
#endif
        }

        /// <summary>
        /// Whether the left mouse button was pressed this frame.
        /// </summary>
        /// <returns>True on the frame the left button goes down.</returns>
        public static bool WasClickPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current is { } mouse && mouse.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        /// <summary>
        /// Reads this frame's mouse movement on the scale of the legacy mouse axes.
        /// </summary>
        /// <returns>The look input, x right and y up.</returns>
        public static Vector2 ReadLook()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current is { } mouse ? mouse.delta.ReadValue() * MouseDeltaScale : Vector2.zero;
#else
            return new(Input.GetAxisRaw(MouseXInput), Input.GetAxisRaw(MouseYInput));
#endif
        }

        /// <summary>
        /// Reads this frame's scroll wheel on the scale of the legacy scroll axis.
        /// </summary>
        /// <returns>Positive for scrolling up, negative for down, 0 for none.</returns>
        public static float ReadScroll()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current is { } mouse ? Mathf.Clamp(mouse.scroll.ReadValue().y, -1f, 1f) * ScrollPerNotch : 0f;
#else
            return Input.GetAxis(MouseScrollInput);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// Folds two keys each way into one axis, as the legacy input class's keyboard axes do.
        /// </summary>
        /// <param name="negativeKey">A key that pushes the axis negative.</param>
        /// <param name="alternativeNegativeKey">The other key that pushes the axis negative.</param>
        /// <param name="positiveKey">A key that pushes the axis positive.</param>
        /// <param name="alternativePositiveKey">The other key that pushes the axis positive.</param>
        /// <returns>1, -1, or 0 for neither or both.</returns>
        private static float ReadAxis(ButtonControl negativeKey, ButtonControl alternativeNegativeKey, ButtonControl positiveKey, ButtonControl alternativePositiveKey)
        {
            float positive = positiveKey.isPressed || alternativePositiveKey.isPressed ? 1f : 0f;
            float negative = negativeKey.isPressed || alternativeNegativeKey.isPressed ? 1f : 0f;

            return positive - negative;
        }
#endif
    }
}
