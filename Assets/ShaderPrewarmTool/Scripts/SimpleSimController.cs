// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using UnityEngine;

namespace Meta.XR.Experimental.ShaderPrewarmer
{
    public class SimpleSimController : MonoBehaviour
    {
        [SerializeField]
        private float spawnInterval = 2.0f;
        private float lastSpawnTime = 0;
        private int spawnIndex = 0;

        [SerializeField]
        private List<GameObject> prefabs = new();

        private bool gamesimSpawningStarted = false;

        void Start()
        {
            if (!gamesimSpawningStarted)
            {
                // This is just an example, replace with your own game logic
                Debug.Log($"Trigger {nameof(StartGameSimSpawning)}");
                StartGameSimSpawning();
            }
            else
            {
                Debug.Log($"{nameof(StartGameSimSpawning)} was already triggered");
            }
        }

        void Update()
        {
            if (gamesimSpawningStarted && Time.time - lastSpawnTime > spawnInterval && spawnIndex < prefabs.Count)
            {
                lastSpawnTime = Time.time;
                var obj = prefabs[spawnIndex++];
                Debug.Log($"Game sim spawn object: {obj.name}");
                var go = Instantiate(obj);
                go.transform.position = new Vector3(0, 0, 15.0f);
                go.SetActive(true);
            }
        }

        private void StartGameSimSpawning()
        {
            gamesimSpawningStarted = true;
        }
    }
}
