// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meta.XR.Experimental.ShaderPrewarmer
{
    [CreateAssetMenu(fileName = "ShaderPrewarmerConfig", menuName = "ScriptableObjects/ShaderPrewarmerConfig")]
    public class ShaderPrewarmerConfig : ScriptableObject
    {
        [SerializeField]
        private List<GameObject> prewarmPrefabs = new();
        public List<GameObject> PrewarmPrefabs => prewarmPrefabs;

        [SerializeField]
        private List<Material> prewarmMaterials = new();
        public List<Material> PrewarmMaterials => prewarmMaterials;

        [SerializeField]
        private List<Shader> prewarmShaders = new();
        public List<Shader> PrewarmShaders => prewarmShaders;

        [SerializeField]
        private List<ShaderToKeywordsList> prewarmShaderKeywords = new();
        public List<ShaderToKeywordsList> PrewarmShaderKeywords => prewarmShaderKeywords;

        [SerializeField]
        private List<Mesh> prewarmSeedMeshes = new();
        public List<Mesh> PrewarmSeedMeshes => prewarmSeedMeshes;

        [SerializeField]
        private List<GameObject> prewarmSeedMeshRendererObjs = new();
        public List<GameObject> PrewarmSeedMeshRendererObjs => prewarmSeedMeshRendererObjs;

        [Tooltip("System will use subset of and re-transform configured lights to trigger shader variants of prewarm objects")]
        [SerializeField]
        private List<Light> prewarmLights = new();
        public List<Light> PrewarmLights => prewarmLights;

        public void ClearShaderPrewarmerSetupData()
        {
            ShaderPrewarmerSetupData.shaderKeywordsList.Clear();
        }

        public void SetupValidShaderKeywordCombinations()
        {
            prewarmShaderKeywords.Clear();

            var shaderKeywordsList = ShaderPrewarmerSetupData.shaderKeywordsList;

            var shaderToKeywordsListMap = new Dictionary<string, List<ShaderKeyword[]>>();
            var shaderToKeywordsSetMap = new Dictionary<string, HashSet<string>>();
            foreach (var keywords in shaderKeywordsList)
            {
                var shader = keywords.shader;
                if (!shaderToKeywordsListMap.ContainsKey(shader))
                {
                    shaderToKeywordsListMap[shader] = new();
                    shaderToKeywordsSetMap[shader] = new();
                }
                var keywordLists = shaderToKeywordsListMap[shader];
                var keywordSets = shaderToKeywordsSetMap[shader];
                foreach (var ks in keywords.keywordsList)
                {
                    var newKeywordsStr = string.Join(" | ", ks.OrderBy(k => k.ToString()));
                    if (keywordSets.Contains(newKeywordsStr))
                        continue;
                    keywordLists.Add(ks);
                    keywordSets.Add(newKeywordsStr);
                }
            }

            // Only setup the shader and its keywords for the shaders we setup in prewarmer config
            for (var i = 0; i < prewarmShaders.Count; i++)
            {
                var shader = prewarmShaders[i];
                var shaderName = shader.name;
                if (!shaderToKeywordsListMap.ContainsKey(shaderName))
                {
                    Debug.LogWarning($"Can not find {shaderName} from {nameof(ShaderPrewarmerSetupData)}, " +
                        $"maybe the shader is not properly included in your scene or build settings");
                    continue;
                }
                prewarmShaderKeywords.Add(new ShaderToKeywordsList
                {
                    shader = shader,
                    keywordsList = shaderToKeywordsListMap[shaderName]
                    .Select(keywords => new Keywords
                    {
                        keywords = keywords
                        .Select(k => k.name)
                        // Need sorting as keywords not guaranteed to follow alphabetical order on gathering
                        .OrderBy(name => name)
                        .ToArray()
                    })
                    .ToList(),
                });
            }
            Debug.Log("Finished setting up valid keyword combinations");
        }

        public void DebugShaderPrewarmerData()
        {
            var shaderKeywordsList = ShaderPrewarmerSetupData.shaderKeywordsList;
            for (int i = 0; i < shaderKeywordsList.Count; i++)
            {
                var pair = shaderKeywordsList[i];
                var shaderName = pair.shader;
                var keywordsList = pair.keywordsList;
                Debug.Log($"Shader \"{shaderName}\" has the following valid keyword combinations:");
                for (int j = 0; j < keywordsList.Count; j++)
                {
                    var keywords = keywordsList[j];
                    Debug.Log($"Shader keywords: {string.Join(" ", keywords)}");
                }
            }
        }
    }
}
