// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meta.XR.Experimental.ShaderPrewarmer
{
    class ShaderDebugBuildProcessor : IPreprocessShaders
    {
        public int callbackOrder { get { return 0; } }

        public void OnProcessShader(
            Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (ShaderPrewarmerSetupData.shouldGatherKeywords)
            {
                var shaderKeywords = new ShaderPrewarmerSetupData.ShaderKeywordsPair
                {
                    shader = shader.name
                };
                foreach (var d in data)
                {
                    var keywords = d.shaderKeywordSet.GetShaderKeywords();
                    if (keywords.Length < 1)
                    {
                        continue;
                    }
                    if (shaderKeywords.keywordsList.Any(list => list.SequenceEqual(keywords)))
                    {
                        continue;
                    }
                    var keywordsCopy = new ShaderKeyword[keywords.Length];
                    Array.Copy(keywords, keywordsCopy, keywords.Length);
                    shaderKeywords.keywordsList.Add(keywordsCopy);
                }
                ShaderPrewarmerSetupData.shaderKeywordsList.Add(shaderKeywords);
            }
        }
    }
}
