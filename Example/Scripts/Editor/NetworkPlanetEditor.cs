using Nucleus.Integrations.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example.Editor
{
    /// <summary>
    /// Draws the inspector for <see cref="NetworkPlanet"/>: how strongly it pulls the characters on it, and how it turns.
    /// </summary>
    [CustomEditor(typeof(NetworkPlanet))]
    public class NetworkPlanetEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            NucleusInspectorGUI.DrawScriptField(serializedObject);
            NucleusInspectorGUI.DrawPropertiesWithoutScript(serializedObject);
        }
    }
}
