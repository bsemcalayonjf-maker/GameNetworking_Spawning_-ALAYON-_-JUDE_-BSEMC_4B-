using System.Collections.Generic;
using System.IO;
using MultiplayerActivity;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiplayerActivity.Editor
{
    /// <summary>
    /// Rebuilds all authored Unity assets for the activity. This also makes the
    /// repository reproducible instead of relying on undocumented Inspector state.
    /// </summary>
    public static class MultiplayerActivityProjectBuilder
    {
        private const string ScenePath = "Assets/Scenes/NetworkSpawnDemo.unity";
        private const string PrefabPath = "Assets/Prefabs/NetworkCube.prefab";
        private const string PrefabListPath = "Assets/NetworkPrefabsList.asset";
        private const string MaterialPath = "Assets/Materials/NetworkCubeBase.mat";

        [MenuItem("Tools/Multiplayer Activity/Rebuild Demo Scene")]
        public static void Build()
        {
            EnsureFolders();
            RemoveGeneratedAssets();

            Material material = CreateMaterial();
            NetworkObject cubePrefab = CreateNetworkCubePrefab(material);
            NetworkPrefabsList prefabList = CreateNetworkPrefabList(cubePrefab.gameObject);
            CreateDemoScene(cubePrefab, prefabList);
            ConfigureProjectSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Multiplayer Activity demo scene and network prefab were rebuilt successfully.");
        }

        [MenuItem("Tools/Multiplayer Activity/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            Build();
            Directory.CreateDirectory("Builds/Windows");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/NetworkSpawnDemo.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException($"Windows build failed: {report.summary.result}");
            }

            Debug.Log($"Windows player built at {Path.GetFullPath(options.locationPathName)}");
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets", "Scenes");
            CreateFolderIfMissing("Assets", "Prefabs");
            CreateFolderIfMissing("Assets", "Materials");
        }

        private static void CreateFolderIfMissing(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void RemoveGeneratedAssets()
        {
            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.DeleteAsset(PrefabPath);
            AssetDatabase.DeleteAsset(PrefabListPath);
            AssetDatabase.DeleteAsset(MaterialPath);
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new System.InvalidOperationException("No compatible Lit shader was found.");
            }

            var material = new Material(shader)
            {
                name = "NetworkCubeBase",
                color = new Color(0.12f, 0.65f, 1f)
            };
            material.SetFloat("_Glossiness", 0.65f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static NetworkObject CreateNetworkCubePrefab(Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "NetworkCube";
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;

            var networkObject = cube.AddComponent<NetworkObject>();
            cube.AddComponent<NetworkTransform>();
            cube.AddComponent<SpawnedNetworkCube>();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(cube, PrefabPath);
            Object.DestroyImmediate(cube);
            return savedPrefab.GetComponent<NetworkObject>();
        }

        private static NetworkPrefabsList CreateNetworkPrefabList(GameObject prefab)
        {
            var prefabList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            prefabList.Add(new NetworkPrefab { Prefab = prefab });
            AssetDatabase.CreateAsset(prefabList, PrefabListPath);
            return prefabList;
        }

        private static void CreateDemoScene(NetworkObject cubePrefab, NetworkPrefabsList prefabList)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "NetworkSpawnDemo";

            CreateCamera();
            CreateLighting();
            CreateStage();

            GameObject managerObject = new("NetworkManager");
            var networkManager = managerObject.AddComponent<NetworkManager>();
            var transport = managerObject.AddComponent<UnityTransport>();
            managerObject.AddComponent<NetworkLauncher>();

            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                TickRate = 30,
                EnableSceneManagement = true,
                ForceSamePrefabs = true
            };
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists =
                new List<NetworkPrefabsList> { prefabList };

            GameObject controllerObject = new("NetworkSpawnController");
            controllerObject.AddComponent<NetworkObject>();
            NetworkSpawnController controller = controllerObject.AddComponent<NetworkSpawnController>();

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("networkCubePrefab").objectReferenceValue = cubePrefab;
            serializedController.FindProperty("objectLifetime").floatValue = 10f;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 5.4f, -10.5f);
            cameraObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.07f);
            camera.fieldOfView = 55f;
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(0.84f, 0.9f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(45f, -32f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.2f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.11f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.04f, 0.06f);
        }

        private static void CreateStage()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Demo Stage";
            floor.transform.position = new Vector3(0f, -0.05f, 1f);
            floor.transform.localScale = new Vector3(1.3f, 1f, 0.85f);

            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            var stageMaterial = new Material(shader)
            {
                name = "DemoStage_Runtime",
                color = new Color(0.08f, 0.11f, 0.18f)
            };
            floor.GetComponent<MeshRenderer>().sharedMaterial = stageMaterial;

            for (int i = -3; i <= 3; i++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"StageMarker_{i + 4}";
                marker.transform.position = new Vector3(i * 1.5f, 0.03f, 3.7f);
                marker.transform.localScale = new Vector3(0.08f, 0.03f, 0.08f);
                marker.GetComponent<MeshRenderer>().sharedMaterial = stageMaterial;
            }
        }

        private static void ConfigureProjectSettings()
        {
            PlayerSettings.companyName = "Networking Activity";
            PlayerSettings.productName = "NetworkObject Spawn Despawn Demo";
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 600;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }
    }
}
