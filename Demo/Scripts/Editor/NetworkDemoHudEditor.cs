using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="NetworkDemoHud"/>: the address its client connects to.
    /// </summary>
    [CustomEditor(typeof(NetworkDemoHud))]
    public class NetworkDemoHudEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);
            NucleusInspectorGUI.DrawPropertiesWithoutScript(serializedObject);
        }
    }
}
