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
        /// Writes this tick's input as the character's client state. The jump press lands on its own, from
        /// <see cref="NetworkDemoCharacterComponent.IsJumpPressed"/>.
        /// </summary>
        /// <param name="networkDemoCharacterComponent">The component of the character this peer controls.</param>
        public void WriteClientState(NetworkDemoCharacterComponent networkDemoCharacterComponent)
        {
            networkDemoCharacterComponent.MoveAxisForward.Value = DemoInput.ReadMoveForward();
            networkDemoCharacterComponent.MoveAxisRight.Value = DemoInput.ReadMoveRight();
            networkDemoCharacterComponent.CameraYaw.Value = _demoOrbitCamera.Yaw;
            networkDemoCharacterComponent.IsCrouchHeld.Value = DemoInput.IsCrouchHeld();
        }

        /// <summary>
        /// Starts disabled, since there is nothing to read input for until a character is handed over.
        /// </summary>
        private void Awake() => enabled = false;

        /// <summary>
        /// Sets the jump trigger on any frame jump is pressed, and relocks the cursor on a click.
        /// </summary>
        private void Update()
        {
            if (DemoInput.WasClickPressed())
                Cursor.lockState = CursorLockMode.Locked;

            if (DemoInput.WasJumpPressed() && NetworkDemoCharacter.Component is { } networkDemoCharacterComponent)
                networkDemoCharacterComponent.IsJumpPressed.Set();
        }

        /// <summary>
        /// Turns and zooms the camera once the frame's smoothed graphics are drawn.
        /// </summary>
        private void LateUpdate()
        {
            Vector2 lookInput = Cursor.lockState == CursorLockMode.Locked ? DemoInput.ReadLook() : Vector2.zero;
            _demoOrbitCamera.Orbit(lookInput, DemoInput.ReadScroll() * ZoomPerScroll);
        }
    }
}
