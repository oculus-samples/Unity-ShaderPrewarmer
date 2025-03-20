// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meta.XR.Experimental.ShaderPrewarmer
{
    public static class Debug
    {
        public const bool ForceDiable = false; // Use this flag to prevent massive logging from prewarmer tool set
        internal static bool Enable = !ForceDiable && (Application.isEditor || UnityEngine.Debug.isDebugBuild);
        internal static string Prefix = $"[{nameof(ShaderPrewarmer)}]: ";
        public static void Log(object message)
        {
            if (Enable)
                UnityEngine.Debug.Log(Prefix + message.ToString());
        }
        public static void LogWarning(object message)
        {
            if (Enable)
                UnityEngine.Debug.LogWarning(Prefix + message.ToString());
        }
        public static void LogError(object message)
        {
            if (Enable)
                UnityEngine.Debug.LogError(Prefix + message.ToString());
        }
    }

    public static class Utils
    {
        /// <summary>
        /// Returns all possible non-empty subsets of the input elements, sorted by increasing subset size.
        /// </summary>
        public static List<List<T>> GetAllNonEmptySubsets<T>(List<T> list)
        {
            List<List<T>> subsets = new List<List<T>>();
            int subsetCount = 1 << list.Count; // 2^n subsets

            for (int i = 1; i < subsetCount; i++) // Start from 1 to exclude the empty subset
            {
                List<T> subset = new List<T>();

                for (int j = 0; j < list.Count; j++)
                {
                    if ((i & (1 << j)) != 0)
                    {
                        subset.Add(list[j]);
                    }
                }

                subsets.Add(subset);
            }

            subsets.Sort((a, b) => a.Count.CompareTo(b.Count));

            return subsets;
        }
    }

    public class ShaderPrewarmerSetupData
    {
        public class ShaderKeywordsPair
        {
            public string shader;
            public List<ShaderKeyword[]> keywordsList = new();
        }
        public static List<ShaderKeywordsPair> shaderKeywordsList = new();
        public static bool shouldGatherKeywords = false;
    }

    [System.Serializable]
    // need this extra class due to unity serialization limitation
    public class Keywords
    {
        [SerializeField]
        public string[] keywords;
    }

    [System.Serializable]
    public class ShaderToKeywordsList
    {
        [SerializeField]
        public Shader shader;
        [SerializeField]
        public List<Keywords> keywordsList = new();
    }
}
