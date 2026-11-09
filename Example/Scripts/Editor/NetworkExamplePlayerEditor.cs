using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="NetworkExamplePlayer"/>: the camera it drives, and while playing, the character this peer
    /// controls.
    /// </summary>
    [CustomEditor(typeof(NetworkExamplePlayer))]
    public class NetworkExamplePlayerEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        /// <remarks>The character is handed over when the player spawns, so the readout redraws itself while playing.</remarks>
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);
            NucleusInspectorGUI.DrawPropertiesWithoutScript(serializedObject);

            if (!Application.isPlaying)
                return;

            NetworkExampleCharacter networkExampleCharacter = ((NetworkExamplePlayer)target).NetworkExampleCharacter;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Controls", networkExampleCharacter == null ? "Nothing yet" : networkExampleCharacter.name);
        }
    }
}
