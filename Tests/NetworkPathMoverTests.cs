using Nucleus.Integrations.KinematicCharacterController.Demo;
using NUnit.Framework;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Tests
{
    /// <summary>
    /// Checks the path a <see cref="NetworkPathMover"/> follows: where it is after a distance, at the turnarounds and around a loop.
    /// </summary>
    public class NetworkPathMoverTests
    {
        /// <summary>
        /// A two-point path runs there and back: halfway out, at the far end, halfway back, and home after a full cycle.
        /// </summary>
        [Test]
        public void PingPongPath_RunsThereAndBack()
        {
            Vector3[] waypoints = { Vector3.zero, new(0f, 0f, 8f) };
            float[] legLengths = NetworkPathMover.MeasureLegs(waypoints, isLooping: false, out float pathLength);

            Assert.AreEqual(8f, pathLength);
            AssertNear(new(0f, 0f, 4f), NetworkPathMover.EvaluatePath(waypoints, legLengths, pathLength, distance: 4f, isLooping: false));
            AssertNear(new(0f, 0f, 8f), NetworkPathMover.EvaluatePath(waypoints, legLengths, pathLength, distance: 8f, isLooping: false));
            AssertNear(new(0f, 0f, 2f), NetworkPathMover.EvaluatePath(waypoints, legLengths, pathLength, distance: 14f, isLooping: false));
            AssertNear(Vector3.zero, NetworkPathMover.EvaluatePath(waypoints, legLengths, pathLength, distance: 16f, isLooping: false));
        }

        /// <summary>
        /// A looping triangle takes its closing leg back to the start rather than retracing its steps.
        /// </summary>
        [Test]
        public void LoopingPath_ClosesBackToTheStart()
        {
            Vector3[] waypoints = { Vector3.zero, new(4f, 0f, 0f), new(4f, 0f, 3f) };
            float[] legLengths = NetworkPathMover.MeasureLegs(waypoints, isLooping: true, out float pathLength);

            Assert.AreEqual(12f, pathLength);
            AssertNear(new(4f, 0f, 1f), NetworkPathMover.EvaluatePath(waypoints, legLengths, pathLength, distance: 5f, isLooping: true));
            AssertNear(new(2f, 0f, 1.5f), NetworkPathMover.EvaluatePath(waypoints, legLengths, pathLength, distance: 9.5f, isLooping: true));
            AssertNear(new(4f, 0f, 0f), NetworkPathMover.EvaluatePath(waypoints, legLengths, pathLength, distance: 16f, isLooping: true));
        }

        /// <summary>
        /// A single waypoint is a mover that only turns, and stays where it was placed.
        /// </summary>
        [Test]
        public void SingleWaypoint_StaysPut()
        {
            Vector3[] waypoints = { Vector3.zero };
            float[] legLengths = NetworkPathMover.MeasureLegs(waypoints, isLooping: false, out float pathLength);

            AssertNear(Vector3.zero, NetworkPathMover.EvaluatePath(waypoints, legLengths, pathLength, distance: 123f, isLooping: false));
        }

        /// <summary>
        /// Asserts two positions are within a millimetre.
        /// </summary>
        /// <param name="expected">The expected position.</param>
        /// <param name="actual">The position found.</param>
        private static void AssertNear(Vector3 expected, Vector3 actual) => Assert.LessOrEqual(Vector3.Distance(expected, actual), 0.001f, $"Expected [{expected}] but was [{actual}].");
    }
}
