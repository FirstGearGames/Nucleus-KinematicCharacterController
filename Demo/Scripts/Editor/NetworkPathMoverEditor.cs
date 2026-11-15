using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="NetworkPathMover"/>: the path and how fast it is travelled, the turn rate, and while playing, the
    /// mover's clock.
    /// </summary>
    [CustomEditor(typeof(NetworkPathMover))]
    [CanEditMultipleObjects]
    public class NetworkPathMoverEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        /// <remarks>The clock advances every tick, so the readout redraws itself while playing.</remarks>
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);

            serializedObject.Update();

            EditorGUILayout.LabelField("Path", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_waypoints"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_speed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_loopEnabled"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Turning", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_turnSpeed"));

            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Simulation Time", $"{((NetworkPathMover)target).GetComponent<ValidatedPhysicsMover>().SimulationTime:F2} s");
        }
    }
}
