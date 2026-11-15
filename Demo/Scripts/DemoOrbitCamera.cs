using Nucleus.Integrations.Unity;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// A third-person camera that orbits a target: the mouse turns it, the scroll wheel pulls it in and out, and it moves in front of
    /// anything that would block the view.
    /// </summary>
    /// <remarks>
    /// It follows whatever it is pointed at in <c>LateUpdate</c>, after the network loop has drawn the frame's smoothed graphics, so
    /// pointing it at a point under a character's smoothed graphic gives a steady view.
    /// </remarks>
    [NucleusHelpUrl(typeof(DemoOrbitCamera))]
    public class DemoOrbitCamera : MonoBehaviour
    {
        /// <summary>
        /// The camera's heading about the world's up axis, in degrees.
        /// </summary>
        public float Yaw { get; private set; }
        /// <summary>
        /// The distance from the target the camera settles at, in metres.
        /// </summary>
        [SerializeField]
        [Tooltip("The distance from the target the camera settles at, in metres.")]
        private float _distance = 6f;
        /// <summary>
        /// The closest the scroll wheel brings the camera, in metres.
        /// </summary>
        [SerializeField]
        [Tooltip("The closest the scroll wheel brings the camera, in metres.")]
        private float _minimumDistance = 2f;
        /// <summary>
        /// The farthest the scroll wheel takes the camera, in metres.
        /// </summary>
        [SerializeField]
        [Tooltip("The farthest the scroll wheel takes the camera, in metres.")]
        private float _maximumDistance = 12f;
        /// <summary>
        /// The degrees the camera turns per unit of mouse movement.
        /// </summary>
        [SerializeField]
        [Tooltip("The degrees the camera turns per unit of mouse movement.")]
        private float _lookSensitivity = 3f;
        /// <summary>
        /// The lowest the camera looks, in degrees below level.
        /// </summary>
        [SerializeField]
        [Tooltip("The lowest the camera looks, in degrees below level.")]
        private float _minimumPitch = -30f;
        /// <summary>
        /// The highest the camera looks down from, in degrees above level.
        /// </summary>
        [SerializeField]
        [Tooltip("The highest the camera looks down from, in degrees above level.")]
        private float _maximumPitch = 75f;
        /// <summary>
        /// The radius kept clear between the camera and what is behind it, in metres.
        /// </summary>
        [SerializeField]
        [Tooltip("The radius kept clear between the camera and what is behind it, in metres.")]
        private float _obstructionRadius = 0.2f;
        /// <summary>
        /// The point orbited, or null while there is none.
        /// </summary>
        private Transform _target;
        /// <summary>
        /// The colliders that never block the view, the target's own.
        /// </summary>
        private Collider[] _ignoredColliders = System.Array.Empty<Collider>();
        /// <summary>
        /// The camera's angle above level, in degrees.
        /// </summary>
        private float _pitch = 20f;
        /// <summary>
        /// Scratch space for the obstruction test.
        /// </summary>
        private readonly RaycastHit[] _raycastHits = new RaycastHit[16];

        /// <summary>
        /// Starts orbiting a target, facing the way it faces.
        /// </summary>
        /// <param name="target">The point to orbit.</param>
        /// <param name="ignoredColliders">Colliders that never block the view, the target's own.</param>
        public void SetTarget(Transform target, Collider[] ignoredColliders)
        {
            _target = target;
            _ignoredColliders = ignoredColliders;
            Yaw = target.eulerAngles.y;
        }

        /// <summary>
        /// Turns and zooms the camera, then places it.
        /// </summary>
        /// <param name="lookInput">The mouse movement this frame.</param>
        /// <param name="zoomInput">The scroll this frame; positive pulls the camera in.</param>
        public void Orbit(Vector2 lookInput, float zoomInput)
        {
            if (_target == null)
                return;

            Yaw = Mathf.Repeat(Yaw + (lookInput.x * _lookSensitivity), 360f);
            _pitch = Mathf.Clamp(_pitch - (lookInput.y * _lookSensitivity), _minimumPitch, _maximumPitch);
            _distance = Mathf.Clamp(_distance - zoomInput, _minimumDistance, _maximumDistance);

            Quaternion rotation = Quaternion.Euler(_pitch, Yaw, 0f);
            Vector3 targetPosition = _target.position;
            Vector3 backward = rotation * Vector3.back;
            float distance = FindClearDistance(targetPosition, backward);

            transform.SetPositionAndRotation(targetPosition + (backward * distance), rotation);
        }

        /// <summary>
        /// Finds how far behind the target the camera can sit before something other than the target is in the way.
        /// </summary>
        /// <param name="targetPosition">Where the target is.</param>
        /// <param name="backward">The direction from the target to the camera.</param>
        /// <returns>The distance to place the camera at.</returns>
        private float FindClearDistance(Vector3 targetPosition, Vector3 backward)
        {
            float clearDistance = _distance;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(targetPosition, _obstructionRadius, backward, _raycastHits, _distance, ~0, QueryTriggerInteraction.Ignore);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit raycastHit = _raycastHits[hitIndex];

                if (raycastHit.distance > 0f && raycastHit.distance < clearDistance && System.Array.IndexOf(_ignoredColliders, raycastHit.collider) < 0)
                    clearDistance = raycastHit.distance;
            }

            return clearDistance;
        }
    }
}
