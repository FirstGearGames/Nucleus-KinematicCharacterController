using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="NetworkPlayableMover"/>: its timeline and speed, and while playing, the mover's clock.
    /// </summary>
    [CustomEditor(typeof(NetworkPlayableMover))]
    public class NetworkPlayableMoverEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        /// <remarks>The clock advances every tick, so the readout redraws itself while playing.</remarks>
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);
            NucleusInspectorGUI.DrawPropertiesWithoutScript(serializedObject);

            if (!Application.isPlaying)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Simulation Time", $"{((NetworkPlayableMover)target).GetComponent<ValidatedPhysicsMover>().SimulationTime:F2} s");
        }
    }
}
