using System.Collections.Generic;
using KinematicCharacterController;
using Nucleus.Integrations.KinematicCharacterController.Demo;
using NUnit.Framework;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Tests
{
    /// <summary>
    /// Checks that a <see cref="DemoCharacterController"/> repeats a move exactly once its own state and its motor's are put
    /// back, which is what a reconcile relies on.
    /// </summary>
    /// <remarks>
    /// KCC is stepped by hand with no network. The replay starts on the tick before a jump is pressed, while the live run has already
    /// jumped and is still in the air, so the replay only makes the same jump when the controller's jump state is put back. The live
    /// tests cannot time a correction that finely across two processes.
    /// </remarks>
    public class DemoCharacterControllerReplayTests
    {
        /// <summary>
        /// The ground the character walks on.
        /// </summary>
        private GameObject _groundGameObject;
        /// <summary>
        /// The character under test.
        /// </summary>
        private GameObject _characterGameObject;
        /// <summary>
        /// The value <see cref="KCCSettings.AutoSimulation"/> held before the test, put back afterwards.
        /// </summary>
        private bool _isAutoSimulationEnabled;
        /// <summary>
        /// The value <see cref="KCCSettings.Interpolate"/> held before the test, put back afterwards.
        /// </summary>
        private bool _isInterpolateEnabled;
        /// <summary>
        /// The seconds each tick covers.
        /// </summary>
        private const float TickDelta = 1f / 30f;
        /// <summary>
        /// The tick jump is pressed on.
        /// </summary>
        private const int JumpTick = 20;
        /// <summary>
        /// The last tick of the move, while the character is still in the air from its jump.
        /// </summary>
        private const int LastTick = 30;
        /// <summary>
        /// The most metres the replay may end from the move it repeats.
        /// </summary>
        private const float ReplayTolerance = 0.0001f;

        /// <summary>
        /// Builds flat ground and a character on it, and stops KCC from stepping on its own.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            KinematicCharacterSystem.EnsureCreation();
            _isAutoSimulationEnabled = KinematicCharacterSystem.Settings.AutoSimulation;
            _isInterpolateEnabled = KinematicCharacterSystem.Settings.Interpolate;
            KinematicCharacterSystem.Settings.AutoSimulation = false;
            KinematicCharacterSystem.Settings.Interpolate = false;

            _groundGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _groundGameObject.transform.SetPositionAndRotation(new(0f, -0.5f, 0f), Quaternion.identity);
            _groundGameObject.transform.localScale = new(50f, 1f, 50f);

            _characterGameObject = new("Character");
            _characterGameObject.AddComponent<KinematicCharacterMotor>();
            _characterGameObject.AddComponent<DemoCharacterController>();

            UnityEngine.Physics.SyncTransforms();
        }

        /// <summary>
        /// Destroys the test's objects and puts KCC's settings back.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_characterGameObject);
            Object.Destroy(_groundGameObject);

            KinematicCharacterSystem.Settings.AutoSimulation = _isAutoSimulationEnabled;
            KinematicCharacterSystem.Settings.Interpolate = _isInterpolateEnabled;
        }

        /// <summary>
        /// Walks and jumps, puts the controller and motor back to the tick before the jump, replays the same input, and requires the
        /// replay to end where the move did.
        /// </summary>
        [Test]
        public void ReplayFromBeforeAJumpEndsWhereTheMoveDid()
        {
            KinematicCharacterMotor kinematicCharacterMotor = _characterGameObject.GetComponent<KinematicCharacterMotor>();
            DemoCharacterController demoCharacterController = _characterGameObject.GetComponent<DemoCharacterController>();
            List<KinematicCharacterMotor> kinematicCharacterMotors = new() { kinematicCharacterMotor };
            List<PhysicsMover> physicsMovers = new();
            KinematicCharacterMotorState[] kinematicCharacterMotorStates = new KinematicCharacterMotorState[LastTick + 1];
            DemoCharacterControllerState[] demoCharacterControllerStates = new DemoCharacterControllerState[LastTick + 1];

            for (int tick = 0; tick <= LastTick; tick++)
            {
                Step(demoCharacterController, kinematicCharacterMotors, physicsMovers, tick);

                kinematicCharacterMotorStates[tick] = kinematicCharacterMotor.GetState();
                demoCharacterControllerStates[tick] = demoCharacterController.GetState();
            }

            Vector3 lastPosition = kinematicCharacterMotor.TransientPosition;

            Assert.IsTrue(demoCharacterControllerStates[JumpTick].IsJumpConsumed && !demoCharacterControllerStates[JumpTick - 1].IsJumpConsumed, "The character did not jump on the tick jump was pressed.");
            Assert.IsFalse(kinematicCharacterMotorStates[LastTick].GroundingStatus.IsStableOnGround, "The character landed before the move ended, so the replay would not cross a jump in progress.");

            const int ReplayStartTick = JumpTick - 1;
            kinematicCharacterMotor.ApplyState(kinematicCharacterMotorStates[ReplayStartTick]);
            demoCharacterController.ApplyState(demoCharacterControllerStates[ReplayStartTick]);

            for (int tick = ReplayStartTick + 1; tick <= LastTick; tick++)
                Step(demoCharacterController, kinematicCharacterMotors, physicsMovers, tick);

            Assert.LessOrEqual(Vector3.Distance(kinematicCharacterMotor.TransientPosition, lastPosition), ReplayTolerance, "The replay did not end where the move did, so the controller's state was not put back.");
        }

        /// <summary>
        /// Hands the controller one tick's input, walking forward and pressing jump on <see cref="JumpTick"/>, then steps KCC once.
        /// </summary>
        /// <param name="demoCharacterController">The controller to hand the input to.</param>
        /// <param name="kinematicCharacterMotors">The motors KCC steps.</param>
        /// <param name="physicsMovers">The movers KCC steps, of which there are none.</param>
        /// <param name="tick">The tick being stepped.</param>
        private static void Step(DemoCharacterController demoCharacterController, List<KinematicCharacterMotor> kinematicCharacterMotors, List<PhysicsMover> physicsMovers, int tick)
        {
            DemoCharacterInputs demoCharacterInputs = new()
            {
                MoveAxisForward = 1f,
                IsJumpPressed = tick == JumpTick
            };

            demoCharacterController.SetInputs(in demoCharacterInputs);
            KinematicCharacterSystem.Simulate(TickDelta, kinematicCharacterMotors, physicsMovers);
        }
    }
}
