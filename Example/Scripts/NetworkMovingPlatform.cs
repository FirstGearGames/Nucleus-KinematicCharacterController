using KinematicCharacterController;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// A platform that slides, oscillates and turns as KCC's <c>ExampleMovingPlatform</c> does, timed by the mover's
    /// <see cref="ValidatedPhysicsMover.SimulationTime"/> instead of Unity's time, so every peer and every replay puts it in the same place
    /// on the same tick.
    /// </summary>
    [RequireComponent(typeof(ValidatedPhysicsMover))]
    public class NetworkMovingPlatform : MonoBehaviour, IMoverController
    {
        /// <summary>
        /// The axis the platform slides along.
        /// </summary>
        [SerializeField]
        [Tooltip("The axis the platform slides along.")]
        private Vector3 _translationAxis = Vector3.right;
        /// <summary>
        /// How far the platform slides either way, in metres.
        /// </summary>
        [SerializeField]
        [Tooltip("How far the platform slides either way, in metres.")]
        private float _translationPeriod = 10f;
        /// <summary>
        /// How fast the platform slides, in radians of its cycle per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How fast the platform slides, in radians of its cycle per second.")]
        private float _translationSpeed = 1f;
        /// <summary>
        /// The axis the platform turns about.
        /// </summary>
        [SerializeField]
        [Tooltip("The axis the platform turns about.")]
        private Vector3 _rotationAxis = Vector3.up;
        /// <summary>
        /// How fast the platform turns, in degrees per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How fast the platform turns, in degrees per second.")]
        private float _rotationSpeed = 10f;
        /// <summary>
        /// The axis the platform rocks about.
        /// </summary>
        [SerializeField]
        [Tooltip("The axis the platform rocks about.")]
        private Vector3 _oscillationAxis = Vector3.zero;
        /// <summary>
        /// How far the platform rocks either way, in degrees.
        /// </summary>
        [SerializeField]
        [Tooltip("How far the platform rocks either way, in degrees.")]
        private float _oscillationPeriod = 10f;
        /// <summary>
        /// How fast the platform rocks, in radians of its cycle per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How fast the platform rocks, in radians of its cycle per second.")]
        private float _oscillationSpeed = 10f;
        /// <summary>
        /// The mover's clock.
        /// </summary>
        private ValidatedPhysicsMover _validatedPhysicsMover;
        /// <summary>
        /// The position the platform was authored at, which it slides about.
        /// </summary>
        private Vector3 _originalPosition;
        /// <summary>
        /// The rotation the platform was authored at, which it turns from.
        /// </summary>
        private Quaternion _originalRotation;

        /// <inheritdoc/>
        public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
        {
            float simulationTime = _validatedPhysicsMover.SimulationTime;
            Quaternion oscillationRotation = Quaternion.Euler(_oscillationAxis.normalized * (Mathf.Sin(simulationTime * _oscillationSpeed) * _oscillationPeriod)) * _originalRotation;

            goalPosition = _originalPosition + (_translationAxis.normalized * Mathf.Sin(simulationTime * _translationSpeed) * _translationPeriod);
            goalRotation = Quaternion.Euler(_rotationAxis * _rotationSpeed * simulationTime) * oscillationRotation;
        }

        /// <summary>
        /// Records the authored pose, before the network can move the platform, and becomes the mover's controller.
        /// </summary>
        private void Awake()
        {
            _validatedPhysicsMover = GetComponent<ValidatedPhysicsMover>();
            transform.GetPositionAndRotation(out _originalPosition, out _originalRotation);
            GetComponent<PhysicsMover>().MoverController = this;
        }
    }
}
