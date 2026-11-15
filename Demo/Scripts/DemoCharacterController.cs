using KinematicCharacterController;
using Nucleus.Integrations.Unity;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// The demo's character: walks relative to the camera's heading, turns to face where it walks, jumps with a short input buffer and a
    /// short grace after leaving a ledge, and crouches while crouch is held and nothing is above it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// KCC's motor does the collision work and calls this through <see cref="ICharacterController"/>; this only decides velocity and
    /// rotation. Every time is counted in seconds of simulated ticks, never read from Unity's clock, so a replayed tick moves exactly as
    /// the live one did once <see cref="ApplyState"/> has put the state back.
    /// </para>
    /// <para>
    /// Everything that carries from one tick to the next is in <see cref="DemoCharacterControllerState"/>. The input is not state: it is
    /// handed over again on every tick, replayed ones included, through <see cref="SetInputs"/>.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(KinematicCharacterMotor))]
    [NucleusHelpUrl(typeof(DemoCharacterController))]
    public class DemoCharacterController : MonoBehaviour, ICharacterController
    {
        /// <summary>
        /// The motor this drives.
        /// </summary>
        public KinematicCharacterMotor Motor { get; private set; }
        /// <summary>
        /// The point the camera follows, under the drawn graphic so it moves smoothly.
        /// </summary>
        public Transform CameraTarget => _cameraTarget;
        /// <summary>
        /// The top walking speed, in metres per second.
        /// </summary>
        [SerializeField]
        [Tooltip("The top walking speed, in metres per second.")]
        private float _walkSpeed = 7f;
        /// <summary>
        /// The top walking speed while crouched, in metres per second.
        /// </summary>
        [SerializeField]
        [Tooltip("The top walking speed while crouched, in metres per second.")]
        private float _crouchSpeed = 3f;
        /// <summary>
        /// How quickly the walking speed changes on the ground, in metres per second per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How quickly the walking speed changes on the ground, in metres per second per second.")]
        private float _groundAcceleration = 60f;
        /// <summary>
        /// How quickly steering changes the speed in the air, in metres per second per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How quickly steering changes the speed in the air, in metres per second per second.")]
        private float _airAcceleration = 15f;
        /// <summary>
        /// The fastest steering alone can move the character sideways in the air, in metres per second.
        /// </summary>
        [SerializeField]
        [Tooltip("The fastest steering alone can move the character sideways in the air, in metres per second.")]
        private float _maximumAirSpeed = 7f;
        /// <summary>
        /// How fast the character turns to face where it walks, in degrees per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How fast the character turns to face where it walks, in degrees per second.")]
        private float _turnSpeed = 720f;
        /// <summary>
        /// The upward speed a jump leaves with, in metres per second.
        /// </summary>
        [SerializeField]
        [Tooltip("The upward speed a jump leaves with, in metres per second.")]
        private float _jumpSpeed = 9f;
        /// <summary>
        /// How long a jump pressed in the air is kept, so landing just after still jumps, in seconds.
        /// </summary>
        [SerializeField]
        [Tooltip("How long a jump pressed in the air is kept, so landing just after still jumps, in seconds.")]
        private float _jumpBufferTime = 0.15f;
        /// <summary>
        /// How long after walking off a ledge a jump is still allowed, in seconds.
        /// </summary>
        [SerializeField]
        [Tooltip("How long after walking off a ledge a jump is still allowed, in seconds.")]
        private float _coyoteTime = 0.1f;
        /// <summary>
        /// The downward acceleration in the air, in metres per second per second.
        /// </summary>
        [SerializeField]
        [Tooltip("The downward acceleration in the air, in metres per second per second.")]
        private float _gravity = 25f;
        /// <summary>
        /// The capsule's height standing, in metres.
        /// </summary>
        [SerializeField]
        [Tooltip("The capsule's height standing, in metres.")]
        private float _standingHeight = 2f;
        /// <summary>
        /// The capsule's height crouched, in metres.
        /// </summary>
        [SerializeField]
        [Tooltip("The capsule's height crouched, in metres.")]
        private float _crouchingHeight = 1.2f;
        /// <summary>
        /// The graphic squashed while crouched, or null for a character with nothing to draw, such as one on a dedicated server.
        /// </summary>
        [SerializeField]
        [Tooltip("The graphic squashed while crouched. Optional.")]
        private Transform _meshRoot;
        /// <summary>
        /// The point the camera follows.
        /// </summary>
        [SerializeField]
        [Tooltip("The point the camera follows. Put it under the drawn graphic so it moves smoothly.")]
        private Transform _cameraTarget;
        /// <summary>
        /// The direction this tick's input walks in, flat on the ground and no longer than one.
        /// </summary>
        private Vector3 _moveDirection;
        /// <summary>
        /// True while this tick's input holds crouch.
        /// </summary>
        private bool _isCrouchHeld;
        /// <summary>
        /// True once a jump has been made this tick, so landing on the same tick does not hand the jump straight back.
        /// </summary>
        private bool _isJumpedThisTick;
        /// <summary>
        /// The state carried between ticks.
        /// </summary>
        private DemoCharacterControllerState _state;
        /// <summary>
        /// Scratch space for the stand-up overlap test.
        /// </summary>
        private readonly Collider[] _overlapColliders = new Collider[8];
        /// <summary>
        /// The graphic's scale while crouched.
        /// </summary>
        private static readonly Vector3 CrouchedScale = new(1f, 0.6f, 1f);

        /// <summary>
        /// Hands over this tick's input. Called on every simulated tick, replayed ones included, before the motor steps.
        /// </summary>
        /// <param name="demoCharacterInputs">The input.</param>
        public void SetInputs(in DemoCharacterInputs demoCharacterInputs)
        {
            Vector3 moveInput = Vector3.ClampMagnitude(new(demoCharacterInputs.MoveAxisRight, 0f, demoCharacterInputs.MoveAxisForward), 1f);
            _moveDirection = Quaternion.Euler(0f, demoCharacterInputs.CameraYaw, 0f) * moveInput;
            _isCrouchHeld = demoCharacterInputs.IsCrouchHeld;

            if (demoCharacterInputs.IsJumpPressed)
                _state.JumpBufferRemaining = _jumpBufferTime;

            if (_isCrouchHeld && !_state.IsCrouching)
                SetCrouching(isCrouching: true);
        }

        /// <summary>
        /// Returns the state carried between ticks.
        /// </summary>
        /// <returns>The state.</returns>
        public DemoCharacterControllerState GetState() => _state;

        /// <summary>
        /// Puts back a state returned by <see cref="GetState"/>. The capsule belongs to the motor's state, so only the crouched look is set
        /// here.
        /// </summary>
        /// <param name="demoCharacterControllerState">The state.</param>
        public void ApplyState(in DemoCharacterControllerState demoCharacterControllerState)
        {
            _state = demoCharacterControllerState;
            DrawCrouch();
        }

        /// <inheritdoc/>
        public void BeforeCharacterUpdate(float deltaTime) => _isJumpedThisTick = false;

        /// <inheritdoc/>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (_moveDirection.sqrMagnitude <= 0f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(_moveDirection, Motor.CharacterUp);
            currentRotation = Quaternion.RotateTowards(currentRotation, targetRotation, _turnSpeed * deltaTime);
        }

        /// <inheritdoc/>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            KinematicCharacterMotor kinematicCharacterMotor = Motor;
            Vector3 characterUp = kinematicCharacterMotor.CharacterUp;
            bool isStableOnGround = kinematicCharacterMotor.GroundingStatus.IsStableOnGround;

            if (isStableOnGround)
            {
                float topSpeed = _state.IsCrouching ? _crouchSpeed : _walkSpeed;
                Vector3 targetVelocity = kinematicCharacterMotor.GetDirectionTangentToSurface(_moveDirection, kinematicCharacterMotor.GroundingStatus.GroundNormal) * (_moveDirection.magnitude * topSpeed);
                currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, _groundAcceleration * deltaTime);
            }
            else
            {
                Vector3 planarVelocity = Vector3.ProjectOnPlane(currentVelocity, characterUp);
                Vector3 verticalVelocity = currentVelocity - planarVelocity;
                Vector3 steeredVelocity = planarVelocity + (_moveDirection * (_airAcceleration * deltaTime));

                // Steering never pushes past the air limit, but never takes away speed the character already had either.
                planarVelocity = Vector3.ClampMagnitude(steeredVelocity, Mathf.Max(_maximumAirSpeed, planarVelocity.magnitude));
                currentVelocity = planarVelocity + verticalVelocity - (characterUp * (_gravity * deltaTime));
            }

            bool canJump = !_state.IsJumpConsumed && (isStableOnGround || _state.CoyoteRemaining > 0f);

            if (_state.JumpBufferRemaining > 0f && canJump)
            {
                kinematicCharacterMotor.ForceUnground();
                currentVelocity = Vector3.ProjectOnPlane(currentVelocity, characterUp) + (characterUp * _jumpSpeed);
                _state.JumpBufferRemaining = 0f;
                _state.CoyoteRemaining = 0f;
                _state.IsJumpConsumed = true;
                _isJumpedThisTick = true;
            }
        }

        /// <inheritdoc/>
        public void AfterCharacterUpdate(float deltaTime)
        {
            _state.JumpBufferRemaining = Mathf.Max(0f, _state.JumpBufferRemaining - deltaTime);

            if (Motor.GroundingStatus.IsStableOnGround && !_isJumpedThisTick)
            {
                _state.CoyoteRemaining = _coyoteTime;
                _state.IsJumpConsumed = false;
            }
            else
            {
                _state.CoyoteRemaining = Mathf.Max(0f, _state.CoyoteRemaining - deltaTime);
            }

            if (_state.IsCrouching && !_isCrouchHeld)
                SetCrouching(isCrouching: false);
        }

        /// <inheritdoc/>
        public void PostGroundingUpdate(float deltaTime) { }

        /// <inheritdoc/>
        public bool IsColliderValidForCollisions(Collider coll) => true;

        /// <inheritdoc/>
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

        /// <inheritdoc/>
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

        /// <inheritdoc/>
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }

        /// <inheritdoc/>
        public void OnDiscreteCollisionDetected(Collider hitCollider) { }

        /// <summary>
        /// Crouches, or stands up when nothing is in the way of the standing capsule.
        /// </summary>
        /// <param name="isCrouching">True to crouch; false to try standing.</param>
        private void SetCrouching(bool isCrouching)
        {
            KinematicCharacterMotor kinematicCharacterMotor = Motor;
            float radius = kinematicCharacterMotor.Capsule.radius;

            if (isCrouching)
            {
                kinematicCharacterMotor.SetCapsuleDimensions(radius, _crouchingHeight, _crouchingHeight * 0.5f);
            }
            else
            {
                kinematicCharacterMotor.SetCapsuleDimensions(radius, _standingHeight, _standingHeight * 0.5f);

                // Something above the crouched character keeps it crouched.
                if (kinematicCharacterMotor.CharacterOverlap(kinematicCharacterMotor.TransientPosition, kinematicCharacterMotor.TransientRotation, _overlapColliders, kinematicCharacterMotor.CollidableLayers, QueryTriggerInteraction.Ignore) > 0)
                {
                    kinematicCharacterMotor.SetCapsuleDimensions(radius, _crouchingHeight, _crouchingHeight * 0.5f);

                    return;
                }
            }

            _state.IsCrouching = isCrouching;
            DrawCrouch();
        }

        /// <summary>
        /// Squashes the graphic while crouched and restores it while standing.
        /// </summary>
        private void DrawCrouch()
        {
            if (_meshRoot != null)
                _meshRoot.localScale = _state.IsCrouching ? CrouchedScale : Vector3.one;
        }

        /// <summary>
        /// Becomes the motor's controller.
        /// </summary>
        private void Awake()
        {
            Motor = GetComponent<KinematicCharacterMotor>();
            Motor.CharacterController = this;
        }
    }
}
