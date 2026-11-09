using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="NetworkExampleCharacterController"/>, its fields grouped as KCC's example controller groups
    /// them, and while playing, the jump and crouch state a replay puts back.
    /// </summary>
    [CustomEditor(typeof(NetworkExampleCharacterController))]
    [CanEditMultipleObjects]
    public class NetworkExampleCharacterControllerEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        /// <remarks>The state changes every tick, so the readout redraws itself while playing.</remarks>
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);

            serializedObject.Update();

            EditorGUILayout.LabelField("Stable Movement", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxStableMoveSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_stableMovementSharpness"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_orientationSharpness"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_orientationMethod"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Air Movement", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxAirMoveSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_airAccelerationSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_drag"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Jumping", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_jumpWhenSlidingEnabled"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_jumpUpSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_jumpScalableForwardSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_jumpPreGroundingGraceTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_jumpPostGroundingGraceTime"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Orientation and Gravity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_bonusOrientationMethod"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_bonusOrientationSharpness"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_gravity"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Body", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_meshRoot"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_cameraFollowPoint"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_crouchedCapsuleHeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_ignoredColliders"));

            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying || targets.Length > 1)
                return;

            NetworkExampleCharacterControllerState networkExampleCharacterControllerState = ((NetworkExampleCharacterController)target).GetState();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Jump Requested", networkExampleCharacterControllerState.IsJumpRequested.ToString());
            EditorGUILayout.LabelField("Jump Consumed", networkExampleCharacterControllerState.IsJumpConsumed.ToString());
            EditorGUILayout.LabelField("Crouching", networkExampleCharacterControllerState.IsCrouching.ToString());
        }
    }
}
