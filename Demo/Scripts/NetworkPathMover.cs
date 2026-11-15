using KinematicCharacterController;
using Nucleus.Integrations.Unity;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// Moves a platform along a fixed path and turns it at a fixed rate, working out its pose purely from the mover's
    /// <see cref="ValidatedPhysicsMover.SimulationTime"/>, so a client predicting the mover ahead of the server puts it exactly where the
    /// server will have it when the client's own input arrives there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The path never changes, so nothing about the mover needs remembering from one tick to the next: the pose at any tick is a function
    /// of that tick's clock alone. A client simulates the mover on its own ticks, which run ahead of the server's by the round trip, and a
    /// replay recomputes each replayed tick from the restored clock, so a character riding the platform is carried exactly as the server
    /// carries it.
    /// </para>
    /// <para>
    /// The path runs through <see cref="_waypoints"/>, offsets from the pose the mover was placed at, at <see cref="_speed"/>. It runs back
    /// the way it came at each end, or with <see cref="_loopEnabled"/> on, from the last waypoint straight back to the first.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(ValidatedPhysicsMover))]
    [NucleusHelpUrl(typeof(NetworkPathMover))]
    public class NetworkPathMover : MonoBehaviour, IMoverController
    {
        /// <summary>
        /// The points the path runs through, as offsets in world axes from the position the mover was placed at.
        /// </summary>
        [SerializeField]
        [Tooltip("The points the path runs through, as offsets in world axes from where the mover was placed. Leave one point for a mover that only turns.")]
        private Vector3[] _waypoints = { Vector3.zero, new(0f, 0f, 8f) };
        /// <summary>
        /// How fast the mover travels along the path, in metres per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How fast the mover travels along the path, in metres per second.")]
        private float _speed = 2f;
        /// <summary>
        /// True to run from the last waypoint straight back to the first; false to run back the way it came.
        /// </summary>
        [SerializeField]
        [Tooltip("Run from the last waypoint straight back to the first, rather than back the way it came.")]
        private bool _loopEnabled;
        /// <summary>
        /// How fast the mover turns about the world's up axis, in degrees per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How fast the mover turns about the world's up axis, in degrees per second.")]
        private float _turnSpeed;
        /// <summary>
        /// The mover's clock.
        /// </summary>
        private ValidatedPhysicsMover _validatedPhysicsMover;
        /// <summary>
        /// The position the mover was placed at, which the waypoints are offsets from.
        /// </summary>
        private Vector3 _originalPosition;
        /// <summary>
        /// The rotation the mover was placed at, which it turns from.
        /// </summary>
        private Quaternion _originalRotation;
        /// <summary>
        /// The length of each leg of the path, from waypoint i to the next, including the closing leg when looping.
        /// </summary>
        private float[] _legLengths;
        /// <summary>
        /// The length of one trip along the path, the sum of <see cref="_legLengths"/>.
        /// </summary>
        private float _pathLength;

        /// <inheritdoc/>
        public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
        {
            float simulationTime = _validatedPhysicsMover.SimulationTime;

            goalPosition = _originalPosition + EvaluatePath(_waypoints, _legLengths, _pathLength, simulationTime * _speed, _loopEnabled);
            goalRotation = Quaternion.AngleAxis(_turnSpeed * simulationTime, Vector3.up) * _originalRotation;
        }

        /// <summary>
        /// Returns the offset a path reaches after travelling a distance along it.
        /// </summary>
        /// <param name="waypoints">The points the path runs through.</param>
        /// <param name="legLengths">The length of each leg, from waypoint i to the next, the closing leg included when looping.</param>
        /// <param name="pathLength">The length of one trip, the sum of <paramref name="legLengths"/>.</param>
        /// <param name="distance">How far along the path has been travelled.</param>
        /// <param name="isLooping">True when the path runs from its last waypoint back to its first; false when it runs back the way it came.</param>
        /// <returns>The offset reached.</returns>
        /// <remarks>A pure function of its arguments, so every peer and every replayed tick gets the same answer from the same distance.</remarks>
        public static Vector3 EvaluatePath(Vector3[] waypoints, float[] legLengths, float pathLength, float distance, bool isLooping)
        {
            if (pathLength <= 0f)
                return waypoints.Length == 0 ? Vector3.zero : waypoints[0];

            // Ping-pong covers the trip there and back, so its cycle is twice the trip.
            float cycleLength = isLooping ? pathLength : pathLength * 2f;
            float cycleDistance = Mathf.Repeat(distance, cycleLength);

            if (cycleDistance > pathLength)
                cycleDistance = cycleLength - cycleDistance;

            for (int legIndex = 0; legIndex < legLengths.Length; legIndex++)
            {
                float legLength = legLengths[legIndex];

                if (cycleDistance <= legLength || legIndex == legLengths.Length - 1)
                {
                    Vector3 legStart = waypoints[legIndex];
                    Vector3 legEnd = waypoints[(legIndex + 1) % waypoints.Length];

                    return legLength <= 0f ? legStart : Vector3.Lerp(legStart, legEnd, cycleDistance / legLength);
                }

                cycleDistance -= legLength;
            }

            return waypoints[0];
        }

        /// <summary>
        /// Measures the legs of a path.
        /// </summary>
        /// <param name="waypoints">The points the path runs through.</param>
        /// <param name="isLooping">True to include the closing leg from the last waypoint back to the first.</param>
        /// <param name="pathLength">The length of one trip.</param>
        /// <returns>The length of each leg.</returns>
        public static float[] MeasureLegs(Vector3[] waypoints, bool isLooping, out float pathLength)
        {
            int legCount = waypoints.Length < 2 ? 0 : isLooping ? waypoints.Length : waypoints.Length - 1;
            float[] legLengths = new float[legCount];
            pathLength = 0f;

            for (int legIndex = 0; legIndex < legCount; legIndex++)
            {
                legLengths[legIndex] = Vector3.Distance(waypoints[legIndex], waypoints[(legIndex + 1) % waypoints.Length]);
                pathLength += legLengths[legIndex];
            }

            return legLengths;
        }

        /// <summary>
        /// Records the pose the mover was placed at, before the network can move it, measures the path and becomes the mover's controller.
        /// </summary>
        private void Awake()
        {
            _validatedPhysicsMover = GetComponent<ValidatedPhysicsMover>();
            transform.GetPositionAndRotation(out _originalPosition, out _originalRotation);
            _legLengths = MeasureLegs(_waypoints, _loopEnabled, out _pathLength);
            GetComponent<PhysicsMover>().MoverController = this;
        }
    }
}
