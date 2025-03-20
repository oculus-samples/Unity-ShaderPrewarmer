// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace Meta.XR.Experimental.ShaderPrewarmer
{
    public class ShaderPrewarmer : MonoBehaviour
    {
        public enum MeshSeedType
        {
            None,
            Mesh,
            MeshRenderer,
            All,
        }

        [SerializeField]
        private bool dontDestroyOnLoad = false;

        [Header("PREWARM SETTINGS")]
        [Tooltip("This is usually a transform who gameobject has a camera")]
        [SerializeField]
        private Transform fixToTransform = null;
        [SerializeField]
        private int maxObjPerSpawn = 50;
        [SerializeField]
        private bool useSourcePrefabs; // not recommended to be used yet
        [SerializeField]
        private bool useSourceMaterials;
        [SerializeField]
        private bool useSourceShaders;
        [Tooltip("The minimum interval for spawning a prewarm render object batch, " +
            "but it is not guaranteed to be reached if you have many prewarming lights or other kinds of shader variant influencers")]
        [SerializeField]
        private float minSpawnInterval = 0.015f;
        [Tooltip("The minimum interval for despawning prewarm render object batches, " +
            "but it is not guaranteed to be reached if you have many prewarming lights or other kinds of shader variant influencers")]
        [SerializeField]
        private float minDespawnInterval = 0.015f;

        private float lastSpawnTime = 0;
        private float lastDepawnTime = 0;

        [SerializeField]
        private MeshSeedType meshSeedType = MeshSeedType.None;

        [SerializeField]
        private int loadNextSceneIndex;

        [Header("ASSET CONFIGURATION")]
        [SerializeField]
        private ShaderPrewarmerConfig prewarmConfig = null;
        public ShaderPrewarmerConfig PrewarmConfig => prewarmConfig;

        private List<GameObject> prewarmPrefabs = new();
        private List<Material> prewarmMaterials = new();
        private List<Shader> prewarmShaders = new();
        private List<ShaderToKeywordsList> prewarmShaderKeywords = new();
        private List<Mesh> prewarmSeedMeshes = new();
        private List<GameObject> prewarmSeedMeshRendererObjs = new();
        private List<Light> prewarmLights = new();

        private List<Material> prewarmShaderGenMaterials = new();
        private List<MeshRenderer> prewarmSeedMeshRenderers = new();
        private List<List<Light>> prewarmLightSubsets = new();

        private bool prewarmingStarted = false;

        private Queue<GameObject> prewarmCleanupQueue = new();

        private DebugControls debugControls;

        private Queue<GameObject> prewarmPrefabQueue = new();
        private Queue<(Mesh, Material)> prewarmMeshMaterialQueue = new();
        private Queue<(MeshRenderer, Material)> prewarmMeshRendererMaterialQueue = new();

        Material lastPrewarmMaterial = null;

        // x - half width of spawn plane
        // y - half height of spawn plane
        // z - distance away from render camera
        private Vector3 spawnSpans = new Vector3(10, 10, 30);

        private void OnSimOculusButtonA()
        {
            OnOculusControllerButtonAPressed();
        }

        private void OnSimOculusButtonB()
        {
            LoadNextScene();
        }

        private void OnOculusControllerButtonAPressed()
        {
            // Replace with your own triggering logic to start the prewarming at the moment you need
            if (!prewarmingStarted)
            {
                PreparePrewarming();
                Debug.Log($"Calling {nameof(StartPrewarmingSpawning)}");
                StartPrewarmingSpawning();
            }
            else
            {
                Debug.Log($"{nameof(StartPrewarmingSpawning)} was already fired");
            }
        }

        private void Start()
        {
            prewarmPrefabs = prewarmConfig.PrewarmPrefabs.Where(item => item != null).ToList();
            prewarmMaterials = prewarmConfig.PrewarmMaterials.Where(item => item != null).ToList();
            prewarmShaders = prewarmConfig.PrewarmShaders.Where(item => item != null).ToList();
            prewarmShaderKeywords = prewarmConfig.PrewarmShaderKeywords.Where(item => item != null).ToList();
            prewarmSeedMeshes = prewarmConfig.PrewarmSeedMeshes.Where(item => item != null).ToList();
            prewarmSeedMeshRendererObjs = prewarmConfig.PrewarmSeedMeshRendererObjs.Where(item => item != null).ToList();
            prewarmLights = prewarmConfig.PrewarmLights.Where(item => item != null).ToList();

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(this);
            debugControls = new DebugControls();

            StartCoroutine(UpdateLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        void Update()
        {
            if (OVRInput.GetDown(OVRInput.Button.One)) // Oculus button A
            {
                OnOculusControllerButtonAPressed();
            }

            if (OVRInput.GetDown(OVRInput.Button.Two))
            {
                // Replace with your own triggering logic to start the post-prewarming scene at the moment you need
                LoadNextScene();
            }
        }
        IEnumerator UpdateLoop()
        {
            var spawnedPrewarmLights = new List<Light>();
            while (true)
            {
                yield return null; // Basic yield to act like a per Unity frame update loop

                if (spawnedPrewarmLights.Count > 0)
                {
                    foreach (var light in spawnedPrewarmLights)
                        Destroy(light.gameObject);
                    spawnedPrewarmLights.Clear();
                }

                if (Time.time - lastDepawnTime > minDespawnInterval)
                {
                    lastDepawnTime = Time.time;
                    if (prewarmCleanupQueue.Count > 0)
                    {
                        Debug.Log($"Started despawning {prewarmCleanupQueue.Count} objects");
                        while (prewarmCleanupQueue.Count > 0)
                        {
                            // cleanup
                            var go = prewarmCleanupQueue.Dequeue();
                            Destroy(go);
                        }
                    }
                }

                if (prewarmingStarted && Time.time - lastSpawnTime > minSpawnInterval)
                {
                    lastSpawnTime = Time.time;
                    var spawnCount = PrewarmSpawn();
                    //Debug.Log("Spawn Count: " + spawnCount);
                    if (spawnCount > 0 && prewarmLightSubsets.Count > 0)
                    {
                        yield return null;
                        var lightSubsetQueue = new Queue<List<Light>>(prewarmLightSubsets);
                        // Prewarm with light subsets in multiple iterations
                        while (true)
                        {
                            if (spawnedPrewarmLights.Count > 0)
                            {
                                foreach (var light in spawnedPrewarmLights)
                                    Destroy(light.gameObject);
                                spawnedPrewarmLights.Clear();
                            }

                            var lightSubset = lightSubsetQueue.Dequeue();
                            Debug.Log($"Started spawning prewarming light subset for {spawnCount} existing scene objects. " +
                                $"Current light subset: {string.Join(" | ", lightSubset.Select(l => l.gameObject.name))}");
                            foreach (var light in lightSubset)
                            {
                                var l = PrewarmSpawnLight(light);
                                spawnedPrewarmLights.Add(l);
                            }

                            if (lightSubsetQueue.Count == 0)
                            {
                                break;
                            }

                            yield return null;
                        }
                    }
                }
            }
        }

        private void PreparePrewarming()
        {
            PreparePrewarmingRenderObjects();
            if (prewarmLights.Count > 0)
            {
                PreparePrewarmingLightingObjects();
            }
        }

        private void PreparePrewarmingRenderObjects()
        {
            // Make sure only filter out GOs with mesh renderers and append
            prewarmSeedMeshRenderers.Clear();
            var seedMeshRenderSet = new HashSet<MeshRenderer>();
            foreach (var go in prewarmSeedMeshRendererObjs)
            {
                var meshRends = go.GetComponentsInChildren<MeshRenderer>();
                foreach (var meshRend in meshRends)
                {
                    if (seedMeshRenderSet.Contains(meshRend)) // dedup
                        continue;
                    prewarmSeedMeshRenderers.Add(meshRend);
                    seedMeshRenderSet.Add(meshRend);
                }
            }
            if (useSourcePrefabs)
            {
                foreach (var prefab in prewarmPrefabs)
                {
                    prewarmPrefabQueue.Enqueue(prefab);
                }
            }
            if (meshSeedType != MeshSeedType.None)
            {
                if (useSourceShaders)
                {
                    PrepareShaderGenMaterials();
                }
                if (meshSeedType == MeshSeedType.Mesh || meshSeedType == MeshSeedType.All)
                {
                    if (useSourceMaterials)
                    {
                        foreach (var mesh in prewarmSeedMeshes)
                            foreach (var mat in prewarmMaterials)
                                prewarmMeshMaterialQueue.Enqueue((mesh, mat));
                    }
                    if (useSourceShaders)
                    {
                        foreach (var mesh in prewarmSeedMeshes)
                            foreach (var mat in prewarmShaderGenMaterials)
                                prewarmMeshMaterialQueue.Enqueue((mesh, mat));
                    }
                }
                if (meshSeedType == MeshSeedType.MeshRenderer || meshSeedType == MeshSeedType.All)
                {
                    if (useSourceMaterials)
                    {
                        foreach (var meshRend in prewarmSeedMeshRenderers)
                            foreach (var mat in prewarmMaterials)
                                prewarmMeshRendererMaterialQueue.Enqueue((meshRend, mat));
                    }
                    if (useSourceShaders)
                    {
                        foreach (var meshRend in prewarmSeedMeshRenderers)
                            foreach (var mat in prewarmShaderGenMaterials)
                                prewarmMeshRendererMaterialQueue.Enqueue((meshRend, mat));
                    }
                    Debug.Log($"Total {nameof(prewarmMeshRendererMaterialQueue)} count: {prewarmMeshRendererMaterialQueue.Count}");
                }
            }
        }

        private void PreparePrewarmingLightingObjects()
        {
            // Populate prewarmLightSubsets with all possible subsets of prewarmLights
            prewarmLightSubsets = Utils.GetAllNonEmptySubsets(prewarmLights);
            Debug.Log("prewarmLightSubsets.Count: " + prewarmLightSubsets.Count);
        }

        private void StartPrewarmingSpawning()
        {
            prewarmingStarted = true;
        }

        private int PrewarmSpawn()
        {
            var spawnCount = 0;
            while (prewarmPrefabQueue.Count > 0)
            {
                var obj = prewarmPrefabQueue.Dequeue();
                Debug.Log($"Prewarm spawn object: {obj.name}");
                var go = Instantiate(obj);
                {
                    var basePos = Vector3.zero;
                    var forward = Vector3.forward; var up = Vector3.up; var right = Vector3.right;
                    if (fixToTransform != null)
                    {
                        go.transform.SetParent(fixToTransform);
                        basePos = fixToTransform.position;
                        forward = fixToTransform.forward;
                        up = fixToTransform.up;
                        right = fixToTransform.right;
                    }
                    go.transform.position = basePos + forward * spawnSpans.z +
                        up * Random.Range(-spawnSpans.y, spawnSpans.y) +
                        right * Random.Range(-spawnSpans.x, spawnSpans.x);
                    // TODO: figure out how to handle renderers and bounds here
                }
                go.SetActive(true);
                prewarmCleanupQueue.Enqueue(go);
                spawnCount++;
                if (spawnCount == maxObjPerSpawn)
                {
                    return spawnCount;
                }
            }
            if (spawnCount > 0)
                return spawnCount;

            if (prewarmMeshMaterialQueue.Count > 0)
            {
                while (prewarmMeshMaterialQueue.Count > 0)
                {
                    var (mesh, mat) = prewarmMeshMaterialQueue.Dequeue();
                    if (lastPrewarmMaterial != mat)
                    {
                        Debug.Log($"Started prewarm rendering material \"{mat.name}\", with mesh seeds");
                    }
                    lastPrewarmMaterial = mat;
                    var shaderKeywords = mat.shaderKeywords;
                    shaderKeywords = shaderKeywords.OrderBy(name => name).ToArray();

                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(go.GetComponent<Collider>());
                    go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    {
                        var basePos = Vector3.zero;
                        var forward = Vector3.forward; var up = Vector3.up; var right = Vector3.right;
                        if (fixToTransform != null)
                        {
                            go.transform.SetParent(fixToTransform);
                            basePos = fixToTransform.position;
                            forward = fixToTransform.forward;
                            up = fixToTransform.up;
                            right = fixToTransform.right;
                        }
                        go.transform.position = basePos + forward * spawnSpans.z +
                            up * Random.Range(-spawnSpans.y, spawnSpans.y) +
                            right * Random.Range(-spawnSpans.x, spawnSpans.x);
                        go.transform.localScale = Vector3.one;
                    }

                    prewarmCleanupQueue.Enqueue(go);
                    spawnCount++;
                    if (spawnCount == maxObjPerSpawn)
                    {
                        return spawnCount;
                    }
                }
                if (prewarmMeshMaterialQueue.Count == 0)
                {
                    Debug.Log($"Finished prewarm rendering {nameof(prewarmMeshMaterialQueue)}");
                }
            }
            if (spawnCount > 0)
                return spawnCount;

            if (prewarmMeshRendererMaterialQueue.Count > 0)
            {
                while (prewarmMeshRendererMaterialQueue.Count > 0)
                {
                    var (meshRenderer, mat) = prewarmMeshRendererMaterialQueue.Dequeue();
                    if (lastPrewarmMaterial != mat)
                    {
                        Debug.Log($"Started prewarm rendering material \"{mat.name}\" with mesh renderer seeds");
                    }
                    lastPrewarmMaterial = mat;
                    var shaderKeywords = mat.shaderKeywords;
                    shaderKeywords = shaderKeywords.OrderBy(name => name).ToArray();
                    // var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    var go = Instantiate(meshRenderer).gameObject;
                    if (go.GetComponent<Collider>() != null)
                        Destroy(go.GetComponent<Collider>());
                    var rend = go.GetComponent<MeshRenderer>(); // should be no need to check null here
                    // CopyMeshRendererProperties(meshRenderer, rend);
                    rend.sharedMaterial = mat;
                    {
                        var basePos = Vector3.zero;
                        var forward = Vector3.forward; var up = Vector3.up; var right = Vector3.right;
                        if (fixToTransform != null)
                        {
                            go.transform.SetParent(fixToTransform);
                            basePos = fixToTransform.position;
                            forward = fixToTransform.forward;
                            up = fixToTransform.up;
                            right = fixToTransform.right;
                        }
                        go.transform.position = basePos + forward * spawnSpans.z +
                            up * Random.Range(-spawnSpans.y, spawnSpans.y) +
                            right * Random.Range(-spawnSpans.x, spawnSpans.x);
                        var rendBounds = rend.bounds;
                        float largestAxis = Mathf.Max(rendBounds.size.x, rendBounds.size.y, rendBounds.size.z);
                        float scaleMultiplier = 1.0f / largestAxis;
                        go.transform.localScale = Vector3.one * scaleMultiplier;
                    }

                    prewarmCleanupQueue.Enqueue(go);
                    spawnCount++;
                    if (spawnCount == maxObjPerSpawn)
                    {
                        return spawnCount;
                    }
                }
                if (prewarmMeshMaterialQueue.Count == 0)
                {
                    Debug.Log($"Finished prewarm rendering {nameof(prewarmMeshMaterialQueue)}");
                }
            }
            if (spawnCount > 0)
                return spawnCount;

            // Potentially more approaches & stages later ...

            return 0;
        }

        private Light PrewarmSpawnLight(Light l)
        {
            var lightName = l.gameObject.name;
            var light = Instantiate(l);
            light.gameObject.SetActive(true); // ensure it's on
            light.enabled = true; // ensure it's on
            light.gameObject.name = $"Prewarm Light - {lightName}";
            var go = light.gameObject;

            var basePos = Vector3.zero;
            var forward = Vector3.forward; var up = Vector3.up; var right = Vector3.right;
            if (fixToTransform != null)
            {
                go.transform.SetParent(fixToTransform);
                basePos = fixToTransform.position;
                forward = fixToTransform.forward;
                up = fixToTransform.up;
                right = fixToTransform.right;
            }

            if (light.type == UnityEngine.LightType.Directional)
            {
                // Directional light position doesn't matter, just face it down to try to light everything
                go.transform.position = basePos + forward * spawnSpans.z + up * spawnSpans.y * 2.0f;
                go.transform.eulerAngles = new Vector3(90f, 0f, 0f);
            }
            else if (light.type == UnityEngine.LightType.Point)
            {
                // Place the point light in the center of the scene object spawn range / region
                go.transform.position = basePos + forward * spawnSpans.z;
                // Adjust point light radius to make sure it can encompass all in scene objects
                float maxDistance = Mathf.Max(spawnSpans.x, spawnSpans.y);
                light.range = maxDistance * 1.5f; // Increase the range by 50% to ensure all objects are covered
            }
            else if (light.type == UnityEngine.LightType.Spot)
            {
                // Put spot light on top of the scene prewarm objects, facing down
                go.transform.position = basePos + forward * spawnSpans.z + up * spawnSpans.y * 2.05f;
                go.transform.eulerAngles = new Vector3(90f, 0f, 0f);
                // Adjust spot angle attribute to make sure it can light all objects below it
                float angle = Mathf.Atan2(spawnSpans.x, spawnSpans.y) * Mathf.Rad2Deg;
                light.spotAngle = angle * 2f; // Increase the angle by 100% to ensure all objects are covered
                light.range = spawnSpans.y * 2.1f;
            }
            return light;
        }

        public void ClearShaderPrewarmerSetupData()
        {
            prewarmConfig.ClearShaderPrewarmerSetupData();
        }

        private void PrepareShaderGenMaterials()
        {
            foreach (var s in prewarmShaderKeywords)
            {
                var shader = s.shader;
                var keywordsList = s.keywordsList;
                foreach (var ks in keywordsList)
                {
                    var mat = new Material(shader)
                    {
                        name = $"Generated Material (from shader \"{shader.name}\""
                    };
                    // TODO: Check if it is needed to disable all keywords first
                    var keywords = ks.keywords;
                    foreach (var keyword in keywords)
                    {
                        if (keyword == "INSTANCING_ON")
                        {
                            mat.enableInstancing = true;
                        }
                        // Maybe need to add other special condition as well
                        else
                        {
                            mat.EnableKeyword(keyword);
                        }
                    }
                    prewarmShaderGenMaterials.Add(mat);
                }
            }
        }

        public void DebugShaderPrewarmerData()
        {
            prewarmConfig.DebugShaderPrewarmerData();
        }

        public void DebugPrewarmShaderKeywords()
        {
            int totalShaderKeywordCombinationCount = 0;
            foreach (var shaderKeywords in prewarmConfig.PrewarmShaderKeywords)
            {
                Debug.Log($"Shader \"{shaderKeywords.shader.name}\" has keywords combination count: {shaderKeywords.keywordsList.Count}");
                totalShaderKeywordCombinationCount += shaderKeywords.keywordsList.Count;
                foreach (var keywords in shaderKeywords.keywordsList)
                {
                    Debug.Log($"Shader \"{shaderKeywords.shader.name}\" has the keywords: {string.Join(" ", keywords.keywords)}");
                }
            }
        }

        public void SetupValidShaderKeywordCombinations()
        {
            prewarmConfig.SetupValidShaderKeywordCombinations();
        }

        private void LoadNextScene()
        {
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
            if (loadNextSceneIndex < 0 || loadNextSceneIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"Invalid scene index: {loadNextSceneIndex}. Index is out of range.");
                return;
            }
            if (loadNextSceneIndex == currentSceneIndex)
            {
                Debug.LogWarning("Won't switch to the currently active scene.");
                return;
            }
            SceneManager.LoadScene(loadNextSceneIndex);
        }

        public static void CopyMeshRendererProperties(MeshRenderer source, MeshRenderer dest)
        {
            var arrays = new Material[source.sharedMaterials.Length];
            System.Array.Copy(source.sharedMaterials, arrays, source.sharedMaterials.Length);
            dest.sharedMaterials = arrays;
            dest.receiveShadows = source.receiveShadows;
            dest.shadowCastingMode = source.shadowCastingMode;
            dest.lightProbeUsage = source.lightProbeUsage;
            dest.reflectionProbeUsage = source.reflectionProbeUsage;
            dest.motionVectorGenerationMode = source.motionVectorGenerationMode;
            dest.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        }
    }
}
