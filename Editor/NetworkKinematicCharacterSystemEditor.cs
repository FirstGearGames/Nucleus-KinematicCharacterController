using Nucleus.Integrations.Unity.Editor;
using Nucleus.Managers.NetworkLoop;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="NetworkKinematicCharacterSystem"/>. It has nothing to configure, so it reports what the latest
    /// simulation stepped while playing: the tick, how many motors and movers took part, and how many replayed ticks have been stepped.
    /// </summary>
    /// <remarks>
    /// On the server every character takes part in every tick. On a client only the character it controls and the movers it predicts do,
    /// so a client reporting more motors than its own is predicting other players, and a replayed tick count that climbs while standing
    /// still points at a simulation the server and the client run differently.
    /// </remarks>
    [CustomEditor(typeof(NetworkKinematicCharacterSystem))]
    public class NetworkKinematicCharacterSystemEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        /// <remarks>The counts change every tick, so the inspector redraws itself while playing rather than waiting for a mouse move.</remarks>
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("What each tick steps appears while playing.");

                return;
            }

            NetworkKinematicCharacterSystem networkKinematicCharacterSystem = (NetworkKinematicCharacterSystem)target;

            using (new EditorGUI.DisabledScope(disabled: true))
            {
                EditorGUILayout.TextField("Last Simulated Tick", networkKinematicCharacterSystem.LastSimulatedTick is NetworkLoopManager.UnsetTick ? "None yet" : networkKinematicCharacterSystem.LastSimulatedTick.ToString());
                EditorGUILayout.IntField("Motors Stepped", networkKinematicCharacterSystem.LastSimulatedMotorCount);
                EditorGUILayout.IntField("Movers Stepped", networkKinematicCharacterSystem.LastSimulatedMoverCount);
                EditorGUILayout.LongField("Replayed Ticks", networkKinematicCharacterSystem.ReplayedTickCount);
            }
        }
    }
}
