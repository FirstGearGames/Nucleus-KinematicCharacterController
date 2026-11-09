using System;
using KinematicCharacterController;
using KinematicCharacterController.Examples;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// Walks, jumps and crouches a character as KCC's <see cref="ExampleCharacterController"/> does, and hands out the jump and crouch
    /// state it keeps between updates, so a replay can put that state back.
    /// </summary>
    /// <remarks>
    /// KCC's own controller keeps the same state in private fields, which a replay could only restore by changing KCC's file. This
    /// controller is that one with its state made readable, so the integration runs on KCC exactly as shipped.
    /// </remarks>
    [RequireComponent(typeof(KinematicCharacterMotor))]
    public class NetworkExampleCharacterController : MonoBehaviour, ICharacterController
    {
        /// <summary>
        /// The motor this controller moves.
        /// </summary>
        public KinematicCharacterMotor Motor { get; private set; }
        /// <summary>
        /// The gravity the character falls by. The networked character points it at the planet's centre while it stands on the planet.
        /// </summary>
        public Vector3 Gravity
        {
            get => _gravity;
            set => _gravity = value;
        }
        /// <summary>
        /// The point the camera follows.
        /// </summary>
        public Transform CameraFollowPoint => _cameraFollowPoint;
        /// <summary>
        /// The fastest the character moves on stable ground, in metres per second.
        /// </summary>
        [SerializeField]
        [Tooltip("The fastest the character moves on stable ground, in metres per second.")]
        private float _maxStableMoveSpeed = 10f;
        /// <summary>
        /// How quickly the character's velocity on stable ground reaches the velocity its input asks for.
        /// </summary>
        [SerializeField]
        [Tooltip("How quickly the velocity on stable ground reaches the velocity the input asks for.")]
        private float _stableMovementSharpness = 15f;
        /// <summary>
        /// How quickly the character turns to face the direction its input asks for.
        /// </summary>
        [SerializeField]
        [Tooltip("How quickly the character turns to face the direction the input asks for.")]
        private float _orientationSharpness = 10f;
        /// <summary>
        /// Whether a player's character faces the way the camera looks or the way it moves.
        /// </summary>
        [SerializeField]
        [Tooltip("Whether a player's character faces the way the camera looks or the way it moves.")]
        private OrientationMethod _orientationMethod = OrientationMethod.TowardsCamera;
        /// <summary>
        /// The fastest the character's input moves it through the air, in metres per second.
        /// </summary>
        [SerializeField]
        [Tooltip("The fastest the input moves the character through the air, in metres per second.")]
        private float _maxAirMoveSpeed = 15f;
        /// <summary>
        /// How quickly the character's input speeds it up in the air, in metres per second per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How quickly the input speeds the character up in the air, in metres per second per second.")]
        private float _airAccelerationSpeed = 15f;
        /// <summary>
        /// How strongly the air slows the character.
        /// </summary>
        [SerializeField]
        [Tooltip("How strongly the air slows the character.")]
        private float _drag = 0.1f;
        /// <summary>
        /// True to let the character jump from ground too steep to stand on.
        /// </summary>
        [SerializeField]
        [Tooltip("Let the character jump from ground too steep to stand on.")]
        private bool _jumpWhenSlidingEnabled;
        /// <summary>
        /// The speed a jump launches the character upward at, in metres per second.
        /// </summary>
        [SerializeField]
        [Tooltip("The speed a jump launches the character upward at, in metres per second.")]
        private float _jumpUpSpeed = 10f;
        /// <summary>
        /// The speed a jump adds in the direction the input asks for, scaled by how far the input is pushed, in metres per second.
        /// </summary>
        [SerializeField]
        [Tooltip("The speed a jump adds in the direction of the input, scaled by how far it is pushed, in metres per second.")]
        private float _jumpScalableForwardSpeed = 10f;
        /// <summary>
        /// How long before landing a jump may be pressed and still be made on landing, in seconds.
        /// </summary>
        [SerializeField]
        [Tooltip("How long before landing a jump may be pressed and still be made on landing, in seconds.")]
        private float _jumpPreGroundingGraceTime;
        /// <summary>
        /// How long after leaving the ground the character may still jump, in seconds.
        /// </summary>
        [SerializeField]
        [Tooltip("How long after leaving the ground the character may still jump, in seconds.")]
        private float _jumpPostGroundingGraceTime;
        /// <summary>
        /// The colliders the character passes through.
        /// </summary>
        [SerializeField]
        [Tooltip("The colliders the character passes through.")]
        private Collider[] _ignoredColliders = new Collider[0];
        /// <summary>
        /// What the character tilts its up direction towards, beside facing its input.
        /// </summary>
        [SerializeField]
        [Tooltip("What the character tilts its up direction towards, beside facing its input.")]
        private BonusOrientationMethod _bonusOrientationMethod = BonusOrientationMethod.None;
        /// <summary>
        /// How quickly the character tilts its up direction.
        /// </summary>
        [SerializeField]
        [Tooltip("How quickly the character tilts its up direction.")]
        private float _bonusOrientationSharpness = 10f;
        /// <summary>
        /// The gravity the character falls by, in metres per second per second.
        /// </summary>
        [SerializeField]
        [Tooltip("The gravity the character falls by, in metres per second per second.")]
        private Vector3 _gravity = new(0f, -30f, 0f);
        /// <summary>
        /// The root of the character's mesh, squashed while it crouches.
        /// </summary>
        [SerializeField]
        [Tooltip("The root of the character's mesh, squashed while it crouches.")]
        private Transform _meshRoot;
        /// <summary>
        /// The point the camera follows.
        /// </summary>
        [SerializeField]
        [Tooltip("The point the camera follows.")]
        private Transform _cameraFollowPoint;
        /// <summary>
        /// The height of the character's capsule while it crouches, in metres.
        /// </summary>
        [SerializeField]
        [Tooltip("The height of the capsule while the character crouches, in metres.")]
        private float _crouchedCapsuleHeight = 1f;
        /// <summary>
        /// Receives the colliders the standing capsule overlaps when the character tries to stand up.
        /// </summary>
        private readonly Collider[] _probedColliders = new Collider[8];
        /// <summary>
        /// The direction and amount the input asks the character to move, on the character's plane.
        /// </summary>
        private Vector3 _moveInputVector;
        /// <summary>
        /// The direction the input asks the character to face.
        /// </summary>
        private Vector3 _lookInputVector;
        /// <summary>
        /// True while a jump has been asked for and not yet made.
        /// </summary>
        private bool _isJumpRequested;
        /// <summary>
        /// True once a jump has been made and the character has not stood on jumpable ground since.
        /// </summary>
        private bool _isJumpConsumed;
        /// <summary>
        /// True on the update the character jumped.
        /// </summary>
        private bool _isJumpedThisTick;
        /// <summary>
        /// The seconds since jump was last asked for.
        /// </summary>
        private float _timeSinceJumpRequested = Mathf.Infinity;
        /// <summary>
        /// The seconds since the character was last able to jump.
        /// </summary>
        private float _timeSinceLastAbleToJump;
        /// <summary>
        /// The velocity added from outside the character's own movement, waiting for its next update.
        /// </summary>
        private Vector3 _internalVelocityAdd;
        /// <summary>
        /// True while the character wants to crouch.
        /// </summary>
        private bool _isCrouchRequested;
        /// <summary>
        /// True while the character is crouched.
        /// </summary>
        private bool _isCrouching;
        /// <summary>
        /// The scale of the mesh root while the character crouches.
        /// </summary>
        private static readonly Vector3 CrouchedMeshScale = new(1f, 0.5f, 1f);
        /// <summary>
        /// The radius of the character's capsule, crouched or standing, in metres.
        /// </summary>
        private const float CapsuleRadius = 0.5f;
        /// <summary>
        /// The height of the character's capsule while it stands, in metres.
        /// </summary>
        private const float StandingCapsuleHeight = 2f;

        /// <summary>
        /// Hands the controller a player's input: the move axes relative to the camera, and a jump or crouch press.
        /// </summary>
        /// <param name="playerCharacterInputs">The player's input this update.</param>
        public void SetInputs(in PlayerCharacterInputs playerCharacterInputs)
        {
            Vector3 moveInputVector = Vector3.ClampMagnitude(new(playerCharacterInputs.MoveAxisRight, 0f, playerCharacterInputs.MoveAxisForward), 1f);
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(playerCharacterInputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;

            // A camera looking straight up or down has no forward on the character's plane, so its up stands in.
            if (cameraPlanarDirection.sqrMagnitude == 0f)
                cameraPlanarDirection = Vector3.ProjectOnPlane(playerCharacterInputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;

            _moveInputVector = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp) * moveInputVector;
            _lookInputVector = _orientationMethod is OrientationMethod.TowardsCamera ? cameraPlanarDirection : _moveInputVector.normalized;

            if (playerCharacterInputs.JumpDown)
            {
                _timeSinceJumpRequested = 0f;
                _isJumpRequested = true;
            }

            if (playerCharacterInputs.CrouchDown)
            {
                _isCrouchRequested = true;

                if (!_isCrouching)
                {
                    _isCrouching = true;
                    Motor.SetCapsuleDimensions(CapsuleRadius, _crouchedCapsuleHeight, _crouchedCapsuleHeight * 0.5f);
                    _meshRoot.localScale = CrouchedMeshScale;
                }
            }
            else if (playerCharacterInputs.CrouchUp)
            {
                _isCrouchRequested = false;
            }
        }

        /// <summary>
        /// Hands the controller an AI's input: the world direction to move in and the direction to face.
        /// </summary>
        /// <param name="aiCharacterInputs">The AI's input this update.</param>
        public void SetInputs(in AICharacterInputs aiCharacterInputs)
        {
            _moveInputVector = aiCharacterInputs.MoveVector;
            _lookInputVector = aiCharacterInputs.LookVector;
        }

        /// <summary>
        /// Adds a velocity to the character on its next update, such as a push from outside its own movement.
        /// </summary>
        /// <param name="velocity">The velocity to add, in metres per second.</param>
        public void AddVelocity(Vector3 velocity) => _internalVelocityAdd += velocity;

        /// <summary>
        /// Returns the controller's own simulation state. Together with its motor's state, this is all a replay needs to repeat a move.
        /// </summary>
        /// <returns>The controller's state.</returns>
        public NetworkExampleCharacterControllerState GetState() => new()
        {
            IsJumpRequested = _isJumpRequested,
            IsJumpConsumed = _isJumpConsumed,
            IsJumpedThisTick = _isJumpedThisTick,
            TimeSinceJumpRequested = _timeSinceJumpRequested,
            TimeSinceLastAbleToJump = _timeSinceLastAbleToJump,
            InternalVelocityAdd = _internalVelocityAdd,
            IsCrouchRequested = _isCrouchRequested,
            IsCrouching = _isCrouching
        };

        /// <summary>
        /// Puts back a state returned by <see cref="GetState"/>.
        /// </summary>
        /// <param name="networkExampleCharacterControllerState">The state to put back.</param>
        /// <remarks>The capsule's dimensions are part of the motor's state, so only the crouched look of the mesh is set here.</remarks>
        public void ApplyState(in NetworkExampleCharacterControllerState networkExampleCharacterControllerState)
        {
            _isJumpRequested = networkExampleCharacterControllerState.IsJumpRequested;
            _isJumpConsumed = networkExampleCharacterControllerState.IsJumpConsumed;
            _isJumpedThisTick = networkExampleCharacterControllerState.IsJumpedThisTick;
            _timeSinceJumpRequested = networkExampleCharacterControllerState.TimeSinceJumpRequested;
            _timeSinceLastAbleToJump = networkExampleCharacterControllerState.TimeSinceLastAbleToJump;
            _internalVelocityAdd = networkExampleCharacterControllerState.InternalVelocityAdd;
            _isCrouchRequested = networkExampleCharacterControllerState.IsCrouchRequested;

            if (_isCrouching == networkExampleCharacterControllerState.IsCrouching)
                return;

            _isCrouching = networkExampleCharacterControllerState.IsCrouching;
            _meshRoot.localScale = _isCrouching ? CrouchedMeshScale : Vector3.one;
        }

        /// <inheritdoc/>
        public void BeforeCharacterUpdate(float deltaTime) { }

        /// <inheritdoc/>
        /// <remarks>Turns towards the input's direction, then tilts the character's up as <see cref="_bonusOrientationMethod"/> asks.</remarks>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (_lookInputVector.sqrMagnitude > 0f && _orientationSharpness > 0f)
            {
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1f - Mathf.Exp(-_orientationSharpness * deltaTime)).normalized;
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
            }

            Vector3 currentUp = currentRotation * Vector3.up;
            float bonusOrientationFactor = 1f - Mathf.Exp(-_bonusOrientationSharpness * deltaTime);

            if (_bonusOrientationMethod is BonusOrientationMethod.TowardsGroundSlopeAndGravity && Motor.GroundingStatus.IsStableOnGround)
            {
                Vector3 initialCharacterBottomHemiCenter = Motor.TransientPosition + (currentUp * Motor.Capsule.radius);
                Vector3 smoothedGroundNormal = Vector3.Slerp(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal, bonusOrientationFactor);
                currentRotation = Quaternion.FromToRotation(currentUp, smoothedGroundNormal) * currentRotation;

                // Turn about the bottom of the capsule rather than the pivot, so the character stays on the slope.
                Motor.SetTransientPosition(initialCharacterBottomHemiCenter + (currentRotation * Vector3.down * Motor.Capsule.radius));
            }
            else
            {
                Vector3 targetUp = _bonusOrientationMethod is BonusOrientationMethod.None ? Vector3.up : -_gravity.normalized;
                Vector3 smoothedUp = Vector3.Slerp(currentUp, targetUp, bonusOrientationFactor);
                currentRotation = Quaternion.FromToRotation(currentUp, smoothedUp) * currentRotation;
            }
        }

        /// <inheritdoc/>
        /// <remarks>Moves the character on the ground or through the air, then makes a requested jump and adds any velocity from outside.</remarks>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (Motor.GroundingStatus.IsStableOnGround)
                UpdateGroundVelocity(ref currentVelocity, deltaTime);
            else
                UpdateAirVelocity(ref currentVelocity, deltaTime);

            _isJumpedThisTick = false;
            _timeSinceJumpRequested += deltaTime;

            if (_isJumpRequested && !_isJumpConsumed && (IsOnJumpableGround() || _timeSinceLastAbleToJump <= _jumpPostGroundingGraceTime))
            {
                Vector3 jumpDirection = Motor.GroundingStatus is { FoundAnyGround: true, IsStableOnGround: false } ? Motor.GroundingStatus.GroundNormal : Motor.CharacterUp;

                // Without this the motor snaps the character back to the ground on its next update.
                Motor.ForceUnground();

                currentVelocity += (jumpDirection * _jumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                currentVelocity += _moveInputVector * _jumpScalableForwardSpeed;
                _isJumpRequested = false;
                _isJumpConsumed = true;
                _isJumpedThisTick = true;
            }

            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }

        /// <inheritdoc/>
        /// <remarks>Lets a jump pressed too early lapse, keeps the jump grace timers, and stands the character up when nothing is in the way.</remarks>
        public void AfterCharacterUpdate(float deltaTime)
        {
            if (_isJumpRequested && _timeSinceJumpRequested > _jumpPreGroundingGraceTime)
                _isJumpRequested = false;

            if (IsOnJumpableGround())
            {
                if (!_isJumpedThisTick)
                    _isJumpConsumed = false;

                _timeSinceLastAbleToJump = 0f;
            }
            else
            {
                _timeSinceLastAbleToJump += deltaTime;
            }

            if (!_isCrouching || _isCrouchRequested)
                return;

            Motor.SetCapsuleDimensions(CapsuleRadius, StandingCapsuleHeight, StandingCapsuleHeight * 0.5f);

            if (Motor.CharacterOverlap(Motor.TransientPosition, Motor.TransientRotation, _probedColliders, Motor.CollidableLayers, QueryTriggerInteraction.Ignore) > 0)
            {
                Motor.SetCapsuleDimensions(CapsuleRadius, _crouchedCapsuleHeight, _crouchedCapsuleHeight * 0.5f);
            }
            else
            {
                _meshRoot.localScale = Vector3.one;
                _isCrouching = false;
            }
        }

        /// <inheritdoc/>
        public void PostGroundingUpdate(float deltaTime) { }

        /// <inheritdoc/>
        public bool IsColliderValidForCollisions(Collider collider) => Array.IndexOf(_ignoredColliders, collider) < 0;

        /// <inheritdoc/>
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

        /// <inheritdoc/>
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

        /// <inheritdoc/>
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }

        /// <inheritdoc/>
        public void OnDiscreteCollisionDetected(Collider hitCollider) { }

        /// <summary>
        /// Finds the motor and becomes its controller.
        /// </summary>
        private void Awake()
        {
            Motor = GetComponent<KinematicCharacterMotor>();
            Motor.CharacterController = this;
        }

        /// <summary>
        /// Moves the character along the ground it stands on, easing its velocity towards the one the input asks for.
        /// </summary>
        /// <param name="currentVelocity">The character's velocity, changed in place.</param>
        /// <param name="deltaTime">The seconds this update covers.</param>
        private void UpdateGroundVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 groundNormal = Motor.GroundingStatus.GroundNormal;
            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, groundNormal) * currentVelocity.magnitude;

            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
            Vector3 reorientedInput = Vector3.Cross(groundNormal, inputRight).normalized * _moveInputVector.magnitude;
            currentVelocity = Vector3.Lerp(currentVelocity, reorientedInput * _maxStableMoveSpeed, 1f - Mathf.Exp(-_stableMovementSharpness * deltaTime));
        }

        /// <summary>
        /// Moves the character through the air: speeds it up by its input to no more than <see cref="_maxAirMoveSpeed"/>, then applies
        /// gravity and drag.
        /// </summary>
        /// <param name="currentVelocity">The character's velocity, changed in place.</param>
        /// <param name="deltaTime">The seconds this update covers.</param>
        private void UpdateAirVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (_moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 addedVelocity = _moveInputVector * _airAccelerationSpeed * deltaTime;
                Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                // Under the limit the input adds speed up to it. Over it, the input may steer but not add speed in the direction of travel.
                if (currentVelocityOnInputsPlane.magnitude < _maxAirMoveSpeed)
                    addedVelocity = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, _maxAirMoveSpeed) - currentVelocityOnInputsPlane;
                else if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                    addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);

                // Keeps the input from climbing a slope too steep to stand on.
                if (Motor.GroundingStatus.FoundAnyGround && Vector3.Dot(currentVelocity + addedVelocity, addedVelocity) > 0f)
                {
                    Vector3 perpendicularObstructionNormal = Vector3.Cross(Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal), Motor.CharacterUp).normalized;
                    addedVelocity = Vector3.ProjectOnPlane(addedVelocity, perpendicularObstructionNormal);
                }

                currentVelocity += addedVelocity;
            }

            currentVelocity += _gravity * deltaTime;
            currentVelocity *= 1f / (1f + (_drag * deltaTime));
        }

        /// <summary>
        /// Returns whether the character stands on ground it may jump from.
        /// </summary>
        /// <returns>True when the character stands on stable ground, or on any ground while jumping when sliding is enabled.</returns>
        private bool IsOnJumpableGround() => _jumpWhenSlidingEnabled ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround;
    }
}
