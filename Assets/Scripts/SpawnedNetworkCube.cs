using Unity.Netcode;
using UnityEngine;

namespace MultiplayerActivity
{
    /// <summary>
    /// Networked behavior for the spawned prefab. The server owns movement and lifetime;
    /// NetworkTransform and NetworkVariables reproduce that state on every client.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [DisallowMultipleComponent]
    public sealed class SpawnedNetworkCube : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _sequence = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _remainingLifetime = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Color> _tint = new(
            Color.white,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private MeshRenderer _meshRenderer;
        private Material _runtimeMaterial;
        private int _configuredSequence;
        private float _configuredLifetime;
        private Color _configuredTint = Color.white;
        private Vector3 _serverOrigin;
        private float _despawnAt;
        private float _nextLifetimeSync;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer != null)
            {
                _runtimeMaterial = new Material(_meshRenderer.sharedMaterial);
                _meshRenderer.material = _runtimeMaterial;
            }
        }

        public void ConfigureBeforeSpawn(int sequence, float lifetime, Color tint, Vector3 origin)
        {
            if (IsSpawned)
            {
                Debug.LogWarning("Configuration must happen before NetworkObject.Spawn().", this);
                return;
            }

            _configuredSequence = sequence;
            _configuredLifetime = Mathf.Max(1f, lifetime);
            _configuredTint = tint;
            _serverOrigin = origin;
            transform.localScale = Vector3.one * (0.75f + (sequence % 3) * 0.12f);
            transform.rotation = Quaternion.Euler(15f, sequence * 29f, 8f);
        }

        public override void OnNetworkSpawn()
        {
            _tint.OnValueChanged += OnTintChanged;
            _sequence.OnValueChanged += OnSequenceChanged;

            if (IsServer)
            {
                _sequence.Value = _configuredSequence;
                _remainingLifetime.Value = _configuredLifetime;
                _tint.Value = _configuredTint;
                _despawnAt = Time.time + _configuredLifetime;
                _nextLifetimeSync = Time.time;
            }

            ApplyTint(_tint.Value);
            UpdateLocalName(_sequence.Value);
            Debug.Log($"[{LocalRole()}] OnNetworkSpawn: NetworkObjectId={NetworkObjectId}", this);
        }

        public override void OnNetworkDespawn()
        {
            _tint.OnValueChanged -= OnTintChanged;
            _sequence.OnValueChanged -= OnSequenceChanged;
            Debug.Log($"[{LocalRole()}] OnNetworkDespawn: NetworkObjectId={NetworkObjectId}", this);
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            double serverTime = NetworkManager.ServerTime.Time;
            float orbit = (float)serverTime * 1.25f + _configuredSequence;
            transform.position = _serverOrigin + new Vector3(
                Mathf.Sin(orbit) * 0.65f,
                Mathf.Sin(orbit * 1.7f) * 0.35f,
                Mathf.Cos(orbit) * 0.65f);
            transform.Rotate(35f * Time.deltaTime, 70f * Time.deltaTime, 25f * Time.deltaTime, Space.Self);

            float remaining = Mathf.Max(0f, _despawnAt - Time.time);
            if (Time.time >= _nextLifetimeSync)
            {
                _remainingLifetime.Value = remaining;
                _nextLifetimeSync = Time.time + 0.1f;
            }

            if (remaining <= 0f && NetworkObject.IsSpawned)
            {
                Debug.Log($"[SERVER] Lifetime ended; despawning NetworkObjectId={NetworkObjectId}.", this);
                NetworkObject.Despawn(true);
            }
        }

        private void OnGUI()
        {
            if (!IsSpawned || Camera.main == null)
            {
                return;
            }

            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.15f);
            if (screen.z <= 0f)
            {
                return;
            }

            Rect rect = new(screen.x - 85f, Screen.height - screen.y - 24f, 170f, 46f);
            GUI.Label(
                rect,
                $"Cube #{_sequence.Value} | Net ID {NetworkObjectId}\nDespawns in {_remainingLifetime.Value:0.0}s",
                LabelStyle());
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }
        }

        private void OnTintChanged(Color previous, Color current)
        {
            ApplyTint(current);
        }

        private void OnSequenceChanged(int previous, int current)
        {
            UpdateLocalName(current);
        }

        private void ApplyTint(Color color)
        {
            if (_runtimeMaterial != null)
            {
                _runtimeMaterial.color = color;
            }
        }

        private void UpdateLocalName(int sequence)
        {
            if (sequence > 0)
            {
                gameObject.name = $"NetworkCube_{sequence:00}_LocalClone";
            }
        }

        private string LocalRole()
        {
            if (NetworkManager == null)
            {
                return "OFFLINE";
            }

            return NetworkManager.IsHost ? "HOST" : NetworkManager.IsServer ? "SERVER" : "CLIENT";
        }

        private static GUIStyle LabelStyle()
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;
            return style;
        }
    }
}
