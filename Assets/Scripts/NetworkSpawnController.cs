using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace MultiplayerActivity
{
    /// <summary>
    /// An in-scene NetworkObject. Client button presses become server RPC requests;
    /// only the server actually instantiates, spawns, and despawns network objects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSpawnController : NetworkBehaviour
    {
        [SerializeField] private NetworkObject networkCubePrefab;
        [SerializeField, Min(1f)] private float objectLifetime = 10f;

        private readonly NetworkVariable<int> _spawnCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            if (IsServer && Environment.GetCommandLineArgs().Any(
                    value => value.Equals("--auto-spawn", StringComparison.OrdinalIgnoreCase)))
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnectedForSmokeTest;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnectedForSmokeTest;
            }
        }

        private void OnGUI()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16, 315, 350, 230), GUI.skin.box);
            GUILayout.Label("SERVER-AUTHORITATIVE OBJECT CONTROLS", HeaderStyle());
            GUILayout.Label($"Network objects spawned this session: {_spawnCount.Value}");
            GUILayout.Label($"Automatic lifetime: {objectLifetime:0.0} seconds");

            GUI.enabled = IsSpawned;
            if (GUILayout.Button("Spawn Network Cube", GUILayout.Height(42)))
            {
                RequestSpawn();
            }

            if (GUILayout.Button("Despawn All Cubes", GUILayout.Height(34)))
            {
                RequestDespawnAll();
            }
            GUI.enabled = true;

            GUILayout.Space(5);
            GUILayout.Label(
                manager.IsServer
                    ? "This instance has authority. Instantiate -> configure -> NetworkObject.Spawn()."
                    : "This client sends an RPC request; the server performs the spawn/despawn.",
                HintStyle());
            GUILayout.EndArea();
        }

        public void RequestSpawn()
        {
            if (!IsSpawned)
            {
                return;
            }

            RequestSpawnRpc();
        }

        public void RequestDespawnAll()
        {
            if (!IsSpawned)
            {
                return;
            }

            RequestDespawnAllRpc();
        }

        [Rpc(SendTo.Server)]
        private void RequestSpawnRpc(RpcParams rpcParams = default)
        {
            SpawnOnServer(rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server)]
        private void RequestDespawnAllRpc()
        {
            DespawnAllOnServer();
        }

        private void SpawnOnServer(ulong requestedByClientId)
        {
            if (!IsServer || networkCubePrefab == null)
            {
                return;
            }

            int sequence = _spawnCount.Value + 1;
            float angle = sequence * 1.35f;
            Vector3 position = new(
                Mathf.Sin(angle) * 3.5f,
                0.8f + (sequence % 3) * 0.55f,
                Mathf.Cos(angle) * 2.4f);

            // 1) INSTANTIATE the registered network prefab on the server.
            NetworkObject instance = Instantiate(networkCubePrefab, position, Quaternion.identity);

            // 2) CONFIGURE ordinary component state before spawning it.
            Color tint = Color.HSVToRGB((sequence * 0.17f) % 1f, 0.72f, 1f);
            SpawnedNetworkCube cube = instance.GetComponent<SpawnedNetworkCube>();
            cube.ConfigureBeforeSpawn(sequence, objectLifetime, tint, position);
            instance.name = $"NetworkCube_{sequence:00}";

            // 3) NETWORK SPAWN. NGO creates a matching object on every client.
            instance.Spawn();
            _spawnCount.Value = sequence;

            Debug.Log(
                $"[SERVER] Spawned {instance.name}, NetworkObjectId={instance.NetworkObjectId}, " +
                $"requested by client {requestedByClientId}.");
        }

        private void DespawnAllOnServer()
        {
            if (!IsServer)
            {
                return;
            }

            SpawnedNetworkCube[] cubes = FindObjectsByType<SpawnedNetworkCube>(FindObjectsSortMode.None);
            foreach (SpawnedNetworkCube cube in cubes)
            {
                if (cube != null && cube.NetworkObject != null && cube.NetworkObject.IsSpawned)
                {
                    cube.NetworkObject.Despawn(true);
                }
            }
        }

        private void OnClientConnectedForSmokeTest(ulong clientId)
        {
            if (IsServer && clientId != NetworkManager.LocalClientId)
            {
                SpawnOnServer(clientId);
            }
        }

        private static GUIStyle HeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            return style;
        }

        private static GUIStyle HintStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };
            style.normal.textColor = new Color(0.78f, 0.82f, 0.9f);
            return style;
        }
    }
}
