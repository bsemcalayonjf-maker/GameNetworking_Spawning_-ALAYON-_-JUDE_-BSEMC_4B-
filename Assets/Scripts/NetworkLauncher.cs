using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MultiplayerActivity
{
    /// <summary>
    /// Small connection panel for testing one build as Host and another as Client.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkLauncher : MonoBehaviour
    {
        private string _address = "127.0.0.1";
        private string _portText = "7777";
        private string _lastMessage = "Choose Host or Client.";

        private void Awake()
        {
            Application.targetFrameRate = 60;
        }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            string addressArg = args.FirstOrDefault(value => value.StartsWith("--address=", StringComparison.OrdinalIgnoreCase));
            string portArg = args.FirstOrDefault(value => value.StartsWith("--port=", StringComparison.OrdinalIgnoreCase));
            string quitArg = args.FirstOrDefault(value => value.StartsWith("--quit-after=", StringComparison.OrdinalIgnoreCase));

            if (addressArg != null)
            {
                _address = addressArg.Substring("--address=".Length);
            }
            if (portArg != null)
            {
                _portText = portArg.Substring("--port=".Length);
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                if (args.Any(value => value.Equals("--host", StringComparison.OrdinalIgnoreCase)))
                {
                    StartAsHost(manager);
                }
                else if (args.Any(value => value.Equals("--client", StringComparison.OrdinalIgnoreCase)))
                {
                    StartAsClient(manager);
                }
            }

            if (quitArg != null && float.TryParse(
                    quitArg.Substring("--quit-after=".Length),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float seconds))
            {
                StartCoroutine(QuitAfter(Mathf.Max(1f, seconds)));
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 350, 285), GUI.skin.box);
            GUILayout.Label("NETWORKOBJECT SPAWN / DESPAWN", HeaderStyle());

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                GUILayout.Label("NetworkManager is missing from the scene.");
                GUILayout.EndArea();
                return;
            }

            if (!manager.IsListening)
            {
                GUILayout.Label("Connection address");
                _address = GUILayout.TextField(_address);
                GUILayout.Label("Port");
                _portText = GUILayout.TextField(_portText);

                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Start Host", GUILayout.Height(36)))
                {
                    StartAsHost(manager);
                }

                if (GUILayout.Button("Start Client", GUILayout.Height(36)))
                {
                    StartAsClient(manager);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                string mode = manager.IsHost ? "HOST" : manager.IsServer ? "SERVER" : "CLIENT";
                GUILayout.Label($"Mode: {mode}");
                GUILayout.Label($"Local client ID: {manager.LocalClientId}");
                if (manager.IsServer)
                {
                    GUILayout.Label($"Connected clients: {manager.ConnectedClientsIds.Count}");
                }

                GUILayout.Space(8);
                if (GUILayout.Button("Shutdown", GUILayout.Height(32)))
                {
                    manager.Shutdown();
                    _lastMessage = "Network session stopped.";
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(_lastMessage);
            GUILayout.Label("Use the controls below after both instances connect.", HintStyle());
            GUILayout.EndArea();
        }

        private void StartAsHost(NetworkManager manager)
        {
            if (!ConfigureTransport(manager))
            {
                return;
            }

            _lastMessage = manager.StartHost()
                ? "Host started. Waiting for a client..."
                : "Host failed to start. Check the Console.";
        }

        private void StartAsClient(NetworkManager manager)
        {
            if (!ConfigureTransport(manager))
            {
                return;
            }

            _lastMessage = manager.StartClient()
                ? "Client is connecting..."
                : "Client failed to start. Check the Console.";
        }

        private bool ConfigureTransport(NetworkManager manager)
        {
            if (!ushort.TryParse(_portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort port))
            {
                _lastMessage = "Port must be a number from 0 to 65535.";
                return false;
            }

            UnityTransport transport = manager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                _lastMessage = "Unity Transport is missing from NetworkManager.";
                return false;
            }

            transport.SetConnectionData(string.IsNullOrWhiteSpace(_address) ? "127.0.0.1" : _address.Trim(), port);
            return true;
        }

        private static IEnumerator QuitAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            Application.Quit();
        }

        private static GUIStyle HeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
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
