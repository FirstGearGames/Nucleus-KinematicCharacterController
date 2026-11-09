using KinematicCharacterController;
using KinematicCharacterController.Examples;
using Nucleus.Integrations.Unity.Serializers;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// The local player: reads the keyboard, the mouse and the camera every frame, as KCC's <see cref="ExamplePlayer"/> does, and writes
    /// them as the client state of whichever <see cref="NetworkExampleCharacter"/> this peer controls, once per tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// KCC's player hands its character input every frame. Here the frames between two ticks are folded into the tick: the axes, the
    /// camera and crouch are read as they stand when the tick runs, and a jump pressed on any of those frames is kept until the tick
    /// takes it, so a press shorter than a tick is never lost.
    /// </para>
    /// <para>
    /// The character is handed over by <see cref="NetworkExampleCharacter"/> when this peer's client gains control of it, and this
    /// component is enabled only while it holds one, so nothing here runs on a dedicated server or before the player has spawned.
    /// </para>
    /// </remarks>
    public class NetworkExamplePlayer : MonoBehaviour
    {
        /// <summary>
        /// The character this peer controls, or null while it controls none.
        /// </summary>
        public NetworkExampleCharacter NetworkExampleCharacter { get; private set; }
        /// <summary>
        /// The camera that follows the character and that the move axes are relative to.
        /// </summary>
        [SerializeField]
        [Tooltip("The camera that follows the local character and that its movement is relative to.")]
        private ExampleCharacterCamera _characterCamera;
        /// <summary>
        /// True when jump was pressed on a frame since the last tick took one.
        /// </summary>
        private bool _isJumpPressed;
        /// <summary>
        /// The rotation of the mover the character stood on last frame, which the camera turns with, or null while it stands on none.
        /// </summary>
        private Quaternion? _attachedMoverRotation;
        /// <summary>
        /// The input axis for looking up and down.
        /// </summary>
        private const string MouseYInput = "Mouse Y";
        /// <summary>
        /// The input axis for looking left and right.
        /// </summary>
        private const string MouseXInput = "Mouse X";
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
        /// Takes control of a character: the camera follows it and ignores its colliders, and the cursor locks.
        /// </summary>
        /// <param name="networkExampleCharacter">The character this peer now controls.</param>
        public void SetCharacter(NetworkExampleCharacter networkExampleCharacter)
        {
            NetworkExampleCharacter = networkExampleCharacter;
            _isJumpPressed = false;
            _attachedMoverRotation = null;

            _characterCamera.SetFollowTransform(networkExampleCharacter.Character.CameraFollowPoint);
            _characterCamera.IgnoredColliders.Clear();
            _characterCamera.IgnoredColliders.AddRange(networkExampleCharacter.GetComponentsInChildren<Collider>());
            Cursor.lockState = CursorLockMode.Locked;

            enabled = true;
        }

        /// <summary>
        /// Lets go of a character this peer no longer controls.
        /// </summary>
        /// <param name="networkExampleCharacter">The character let go of; ignored unless it is the one held.</param>
        public void ClearCharacter(NetworkExampleCharacter networkExampleCharacter)
        {
            if (NetworkExampleCharacter != networkExampleCharacter)
                return;

            NetworkExampleCharacter = null;
            Cursor.lockState = CursorLockMode.None;

            enabled = false;
        }

        /// <summary>
        /// Writes this tick's input as the character's client state, and clears the jump the tick takes.
        /// </summary>
        /// <param name="networkExampleCharacterComponent">The component of the character this peer controls.</param>
        public void WriteClientState(NetworkExampleCharacterComponent networkExampleCharacterComponent)
        {
            networkExampleCharacterComponent.MoveAxisForward.Value = Input.GetAxisRaw(VerticalInput);
            networkExampleCharacterComponent.MoveAxisRight.Value = Input.GetAxisRaw(HorizontalInput);
            networkExampleCharacterComponent.CameraRotation.Value = _characterCamera.Transform.rotation.ToNative();
            networkExampleCharacterComponent.IsJumpPressed.Value = _isJumpPressed;
            networkExampleCharacterComponent.IsCrouchHeld.Value = Input.GetKey(KeyCode.C);

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
        /// Turns the camera with the mover the character stands on, then applies the mouse and the scroll wheel to it, as KCC's player does.
        /// </summary>
        /// <remarks>
        /// KCC's player turns the camera by the rotation its interpolation gives the mover, which is off under Nucleus, so the turn is
        /// measured from the mover's rotation frame to frame instead.
        /// </remarks>
        private void LateUpdate()
        {
            KinematicCharacterMotor kinematicCharacterMotor = NetworkExampleCharacter.Character.Motor;
            Rigidbody attachedRigidbody = kinematicCharacterMotor.AttachedRigidbody;

            if (attachedRigidbody == null)
            {
                _attachedMoverRotation = null;
            }
            else
            {
                Quaternion attachedMoverRotation = attachedRigidbody.transform.rotation;

                if (_characterCamera.RotateWithPhysicsMover && _attachedMoverRotation.HasValue)
                {
                    Quaternion rotationDelta = attachedMoverRotation * Quaternion.Inverse(_attachedMoverRotation.Value);
                    _characterCamera.PlanarDirection = Vector3.ProjectOnPlane(rotationDelta * _characterCamera.PlanarDirection, kinematicCharacterMotor.CharacterUp).normalized;
                }

                _attachedMoverRotation = attachedMoverRotation;
            }

            Vector3 lookInputVector = Cursor.lockState == CursorLockMode.Locked ? new(Input.GetAxisRaw(MouseXInput), Input.GetAxisRaw(MouseYInput), 0f) : Vector3.zero;
            _characterCamera.UpdateWithInput(Time.deltaTime, -Input.GetAxis(MouseScrollInput), lookInputVector);

            if (Input.GetMouseButtonDown(1))
                _characterCamera.TargetDistance = _characterCamera.TargetDistance == 0f ? _characterCamera.DefaultDistance : 0f;
        }
    }
}
