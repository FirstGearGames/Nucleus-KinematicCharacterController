using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="NetworkPlaygroundHud"/>: the address its client connects to.
    /// </summary>
    [CustomEditor(typeof(NetworkPlaygroundHud))]
    public class NetworkPlaygroundHudEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);
            NucleusInspectorGUI.DrawPropertiesWithoutScript(serializedObject);
        }
    }
}
