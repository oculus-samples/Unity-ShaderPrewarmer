// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Meta.XR.Experimental.ShaderPrewarmer
{
    [CustomEditor(typeof(ShaderPrewarmer))]
    public class ShaderPrewarmerEditor : UnityEditor.Editor
    {
        ShaderPrewarmer shaderPrewarmer;

        private bool showAdvancedDebugOptions = false;

        private void OnEnable()
        {
            shaderPrewarmer = (ShaderPrewarmer)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ShaderPrewarmer shaderPrewarmer = (ShaderPrewarmer)target;

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            if (GUILayout.Button($"Auto setup shader keywords for {nameof(ShaderPrewarmer)}"))
            {
                AutoBuildAndSetupShaderKeywords();
            }

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            showAdvancedDebugOptions = EditorGUILayout.Foldout(showAdvancedDebugOptions, "Advanced Debug Options");

            if (showAdvancedDebugOptions)
            {
                if (GUILayout.Button($"Clear {nameof(ShaderPrewarmerSetupData)}"))
                {
                    shaderPrewarmer.ClearShaderPrewarmerSetupData();
                }

                if (GUILayout.Button("Calculate valid shader keyword combinations"))
                {
                    BuildProjectToGatherKeywordCombinations();
                }

                if (GUILayout.Button($"Debug Print {nameof(ShaderPrewarmerSetupData)}"))
                {
                    shaderPrewarmer.DebugShaderPrewarmerData();
                }

                if (GUILayout.Button($"Config keyword combinations for {nameof(ShaderPrewarmer)}"))
                {
                    Undo.RecordObject(shaderPrewarmer.PrewarmConfig, "Setup Shader Keywords");
                    shaderPrewarmer.SetupValidShaderKeywordCombinations();
                    EditorUtility.SetDirty(shaderPrewarmer.PrewarmConfig);
                    AssetDatabase.SaveAssets();
                }

                if (GUILayout.Button($"Debug Print {nameof(ShaderPrewarmer)}'s keywords config"))
                {
                    shaderPrewarmer.DebugPrewarmShaderKeywords();
                }
            }
        }

        private void AutoBuildAndSetupShaderKeywords()
        {
            ShaderPrewarmerSetupData.shaderKeywordsList.Clear();
            BuildProjectToGatherKeywordCombinations();
            Undo.RecordObject(shaderPrewarmer.PrewarmConfig, "Setup Shader Keywords");
            FindObjectOfType<ShaderPrewarmer>()?.SetupValidShaderKeywordCombinations();
            EditorUtility.SetDirty(shaderPrewarmer.PrewarmConfig);
            AssetDatabase.SaveAssets();
        }

        private const string buildFolderPath = "Build/_TempPrewarmBuild";
        private const BuildTarget buildTarget = BuildTarget.Android;
        private const BuildOptions buildOptions =
            BuildOptions.CleanBuildCache;

        private void BuildProjectToGatherKeywordCombinations()
        {
            if (string.IsNullOrEmpty(buildFolderPath))
            {
                return;
            }

            if (Directory.Exists(buildFolderPath))
            {
                Directory.Delete(buildFolderPath, true);
            }

            Directory.CreateDirectory(buildFolderPath);

            string buildPath = Path.Combine(buildFolderPath, "TempPrewarmBuild.apk");
            var scenes = EditorBuildSettings.scenes;

            ShaderPrewarmerSetupData.shouldGatherKeywords = true;
            BuildPipeline.BuildPlayer(
                scenes,
                buildPath,
                buildTarget,
                buildOptions
            );
            ShaderPrewarmerSetupData.shouldGatherKeywords = false;

            if (Directory.Exists(buildFolderPath))
            {
                Directory.Delete(buildFolderPath, true);
            }
        }
    }
}
