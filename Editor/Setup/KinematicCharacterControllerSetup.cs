using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Editor
{
    /// <summary>
    /// Puts the imported Kinematic Character Controller in its own assembly, which the rest of this package compiles against, and sets
    /// the define that switches the rest of this package on.
    /// </summary>
    /// <remarks>
    /// KCC ships with no assembly definitions, so its scripts compile into Assembly-CSharp, which no package can reference. Until KCC
    /// has its own assembly, every other assembly in this package is held off by <see cref="Define"/>, so a project without KCC still
    /// compiles. This assembly references nothing from KCC, so it always compiles and can do the setup.
    /// </remarks>
    [InitializeOnLoad]
    internal static class KinematicCharacterControllerSetup
    {
        /// <summary>
        /// The define every other assembly in this package requires.
        /// </summary>
        private const string Define = "NUCLEUS_KCC";
        /// <summary>
        /// The assembly the rest of this package references KCC's runtime scripts by.
        /// </summary>
        private const string AssemblyName = "KinematicCharacterController";
        /// <summary>
        /// The assembly KCC's editor scripts are given.
        /// </summary>
        private const string EditorAssemblyName = "KinematicCharacterController.Editor";
        /// <summary>
        /// The KCC script whose folder is KCC's Core.
        /// </summary>
        private const string MotorScriptFileName = "KinematicCharacterMotor.cs";

        /// <summary>
        /// Runs <see cref="SetUp"/> once the editor has finished loading, on every domain reload, so KCC imported later or a build
        /// target switched to is set up too.
        /// </summary>
        static KinematicCharacterControllerSetup() => EditorApplication.delayCall += SetUp;

        /// <summary>
        /// Gives KCC's Core its assemblies when it has none, then sets <see cref="Define"/>. Does nothing when KCC is not imported.
        /// </summary>
        private static void SetUp()
        {
            if (!TryGetMotorScriptPath(out string motorScriptPath))
                return;

            string assemblyDefinitionPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromScriptPath(motorScriptPath);

            if (string.IsNullOrEmpty(assemblyDefinitionPath))
            {
                string coreFolderPath = Path.GetDirectoryName(motorScriptPath).Replace('\\', '/');
                WriteAssemblyDefinitions(coreFolderPath);
            }
            else
            {
                string assemblyName = JsonUtility.FromJson<AssemblyDefinitionName>(File.ReadAllText(assemblyDefinitionPath)).name;

                // An assembly the project gave KCC under another name cannot be referenced, because this package names its reference.
                if (assemblyName != AssemblyName)
                {
                    Debug.LogError($"Nucleus: Kinematic Character Controller compiles into assembly [{assemblyName}], set by [{assemblyDefinitionPath}]. Nucleus Kinematic Character Controller references it as [{AssemblyName}], so rename that assembly definition to [{AssemblyName}].");

                    return;
                }
            }

            AddDefine(NamedBuildTarget.Standalone);
            AddDefine(NamedBuildTarget.Server);
            AddDefine(NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)));
        }

        /// <summary>
        /// Finds KCC's motor script.
        /// </summary>
        /// <param name="motorScriptPath">The script's asset path, when found.</param>
        /// <returns>True when KCC is imported.</returns>
        private static bool TryGetMotorScriptPath(out string motorScriptPath)
        {
            foreach (string guid in AssetDatabase.FindAssets("KinematicCharacterMotor t:MonoScript"))
            {
                motorScriptPath = AssetDatabase.GUIDToAssetPath(guid);

                if (Path.GetFileName(motorScriptPath) == MotorScriptFileName)
                    return true;
            }

            motorScriptPath = null;

            return false;
        }

        /// <summary>
        /// Writes the runtime assembly definition into KCC's Core, and the editor one into Core's Editor folder when it has one.
        /// </summary>
        /// <param name="coreFolderPath">The asset path of KCC's Core folder.</param>
        private static void WriteAssemblyDefinitions(string coreFolderPath)
        {
            File.WriteAllText($"{coreFolderPath}/{AssemblyName}.asmdef", $"{{\n    \"name\": \"{AssemblyName}\",\n    \"autoReferenced\": true\n}}\n");

            string editorFolderPath = $"{coreFolderPath}/Editor";

            if (Directory.Exists(editorFolderPath))
                File.WriteAllText($"{editorFolderPath}/{EditorAssemblyName}.asmdef", $"{{\n    \"name\": \"{EditorAssemblyName}\",\n    \"references\": [\"{AssemblyName}\"],\n    \"includePlatforms\": [\"Editor\"]\n}}\n");

            AssetDatabase.Refresh();
            Debug.Log($"Nucleus: gave Kinematic Character Controller its own assembly [{AssemblyName}] in [{coreFolderPath}], so Nucleus Kinematic Character Controller can compile against it.");
        }

        /// <summary>
        /// Adds <see cref="Define"/> to a build target's scripting defines when it is missing.
        /// </summary>
        /// <param name="namedBuildTarget">The build target.</param>
        private static void AddDefine(NamedBuildTarget namedBuildTarget)
        {
            PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget, out string[] defines);

            if (Array.IndexOf(defines, Define) >= 0)
                return;

            string[] newDefines = new string[defines.Length + 1];
            defines.CopyTo(newDefines, 0);
            newDefines[defines.Length] = Define;
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newDefines);
            // Saved now, so the define survives an editor that closes without saving the project.
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The one field read from an assembly definition file.
        /// </summary>
        [Serializable]
        private sealed class AssemblyDefinitionName
        {
            /// <summary>
            /// The assembly's name.
            /// </summary>
            /// <remarks>
            /// Lowercase because <see cref="JsonUtility"/> matches fields to the file's keys by name.
            /// </remarks>
            public string name;
        }
    }
}
