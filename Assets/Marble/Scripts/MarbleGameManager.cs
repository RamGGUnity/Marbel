using UnityEngine;
using Unity.Netcode;

namespace Marble
{
    public class MarbleGameManager : MonoBehaviour
    {
        public static MarbleGameManager Instance { get; private set; }

        [Header("Track Configuration")]
        [SerializeField] private MarbleTrack track;

        [Header("Network")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Subsystems (assign in scene)")]
        [SerializeField] private MarbleNetworkManager marbleNetworkManager;
        [SerializeField] private MarbleNetworkSync marbleNetworkSync;
        [SerializeField] private MarbleRaceController raceController;
        [SerializeField] private MarbleRaceUI raceUI;

        private bool _isInitialized;

        public MarbleTrack Track => track;
        public MarbleRaceController RaceController => raceController;
        public MarbleNetworkManager NetworkManager => marbleNetworkManager;
        public MarbleNetworkSync NetworkSync => marbleNetworkSync;
        public MarbleRaceUI RaceUI => raceUI;
        public bool IsInitialized => _isInitialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Initialize()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("MarbleGameManager already initialized.");
                return;
            }

            Debug.Log("Initializing MarbleGameManager...");

            InitializeTrack();

            InitializeNetworking();

            InitializeRaceController();

            InitializeUI();

            SubscribeToNetworkEvents();

            _isInitialized = true;
            Debug.Log("MarbleGameManager initialization complete.");
        }

        private void InitializeTrack()
        {
            track.Initialize();
            Debug.Log($"Track initialized. Checkpoints: {track.CheckpointCount}, Length: {track.TotalLength:F2}m");
        }

        private void InitializeNetworking()
        {
            if (marbleNetworkManager == null)
            {
                Debug.LogError("MarbleNetworkManager not assigned! Add it to the scene and assign in Inspector.");
                return;
            }

            if (marbleNetworkSync == null)
            {
                Debug.LogError("MarbleNetworkSync not assigned! Add it to the scene and assign in Inspector.");
                return;
            }

            if (networkManager == null)
            {
                Debug.LogWarning("Unity NetworkManager not assigned. Network features may not work.");
            }

            Debug.Log("Network components validated.");
        }

        private void InitializeRaceController()
        {
            if (raceController == null)
            {
                Debug.LogError("MarbleRaceController not assigned! Add it to the scene and assign in Inspector.");
                return;
            }

            raceController.Initialize(marbleNetworkManager, marbleNetworkSync, track);
        }

        private void InitializeUI()
        {
            if (raceUI == null)
            {
                Debug.LogWarning("MarbleRaceUI not assigned. UI features disabled.");
                return;
            }

            raceUI.Initialize(raceController, marbleNetworkManager, marbleNetworkSync);
        }

        private void SubscribeToNetworkEvents()
        {
            if (marbleNetworkManager == null) return;

            marbleNetworkManager.OnHostStarted += HandleHostStarted;
            marbleNetworkManager.OnClientConnected += HandleClientConnected;
            marbleNetworkManager.OnClientDisconnected += HandleClientDisconnected;
            marbleNetworkManager.OnPlayerJoined += HandlePlayerJoined;
        }

        private void OnApplicationQuit()
        {
            if (marbleNetworkManager != null)
            {
                marbleNetworkManager.Disconnect();
            }
        }

        #region Public API

        public void StartHost()
        {
            if (marbleNetworkManager == null) return;
            marbleNetworkManager.StartHost();
        }

        public void JoinGame(string joinCode)
        {
            if (marbleNetworkManager == null) return;
            marbleNetworkManager.StartClient(joinCode);
        }

        public void Disconnect()
        {
            if (marbleNetworkManager != null)
            {
                marbleNetworkManager.Disconnect();
            }
        }

        public void StartRace()
        {
            if (raceController != null)
            {
                raceController.StartCountdown();
            }
        }

        public void ResetRace()
        {
            if (raceController != null)
            {
                raceController.ResetRace();
            }
        }

        public RacePhase GetRacePhase()
        {
            if (raceController == null) return RacePhase.Lobby;
            return raceController.CurrentPhase;
        }

        public bool IsHost => marbleNetworkManager != null && marbleNetworkManager.IsHost;
        public bool IsConnected => marbleNetworkManager != null && marbleNetworkManager.IsConnected;

        #endregion

        #region Event Handlers

        private void HandleHostStarted()
        {
            Debug.Log("Host started. Spawning network objects...");

            if (marbleNetworkSync != null)
            {
                var netObj = marbleNetworkSync.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsSpawned)
                {
                    netObj.Spawn();
                    Debug.Log("MarbleNetworkSync spawned.");
                }
            }

            if (raceController != null)
            {
                raceController.InitializeAsServer(marbleNetworkManager, marbleNetworkSync, track);
            }
        }

        private void HandleClientConnected()
        {
            Debug.Log("Connected to host.");
        }

        private void HandleClientDisconnected()
        {
            Debug.Log("Disconnected from network.");

            ResetRace();
            if (raceUI != null)
            {
                raceUI.ShowLobby();
            }
        }

        private void HandlePlayerJoined(ulong clientId)
        {
            Debug.Log($"Player {clientId} joined. Sending initialization data...");

            if (marbleNetworkSync != null && raceController != null)
            {
                var selectedIds = raceController.SelectedCountryIds;
                if (selectedIds != null && selectedIds.Length > 0)
                {
                    var initData = new ClientInitData
                    {
                        MarbleCount = (byte)MarbleConstants.MarbleCount,
                        CurrentPhase = (byte)raceController.CurrentPhase,
                        RaceTime = raceController.GetRaceTime(),
                        ServerTime = marbleNetworkManager.GetServerTime()
                    };
                    initData.SetSelectedIds(selectedIds);

                    marbleNetworkSync.ServerSendInitDataToClient(clientId, initData);
                    Debug.Log($"Sent init data to client {clientId}: {MarbleConstants.MarbleCount} marbles, phase {raceController.CurrentPhase}");
                }
            }
        }

        #endregion

        #region Debug

        [ContextMenu("Start Host (Debug)")]
        private void DebugStartHost()
        {
            StartHost();
        }

        [ContextMenu("Start Client (Debug)")]
        private void DebugStartClient()
        {
            Debug.Log("Cannot start client from debug menu - join code required. Use the UI.");
        }

        [ContextMenu("Start Race (Debug)")]
        private void DebugStartRace()
        {
            StartRace();
        }

        [ContextMenu("Reset Race (Debug)")]
        private void DebugResetRace()
        {
            ResetRace();
        }

        #endregion
    }
}
