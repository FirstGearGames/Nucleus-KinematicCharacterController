using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="DemoOrbitCamera"/>: the distance, zoom range, look sensitivity, pitch limits and obstruction radius.
    /// </summary>
    [CustomEditor(typeof(DemoOrbitCamera))]
    public class DemoOrbitCameraEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);
            NucleusInspectorGUI.DrawPropertiesWithoutScript(serializedObject);
        }
    }
}
