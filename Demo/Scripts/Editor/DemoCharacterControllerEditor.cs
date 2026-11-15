using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="DemoCharacterController"/>: the walking, jumping and crouching tuning and the graphic and camera points, and while playing, the state a replay restores.
    /// </summary>
    [CustomEditor(typeof(DemoCharacterController))]
    public class DemoCharacterControllerEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        /// <remarks>The readout changes every tick, so the inspector redraws itself while playing.</remarks>
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);
            NucleusInspectorGUI.DrawPropertiesWithoutScript(serializedObject);

            if (!Application.isPlaying)
                return;

            DemoCharacterControllerState demoCharacterControllerState = ((DemoCharacterController)target).GetState();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Crouching", demoCharacterControllerState.IsCrouching ? "Yes" : "No");
            EditorGUILayout.LabelField("Jump Consumed", demoCharacterControllerState.IsJumpConsumed ? "Yes" : "No");
            EditorGUILayout.LabelField("Jump Buffer", $"{demoCharacterControllerState.JumpBufferRemaining:F3} s");
            EditorGUILayout.LabelField("Coyote Time", $"{demoCharacterControllerState.CoyoteRemaining:F3} s");
        }
    }
}
