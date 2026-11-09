using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="NetworkTeleporter"/>: where it sends a character, its zone and whether it stands on the planet,
    /// with a warning for a zone that cannot work.
    /// </summary>
    [CustomEditor(typeof(NetworkTeleporter))]
    [CanEditMultipleObjects]
    public class NetworkTeleporterEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);
            NucleusInspectorGUI.DrawPropertiesWithoutScript(serializedObject);

            if (serializedObject.FindProperty("_zoneId").intValue == NetworkTeleporter.UnsetZoneId)
                EditorGUILayout.HelpBox("A zone of 0 means no teleporter, so a character standing here is never sent anywhere. Give each teleporter in the scene its own zone from 1 to 255.", MessageType.Warning);
        }
    }
}
