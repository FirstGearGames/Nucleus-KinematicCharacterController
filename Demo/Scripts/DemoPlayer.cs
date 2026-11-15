using Nucleus.Integrations.Unity;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// The local player: reads the keyboard and mouse every frame, turns the <see cref="DemoOrbitCamera"/>, and writes the input as the
    /// client state of whichever <see cref="NetworkDemoCharacter"/> this peer controls, once per tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The frames between two ticks are folded into the tick: the axes, the camera's heading and crouch are read as they stand when the
    /// tick runs, and a jump pressed on any of those frames is kept until a tick takes it, so a press shorter than a tick is never lost.
    /// </para>
    /// <para>
    /// The character is handed over by <see cref="NetworkDemoCharacter"/> when this peer's client gains control of it, and this component
    /// is enabled only while it holds one, so nothing here runs on a dedicated server or before the player has spawned.
    /// </para>
    /// </remarks>
    [NucleusHelpUrl(typeof(DemoPlayer))]
    public class DemoPlayer : MonoBehaviour
    {
        /// <summary>
        /// The character this peer controls, or null while it controls none.
        /// </summary>
        public NetworkDemoCharacter NetworkDemoCharacter { get; private set; }
        /// <summary>
        /// The camera that follows the character and that the move axes are relative to.
        /// </summary>
        [SerializeField]
        [Tooltip("The camera that follows the local character and that its movement is relative to.")]
        private DemoOrbitCamera _demoOrbitCamera;
        /// <summary>
        /// True when jump was pressed on a frame since the last tick took one.
        /// </summary>
        private bool _isJumpPressed;
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
        /// <summary>
        /// The metres one notch of the scroll wheel zooms by.
        /// </summary>
        private const float ZoomPerScroll = 10f;

        /// <summary>
        /// Takes control of a character: the camera follows it and ignores its colliders, and the cursor locks.
        /// </summary>
        /// <param name="networkDemoCharacter">The character this peer now controls.</param>
        public void SetCharacter(NetworkDemoCharacter networkDemoCharacter)
        {
            NetworkDemoCharacter = networkDemoCharacter;
            _isJumpPressed = false;

            _demoOrbitCamera.SetTarget(networkDemoCharacter.Character.CameraTarget, networkDemoCharacter.GetComponentsInChildren<Collider>());
            Cursor.lockState = CursorLockMode.Locked;

            enabled = true;
        }

        /// <summary>
        /// Lets go of a character this peer no longer controls.
        /// </summary>
        /// <param name="networkDemoCharacter">The character let go of; ignored unless it is the one held.</param>
        public void ClearCharacter(NetworkDemoCharacter networkDemoCharacter)
        {
            if (NetworkDemoCharacter != networkDemoCharacter)
                return;

            NetworkDemoCharacter = null;
            Cursor.lockState = CursorLockMode.None;

            enabled = false;
        }

        /// <summary>
        /// Writes this tick's input as the character's client state, and clears the jump the tick takes.
        /// </summary>
        /// <param name="networkDemoCharacterComponent">The component of the character this peer controls.</param>
        public void WriteClientState(NetworkDemoCharacterComponent networkDemoCharacterComponent)
        {
            networkDemoCharacterComponent.MoveAxisForward.Value = Input.GetAxisRaw(VerticalInput);
            networkDemoCharacterComponent.MoveAxisRight.Value = Input.GetAxisRaw(HorizontalInput);
            networkDemoCharacterComponent.CameraYaw.Value = _demoOrbitCamera.Yaw;
            networkDemoCharacterComponent.IsJumpPressed.Value = _isJumpPressed;
            networkDemoCharacterComponent.IsCrouchHeld.Value = Input.GetKey(KeyCode.C);

            _isJumpPressed = false;
        }

        /// <summary>
        /// Starts disabled, since there is nothing to read input for until a character is handed over.
        /// </summary>
        private void Awake() => enabled = false;

        /// <summary>
        /// Keeps a jump pressed on any frame until a tick takes it, and relocks the cursor on a click.
        /// </summary>
        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
                Cursor.lockState = CursorLockMode.Locked;

            if (Input.GetKeyDown(KeyCode.Space))
                _isJumpPressed = true;
        }

        /// <summary>
        /// Turns and zooms the camera once the frame's smoothed graphics are drawn.
        /// </summary>
        private void LateUpdate()
        {
            Vector2 lookInput = Cursor.lockState == CursorLockMode.Locked ? new(Input.GetAxisRaw(MouseXInput), Input.GetAxisRaw(MouseYInput)) : Vector2.zero;
            _demoOrbitCamera.Orbit(lookInput, Input.GetAxis(MouseScrollInput) * ZoomPerScroll);
        }
    }
}
