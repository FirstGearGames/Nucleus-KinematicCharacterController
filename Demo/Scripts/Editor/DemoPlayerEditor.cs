using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="DemoPlayer"/>: the camera it drives, and while playing, the character this peer controls.
    /// </summary>
    [CustomEditor(typeof(DemoPlayer))]
    public class DemoPlayerEditor : UnityEditor.Editor
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

            NetworkDemoCharacter networkDemoCharacter = ((DemoPlayer)target).NetworkDemoCharacter;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Controls", networkDemoCharacter == null ? "Nothing yet" : networkDemoCharacter.name);
        }
    }
}
