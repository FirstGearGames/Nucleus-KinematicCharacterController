using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="NetworkMovingPlatform"/>, its fields grouped by the motion they shape, and while playing, the
    /// mover's clock.
    /// </summary>
    [CustomEditor(typeof(NetworkMovingPlatform))]
    [CanEditMultipleObjects]
    public class NetworkMovingPlatformEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        /// <remarks>The clock advances every tick, so the readout redraws itself while playing.</remarks>
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);

            serializedObject.Update();

            EditorGUILayout.LabelField("Sliding", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_translationAxis"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_translationPeriod"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_translationSpeed"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Turning", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_rotationAxis"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_rotationSpeed"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rocking", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_oscillationAxis"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_oscillationPeriod"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_oscillationSpeed"));

            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Simulation Time", $"{((NetworkMovingPlatform)target).GetComponent<ValidatedPhysicsMover>().SimulationTime:F2} s");
        }
    }
}
