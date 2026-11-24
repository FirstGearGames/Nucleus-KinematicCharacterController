using System.Collections;
using Nucleus.Integrations.KinematicCharacterController.Demo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nucleus.Integrations.KinematicCharacterController.Tests
{
    /// <summary>
    /// The demo reads input under whichever active input handling the project uses, so a project that turned the legacy input class off
    /// still runs it.
    /// </summary>
    /// <remarks>
    /// Reading an input class the project has turned off throws on every read, and an exception logged during a test fails it, so these
    /// fail in a project set to the Input System alone if any demo script reads the legacy class.
    /// </remarks>
    public class DemoInputPlayTests
    {
        /// <summary>
        /// The demo scene, which the project's build settings must list.
        /// </summary>
        private const string SceneName = "NetworkKccDemo";
        /// <summary>
        /// The frames the scene runs for, enough for every script that reads input each frame to have read it.
        /// </summary>
        private const int FrameCount = 10;

        /// <summary>
        /// Every read <see cref="DemoInput"/> offers answers without throwing, and with no input when nothing is pressed.
        /// </summary>
        [Test]
        public void EveryRead_AnswersUnderTheProjectsInputHandling()
        {
            Assert.AreEqual(0f, DemoInput.ReadMoveForward());
            Assert.AreEqual(0f, DemoInput.ReadMoveRight());
            Assert.IsFalse(DemoInput.IsCrouchHeld());
            Assert.IsFalse(DemoInput.WasJumpPressed());
            Assert.IsFalse(DemoInput.WasLaunchPressed());
            Assert.IsFalse(DemoInput.WasClickPressed());
            Assert.AreEqual(Vector2.zero, DemoInput.ReadLook());
            Assert.AreEqual(0f, DemoInput.ReadScroll());
        }

        /// <summary>
        /// The demo scene runs with its local player reading input and nothing started, and no script in it reads an input class the
        /// project has turned off.
        /// </summary>
        /// <returns>The enumerator the test runner steps.</returns>
        [UnityTest]
        public IEnumerator DemoScene_RunsUnderTheProjectsInputHandling()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);

            // The player reads input only once it holds a character, so it is switched on by hand.
            Object.FindAnyObjectByType<DemoPlayer>().enabled = true;

            for (int frameIndex = 0; frameIndex < FrameCount; frameIndex++)
                yield return null;
        }
    }
}
