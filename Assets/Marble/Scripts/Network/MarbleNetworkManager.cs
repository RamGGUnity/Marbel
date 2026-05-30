using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

namespace Marble
{
    public class RoomInfo
    {
        public string Id;
        public string Name;
        public string HostName;
        public int CurrentPlayers;
        public int MaxPlayers;
    }

    public class MarbleNetworkManager : MonoBehaviour
    {
        private const string RELAY_JOIN_CODE_KEY = "RelayJoinCode";
        private const float LOBBY_HEARTBEAT_INTERVAL = 15f;
        private const float LOBBY_POLL_INTERVAL = 2f;

        [Header("Room Configuration")]
        [SerializeField] private int maxPlayers = 16;
        [SerializeField] private string defaultRoomName = "Marble Race";

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;

        private bool _isConnecting;
        private bool _isServicesInitialized;
        private bool _isServicesInitializing;
        private Task _initializationTask;

        private Lobby _currentLobby;
        private float _heartbeatTimer;
        private float _lobbyPollTimer;
        private List<RoomInfo> _availableRooms = new List<RoomInfo>();

        private string _joinCode;

        public string JoinCode => _joinCode;
        public string CurrentRoomName => _currentLobby?.Name ?? "";
        public string CurrentLobbyId => _currentLobby?.Id ?? "";

        public event Action OnHostStarted;
        public event Action OnClientConnected;
        public event Action OnClientDisconnected;
        public event Action<string> OnConnectionFailed;
        public event Action<ulong> OnPlayerJoined;
        public event Action<ulong> OnPlayerLeft;
        public event Action<List<RoomInfo>> OnRoomsUpdated;

        public bool IsConnected => networkManager != null && networkManager.IsConnectedClient;
        public bool IsHost => networkManager != null && networkManager.IsHost;
        public bool IsServer => networkManager != null && networkManager.IsServer;
        public bool IsClient => networkManager != null && networkManager.IsClient;
        public ulong LocalClientId => networkManager?.LocalClientId ?? 0;
        public List<RoomInfo> AvailableRooms => _availableRooms;

        private void Awake()
        {
            if (networkManager == null)
            {
                networkManager = FindAnyObjectByType<NetworkManager>();
            }
        }

        private void Start()
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback += HandleClientConnected;
                networkManager.OnClientDisconnectCallback += HandleClientDisconnect;
                networkManager.OnServerStarted += HandleServerStarted;
            }
        }

        private void Update()
        {
            HandleLobbyHeartbeat();
        }

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= HandleClientConnected;
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
                networkManager.OnServerStarted -= HandleServerStarted;
            }

            LeaveLobbyAsync();
        }

        private async void HandleLobbyHeartbeat()
        {
            if (_currentLobby == null || !IsHost) return;

            _heartbeatTimer -= Time.deltaTime;
            if (_heartbeatTimer <= 0f)
            {
                _heartbeatTimer = LOBBY_HEARTBEAT_INTERVAL;
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Lobby] Heartbeat failed: {e.Message}");
                }
            }
        }

        private async Task InitializeServicesAsync()
        {
            if (_isServicesInitialized)
            {
                Debug.Log("[Services] Already initialized, skipping.");
                return;
            }

            if (_isServicesInitializing && _initializationTask != null)
            {
                Debug.Log("[Services] Initialization in progress, waiting...");
                await _initializationTask;
                Debug.Log("[Services] Finished waiting for initialization.");
                return;
            }

            Debug.Log("[Services] Starting initialization...");
            _isServicesInitializing = true;
            _initializationTask = InitializeServicesInternalAsync();

            try
            {
                await _initializationTask;
            }
            finally
            {
                _isServicesInitializing = false;
            }
        }

        private async Task InitializeServicesInternalAsync()
        {
            try
            {
                string profile = GetUniqueProfile();
                Debug.Log($"[Services] Initializing with profile: {profile}...");

                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var options = new InitializationOptions();
                    options.SetProfile(profile);
                    await UnityServices.InitializeAsync(options);
                }

                if (!AuthenticationService.Instance.IsSignedIn && !AuthenticationService.Instance.SessionTokenExists)
                {
                    Debug.Log("[Services] Signing in anonymously...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                else if (AuthenticationService.Instance.SessionTokenExists && !AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("[Services] Signing in with existing session...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                Debug.Log($"[Services] Initialized. PlayerId: {AuthenticationService.Instance.PlayerId}");
                _isServicesInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Services] Failed to initialize: {e.Message}");
                throw;
            }
        }

        private string GetUniqueProfile()
        {
#if UNITY_EDITOR
            string projectPath = Application.dataPath;
            if (projectPath.Contains("ParrelSync"))
            {
                return "Clone_" + projectPath.GetHashCode().ToString().Replace("-", "N");
            }
#endif
            return "Default";
        }

        public async void CreateRoom(string roomName = null)
        {
            if (string.IsNullOrEmpty(roomName))
                roomName = defaultRoomName;

            Debug.Log($"[Lobby] Creating room: {roomName}");

            if (networkManager == null)
            {
                OnConnectionFailed?.Invoke("NetworkManager not found");
                return;
            }

            if (IsConnected)
            {
                Debug.LogWarning("[Lobby] Already connected.");
                return;
            }

            _isConnecting = true;

            try
            {
                await InitializeServicesAsync();

                Debug.Log("[Relay] Creating allocation...");
                var allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                _joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log($"[Relay] Join code: {_joinCode}");

                Debug.Log("[Lobby] Creating lobby...");
                var lobbyOptions = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        { RELAY_JOIN_CODE_KEY, new DataObject(DataObject.VisibilityOptions.Public, _joinCode) }
                    },
                    Player = CreatePlayerData()
                };

                _currentLobby = await LobbyService.Instance.CreateLobbyAsync(roomName, maxPlayers, lobbyOptions);
                Debug.Log($"[Lobby] Created: {_currentLobby.Name} (ID: {_currentLobby.Id})");

                Debug.Log($"[Relay] Platform: {Application.platform}, isEditor: {Application.isEditor}");
                var transport = networkManager.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    Debug.Log($"[Relay] Transport found. UseWebSockets BEFORE: {transport.UseWebSockets}");

                    transport.UseWebSockets = true;
                    Debug.Log($"[Relay] UseWebSockets AFTER: {transport.UseWebSockets}");

                    foreach (var endpoint in allocation.ServerEndpoints)
                    {
                        Debug.Log($"[Relay] Available endpoint: {endpoint.ConnectionType} - {endpoint.Host}:{endpoint.Port}");
                    }

                    Debug.Log("[Relay] Creating RelayServerData with 'wss' connection type...");
                    var relayServerData = new Unity.Networking.Transport.Relay.RelayServerData(allocation, "wss");
                    Debug.Log($"[Relay] RelayServerData created. Endpoint: {relayServerData.Endpoint}");
                    transport.SetRelayServerData(relayServerData);
                    Debug.Log($"[Relay] Host relay data set successfully");
                    Debug.Log($"[Relay] Transport protocol: {transport.Protocol}");
                }
                else
                {
                    Debug.LogError("[Relay] UnityTransport component not found!");
                }

                bool success = networkManager.StartHost();
                if (!success)
                {
                    _isConnecting = false;
                    await DeleteLobbyAsync();
                    OnConnectionFailed?.Invoke("Failed to start host");
                }
            }
            catch (Exception e)
            {
                _isConnecting = false;
                _joinCode = null;
                await DeleteLobbyAsync();
                Debug.LogError($"[Lobby] Failed to create room: {e.Message}");
                OnConnectionFailed?.Invoke($"Failed to create room: {e.Message}");
            }
        }

        public async void RefreshRoomList()
        {
            try
            {
                await InitializeServicesAsync();

                Debug.Log("[Lobby] Querying available rooms...");
                var queryOptions = new QueryLobbiesOptions
                {
                    Count = 25,
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                    }
                };

                var response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

                _availableRooms.Clear();
                foreach (var lobby in response.Results)
                {
                    _availableRooms.Add(new RoomInfo
                    {
                        Id = lobby.Id,
                        Name = lobby.Name,
                        HostName = lobby.Players.Count > 0 ? lobby.Players[0].Id : "Unknown",
                        CurrentPlayers = lobby.Players.Count,
                        MaxPlayers = lobby.MaxPlayers
                    });
                }

                Debug.Log($"[Lobby] Found {_availableRooms.Count} rooms");
                OnRoomsUpdated?.Invoke(_availableRooms);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Lobby] Failed to query rooms: {e.Message}");
                _availableRooms.Clear();
                OnRoomsUpdated?.Invoke(_availableRooms);
            }
        }

        public async void JoinRoom(string lobbyId)
        {
            Debug.Log($"[Lobby] Joining room: {lobbyId}");

            if (networkManager == null)
            {
                OnConnectionFailed?.Invoke("NetworkManager not found");
                return;
            }

            if (IsConnected)
            {
                Debug.LogWarning("[Lobby] Already connected.");
                return;
            }

            _isConnecting = true;

            try
            {
                await InitializeServicesAsync();

                var joinOptions = new JoinLobbyByIdOptions
                {
                    Player = CreatePlayerData()
                };
                _currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, joinOptions);
                Debug.Log($"[Lobby] Joined: {_currentLobby.Name}");

                if (!_currentLobby.Data.TryGetValue(RELAY_JOIN_CODE_KEY, out var joinCodeData))
                {
                    throw new Exception("Lobby missing relay join code");
                }

                string relayJoinCode = joinCodeData.Value;
                Debug.Log($"[Relay] Join code from lobby: {relayJoinCode}");

                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

                Debug.Log($"[Relay] Platform: {Application.platform}, isEditor: {Application.isEditor}");
                var transport = networkManager.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    Debug.Log($"[Relay] Transport found. UseWebSockets BEFORE: {transport.UseWebSockets}");

                    transport.UseWebSockets = true;
                    Debug.Log($"[Relay] UseWebSockets AFTER: {transport.UseWebSockets}");

                    foreach (var endpoint in joinAllocation.ServerEndpoints)
                    {
                        Debug.Log($"[Relay] Available endpoint: {endpoint.ConnectionType} - {endpoint.Host}:{endpoint.Port}");
                    }

                    Debug.Log("[Relay] Creating RelayServerData with 'wss' connection type...");
                    var relayServerData = new Unity.Networking.Transport.Relay.RelayServerData(joinAllocation, "wss");
                    Debug.Log($"[Relay] RelayServerData created. Endpoint: {relayServerData.Endpoint}");
                    transport.SetRelayServerData(relayServerData);
                    Debug.Log($"[Relay] Client relay data set successfully");
                    Debug.Log($"[Relay] Transport protocol: {transport.Protocol}");
                }
                else
                {
                    Debug.LogError("[Relay] UnityTransport component not found!");
                }

                bool success = networkManager.StartClient();
                if (!success)
                {
                    _isConnecting = false;
                    await LeaveLobbyAsync();
                    OnConnectionFailed?.Invoke("Failed to start client");
                }
            }
            catch (Exception e)
            {
                _isConnecting = false;
                await LeaveLobbyAsync();
                Debug.LogError($"[Lobby] Failed to join room: {e.Message}");
                OnConnectionFailed?.Invoke($"Failed to join room: {e.Message}");
            }
        }

        public void JoinRoomByIndex(int index)
        {
            if (index >= 0 && index < _availableRooms.Count)
            {
                JoinRoom(_availableRooms[index].Id);
            }
        }

        public async void StartClient(string joinCode)
        {
            Debug.Log($"[Relay] StartClient with code: {joinCode}");

            if (networkManager == null)
            {
                OnConnectionFailed?.Invoke("NetworkManager not found");
                return;
            }

            if (IsConnected || string.IsNullOrEmpty(joinCode))
            {
                OnConnectionFailed?.Invoke("Already connected or invalid code");
                return;
            }

            _isConnecting = true;

            try
            {
                await InitializeServicesAsync();

                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.ToUpper().Trim());

                Debug.Log($"[Relay] Platform: {Application.platform}, isEditor: {Application.isEditor}");
                var transport = networkManager.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    Debug.Log($"[Relay] Transport found. UseWebSockets BEFORE: {transport.UseWebSockets}");

                    transport.UseWebSockets = true;
                    Debug.Log($"[Relay] UseWebSockets AFTER: {transport.UseWebSockets}");

                    foreach (var endpoint in joinAllocation.ServerEndpoints)
                    {
                        Debug.Log($"[Relay] Available endpoint: {endpoint.ConnectionType} - {endpoint.Host}:{endpoint.Port}");
                    }

                    Debug.Log("[Relay] Creating RelayServerData with 'wss' connection type...");
                    var relayServerData = new Unity.Networking.Transport.Relay.RelayServerData(joinAllocation, "wss");
                    Debug.Log($"[Relay] RelayServerData created. Endpoint: {relayServerData.Endpoint}");
                    transport.SetRelayServerData(relayServerData);
                    Debug.Log($"[Relay] Direct client relay data set successfully");
                    Debug.Log($"[Relay] Transport protocol: {transport.Protocol}");
                }
                else
                {
                    Debug.LogError("[Relay] UnityTransport component not found!");
                }

                if (!networkManager.StartClient())
                {
                    _isConnecting = false;
                    OnConnectionFailed?.Invoke("Failed to start client");
                }
            }
            catch (Exception e)
            {
                _isConnecting = false;
                Debug.LogError($"[Relay] Failed: {e.Message}");
                OnConnectionFailed?.Invoke($"Relay error: {e.Message}");
            }
        }

        public void StartHost()
        {
            CreateRoom(defaultRoomName);
        }

        private Player CreatePlayerData()
        {
            return new Player
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "Player") }
                }
            };
        }

        public async void Disconnect()
        {
            if (networkManager != null && (networkManager.IsHost || networkManager.IsClient))
            {
                networkManager.Shutdown();
            }

            await LeaveLobbyAsync();

            _isConnecting = false;
            _joinCode = null;
        }

        private async Task LeaveLobbyAsync()
        {
            if (_currentLobby == null) return;

            try
            {
                string lobbyId = _currentLobby.Id;
                _currentLobby = null;
                await LobbyService.Instance.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId);
                Debug.Log("[Lobby] Left lobby");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Failed to leave: {e.Message}");
            }
        }

        private async Task DeleteLobbyAsync()
        {
            if (_currentLobby == null) return;

            try
            {
                string lobbyId = _currentLobby.Id;
                _currentLobby = null;
                await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                Debug.Log("[Lobby] Deleted lobby");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Failed to delete: {e.Message}");
            }
        }

        private void HandleServerStarted()
        {
            _isConnecting = false;
            Debug.Log($"[Network] Server started. Room: {CurrentRoomName}");
            OnHostStarted?.Invoke();
        }

        private void HandleClientConnected(ulong clientId)
        {
            _isConnecting = false;

            if (clientId == networkManager.LocalClientId)
            {
                Debug.Log($"[Network] Connected to server. ClientId: {clientId}");
                OnClientConnected?.Invoke();
            }
            else
            {
                Debug.Log($"[Network] Player joined. ClientId: {clientId}");
                OnPlayerJoined?.Invoke(clientId);
            }
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            if (clientId == networkManager.LocalClientId)
            {
                Debug.Log("[Network] Disconnected from server.");
                _isConnecting = false;
                OnClientDisconnected?.Invoke();
            }
            else
            {
                Debug.Log($"[Network] Player left. ClientId: {clientId}");
                OnPlayerLeft?.Invoke(clientId);
            }
        }

        public int GetConnectedClientCount()
        {
            if (!IsServer) return 0;
            return networkManager.ConnectedClients.Count;
        }

        public ulong[] GetConnectedClientIds()
        {
            if (!IsServer) return Array.Empty<ulong>();

            var ids = new ulong[networkManager.ConnectedClients.Count];
            int i = 0;
            foreach (var kvp in networkManager.ConnectedClients)
            {
                ids[i++] = kvp.Key;
            }
            return ids;
        }

        public float GetServerTime()
        {
            if (networkManager == null) return Time.time;
            return networkManager.ServerTime.TimeAsFloat;
        }

        public bool IsClientConnected(ulong clientId)
        {
            if (!IsServer) return false;
            return networkManager.ConnectedClients.ContainsKey(clientId);
        }
    }
}
