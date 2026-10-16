using System;
using System.Collections.Generic;
using Fortal.UWB;
using UnityEngine;

namespace Fortal.Players
{
    // Spawns the player prefab once per configured UWB identity.
    public sealed class UWBPlayerSpawner : MonoBehaviour
    {
        public const int MaximumPlayers = 6;

        [Serializable]
        public sealed class PlayerDefinition
        {
            [SerializeField] private bool enabled = true;
            [SerializeField, Min(1)] private int playerId = 1;
            [SerializeField, Min(0)] private int tagId = 1;
            [Tooltip("Floor X/Z before the first UWB frame, and the spawn point in standalone Simulation (Vector2.y is world Z).")]
            [SerializeField] private Vector2 initialPosition;

            public bool Enabled => enabled;
            public int PlayerId => playerId;
            public int TagId => tagId;
            public Vector2 InitialPosition => initialPosition;

            public PlayerDefinition(int playerId, int tagId, Vector2 initialPosition)
            {
                this.playerId = playerId;
                this.tagId = tagId;
                this.initialPosition = initialPosition;
            }
        }

        [Header("Player Template")]
        [Tooltip("Player prefab with UWBPlayerController, Rigidbody and SphereCollider.")]
        [SerializeField] private UWBPlayerController playerPrefab;
        [SerializeField] private Transform playerParent;
        [SerializeField] private bool spawnOnStart = true;

        [Header("Player Size")]
        [Tooltip("Overall scale of every Player, including marker and collider.")]
        [SerializeField, Min(0.05f)] private float playerScale = 0.462f;

        [Header("Simulation")]
        [Tooltip("Number of simulated players (1-6), taken in order from the enabled Players. Applies to standalone Simulation only.")]
        [SerializeField, Range(1, MaximumPlayers)] private int simulationPlayerCount = 4;

        [Header("Player ID / UWB Tag Mapping")]
        [SerializeField] private PlayerDefinition[] players =
        {
            new PlayerDefinition(1, 6, new Vector2(-3.3f, -1.65f)),
            new PlayerDefinition(2, 7, new Vector2(-1.1f, -1.65f)),
            new PlayerDefinition(3, 8, new Vector2(1.1f, -1.65f)),
            new PlayerDefinition(4, 9, new Vector2(3.3f, -1.65f)),
            new PlayerDefinition(5, 10, new Vector2(-4.4f, -1.65f)),
            new PlayerDefinition(6, 11, new Vector2(4.4f, -1.65f))
        };

        [Header("Serial Presence")]
        [Tooltip("In Serial mode, disable each Player until its UWB tag is receiving fresh data. The tag stays registered so the Player can come back.")]
        [SerializeField] private bool disableOfflinePlayersInSerial = true;

        private readonly List<UWBPlayerController> spawnedPlayers = new List<UWBPlayerController>();
        // Once the game has started, only these players may come online; null while anyone may join.
        private HashSet<int> roster;
        private UWBManager uwbManager;
        private bool standaloneSimulationMode;

        public IReadOnlyList<UWBPlayerController> SpawnedPlayers => spawnedPlayers;

        // True when UWBManager is set to Simulation; players are then placed and dragged by hand.
        public bool IsStandaloneSimulationMode => standaloneSimulationMode;

        private void Awake()
        {
            UWBManager manager = FindAnyObjectByType<UWBManager>();
            standaloneSimulationMode = manager != null && manager.IsSimulationMode;
            if (!standaloneSimulationMode)
            {
                return;
            }

            // Simulation only selects the mode here, so the UWB simulation pipeline is
            // switched off to keep it from overwriting hand-placed players.
            manager.enabled = false;
        }

        private void Start()
        {
            uwbManager = standaloneSimulationMode ? null : FindAnyObjectByType<UWBManager>();
            if (spawnOnStart)
            {
                SpawnPlayers();
            }
        }

        private void Update()
        {
            RefreshSerialPlayerPresence();
        }

        private void OnDestroy()
        {
            if (uwbManager == null)
            {
                return;
            }

            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                if (spawnedPlayers[i] != null)
                {
                    uwbManager.UnregisterTag(spawnedPlayers[i].TagId);
                }
            }
        }

        [ContextMenu("Spawn Players")]
        public void SpawnPlayers()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[UWBPlayerSpawner] Spawn Players works in Play Mode only.", this);
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("[UWBPlayerSpawner] Assign the player prefab before spawning.", this);
                return;
            }

            ClearSpawnedPlayers();
            if (players == null)
            {
                return;
            }

            Transform targetParent = playerParent != null ? playerParent : transform;
            int simulationLimit = Mathf.Clamp(simulationPlayerCount, 1, MaximumPlayers);
            for (int i = 0; i < players.Length; i++)
            {
                if (standaloneSimulationMode && spawnedPlayers.Count >= simulationLimit)
                {
                    break;
                }

                PlayerDefinition definition = players[i];
                if (definition == null || !definition.Enabled)
                {
                    continue;
                }

                spawnedPlayers.Add(SpawnPlayer(definition, targetParent));
            }

            if (standaloneSimulationMode && spawnedPlayers.Count < simulationLimit)
            {
                Debug.LogWarning("[UWBPlayerSpawner] Fewer enabled Players than Simulation Player Count.", this);
            }
        }

        // Closes the game to newcomers: from now on only these players show when their tag is online
        // (they may still drop out and come back). Others stay hidden even if their tag connects.
        public void LockRoster(IEnumerable<int> playerIds)
        {
            roster = new HashSet<int>(playerIds);
            Debug.Log($"[UWBPlayerSpawner] Players locked in: {string.Join(", ", roster)}", this);
        }

        public bool TryGetPlayer(int playerId, out UWBPlayerController player)
        {
            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                if (spawnedPlayers[i] != null && spawnedPlayers[i].PlayerId == playerId)
                {
                    player = spawnedPlayers[i];
                    return true;
                }
            }

            player = null;
            return false;
        }

        private UWBPlayerController SpawnPlayer(PlayerDefinition definition, Transform targetParent)
        {
            UWBPlayerController controller = Instantiate(playerPrefab, targetParent);
            controller.gameObject.SetActive(false);
            controller.SetUwbTrackingEnabled(!standaloneSimulationMode);
            controller.Configure(definition.PlayerId, definition.TagId);
            controller.SetStandaloneWorldPosition(definition.InitialPosition);
            controller.SetPlayerScale(playerScale);
            controller.gameObject.SetActive(true);

            // The spawner keeps its own registration so a disabled offline player still receives frames.
            uwbManager?.RegisterTag(controller.TagId);
            return controller;
        }

        private void ClearSpawnedPlayers()
        {
            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                if (spawnedPlayers[i] == null)
                {
                    continue;
                }

                uwbManager?.UnregisterTag(spawnedPlayers[i].TagId);
                spawnedPlayers[i].gameObject.SetActive(false);
                Destroy(spawnedPlayers[i].gameObject);
            }

            spawnedPlayers.Clear();
        }

        private void RefreshSerialPlayerPresence()
        {
            if (standaloneSimulationMode || uwbManager == null || !disableOfflinePlayersInSerial || !uwbManager.IsSerialMode)
            {
                return;
            }

            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                UWBPlayerController player = spawnedPlayers[i];
                if (player == null)
                {
                    continue;
                }

                bool online = uwbManager.IsTagOnline(player.TagId);
                bool allowed = roster == null || roster.Contains(player.PlayerId);
                bool show = online && allowed;
                if (player.gameObject.activeSelf != show)
                {
                    player.gameObject.SetActive(show);
                    if (show && roster == null) Debug.Log($"[UWBPlayerSpawner] Player {player.PlayerId} (tag {player.TagId}) connected", this);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (players == null)
            {
                return;
            }

            var playerIds = new HashSet<int>();
            var tagIds = new HashSet<int>();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerDefinition definition = players[i];
                if (definition == null || !definition.Enabled)
                {
                    continue;
                }

                if (!playerIds.Add(definition.PlayerId))
                {
                    Debug.LogWarning($"[UWBPlayerSpawner] Duplicate Player ID {definition.PlayerId}", this);
                }

                if (!tagIds.Add(definition.TagId))
                {
                    Debug.LogWarning($"[UWBPlayerSpawner] Duplicate Tag ID {definition.TagId}", this);
                }
            }
        }
#endif
    }
}
