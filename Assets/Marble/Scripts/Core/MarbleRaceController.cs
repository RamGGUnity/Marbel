using System;
using UnityEngine;

namespace Marble
{
    public class MarbleRaceController : MonoBehaviour
    {
        [Header("Race Settings")]
        [SerializeField, Range(1, 16)] private int marbleCount = 10;
        [SerializeField] private bool randomizeMarbles = true;

        [Header("References")]
        [SerializeField] private MarbleTrack track;
        [SerializeField] private Transform marbleContainer;
        [SerializeField] private MarbleSpawnPoints spawnPoints;

        [Header("Prefabs")]
        [SerializeField] private GameObject marblePhysicsPrefab;

        private RaceStateMachine _raceStateMachine;
        private RacePositionTracker _positionTracker;
        private MarbleNetworkSync _networkSync;
        private MarbleNetworkManager _networkManager;
        private MarbleLateJoinHandler _lateJoinHandler;

        private MarblePhysicsController[] _physicsControllers;
        private MarblePhysicsVisual[] _physicsVisuals;

        private bool _isInitialized;
        private bool _isServer;
        private bool _marblesSpawned;
        private float _raceTime;
        private byte[] _selectedMarbleIds;

        public event Action OnRaceReady;
        public event Action OnRaceStarted;
        public event Action<byte, int, float> OnMarbleFinished;
        public event Action OnAllMarblesFinished;

        public int ActiveMarbleCount => MarbleConstants.MarbleCount;
        public byte[] SelectedCountryIds => _selectedMarbleIds;
        public bool IsRacing => _raceStateMachine?.IsRacing ?? false;
        public RacePhase CurrentPhase => _raceStateMachine?.CurrentPhase ?? RacePhase.Lobby;
        public MarblePhysicsController[] PhysicsControllers => _physicsControllers;
        public MarblePhysicsVisual[] PhysicsVisuals => _physicsVisuals;
        public RaceStateMachine StateMachine => _raceStateMachine;
        public RacePositionTracker PositionTracker => _positionTracker;
        public MarbleNetworkSync NetworkSync => _networkSync;
        public MarbleTrack Track => track;

        public void Initialize(
            MarbleNetworkManager netManager,
            MarbleNetworkSync netSync,
            MarbleTrack trackRef = null)
        {
            _networkManager = netManager;
            _networkSync = netSync;

            MarbleConstants.MarbleCount = Mathf.Clamp(marbleCount, 1, MarbleConstants.MaxMarbleCount);

            if (trackRef != null)
            {
                track = trackRef;
            }

            if (track != null)
            {
                track.Initialize();
            }

            _isServer = netManager?.IsServer ?? true;

            InitializeStateMachine();
            InitializePositionTracker();

            if (_isServer)
            {
                Debug.Log("[RaceController] Initializing as SERVER - spawning marbles now");
                SpawnMarbles();
                InitializeLateJoinHandler();
            }
            else
            {
                Debug.Log("[RaceController] Initializing as CLIENT - waiting for init data from server");
                if (_networkSync != null)
                {
                    Debug.Log("[RaceController] Subscribing to OnClientInitDataReceived");
                    _networkSync.OnClientInitDataReceived += HandleClientInitDataReceived;
                }
                else
                {
                    Debug.LogError("[RaceController] _networkSync is NULL on client!");
                }
            }

            SubscribeToEvents();

            _isInitialized = true;

            if (_isServer)
            {
                OnRaceReady?.Invoke();
            }
        }

        public void InitializeAsServer(
            MarbleNetworkManager netManager,
            MarbleNetworkSync netSync,
            MarbleTrack trackRef = null)
        {
            if (_isInitialized && _isServer && _marblesSpawned)
            {
                Debug.Log("[MarbleRaceController] Already initialized as server.");
                return;
            }

            _networkManager = netManager;
            _networkSync = netSync;
            _isServer = true;

            if (trackRef != null)
            {
                track = trackRef;
            }

            MarbleConstants.MarbleCount = Mathf.Clamp(marbleCount, 1, MarbleConstants.MaxMarbleCount);

            if (track != null)
            {
                track.Initialize();
            }

            if (_raceStateMachine == null)
            {
                InitializeStateMachine();
            }
            else
            {
                _raceStateMachine.SetServerStatus(true);
            }

            if (_positionTracker == null)
            {
                InitializePositionTracker();
            }

            if (!_marblesSpawned)
            {
                SpawnMarbles();
                InitializeLateJoinHandler();
            }

            if (!_isInitialized)
            {
                SubscribeToEvents();
            }

            _isInitialized = true;

            Debug.Log($"[MarbleRaceController] Initialized as server. Marbles: {MarbleConstants.MarbleCount}");
            OnRaceReady?.Invoke();
        }

        private void InitializeStateMachine()
        {
            _raceStateMachine = gameObject.AddComponent<RaceStateMachine>();
            _raceStateMachine.Initialize(_isServer);
        }

        private void InitializePositionTracker()
        {
            _positionTracker = gameObject.AddComponent<RacePositionTracker>();
        }

        private void SpawnMarbles()
        {
            _selectedMarbleIds = SelectMarbles();

            _physicsControllers = new MarblePhysicsController[MarbleConstants.MarbleCount];
            _physicsVisuals = new MarblePhysicsVisual[MarbleConstants.MarbleCount];

            for (int i = 0; i < MarbleConstants.MarbleCount; i++)
            {
                byte countryId = _selectedMarbleIds[i];
                SpawnMarble((byte)i, countryId);
            }

            _positionTracker.Initialize(_physicsControllers);

            if (_networkSync != null)
            {
                if (_isServer)
                {
                    _networkSync.InitializeServer(_physicsControllers);
                }
                else
                {
                    _networkSync.InitializeClient(_physicsVisuals);
                }
            }

            _marblesSpawned = true;
        }

        private byte[] SelectMarbles()
        {
            var selected = new byte[MarbleConstants.MarbleCount];

            if (randomizeMarbles)
            {
                var available = new System.Collections.Generic.List<byte>();
                for (int i = 0; i < MarbleConstants.MaxMarbleCount; i++)
                {
                    available.Add((byte)i);
                }

                for (int i = available.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    (available[i], available[j]) = (available[j], available[i]);
                }

                for (int i = 0; i < MarbleConstants.MarbleCount; i++)
                {
                    selected[i] = available[i];
                }
            }
            else
            {
                for (int i = 0; i < MarbleConstants.MarbleCount; i++)
                {
                    selected[i] = (byte)i;
                }
            }

            return selected;
        }

        private void SpawnMarble(byte slotIndex, byte countryId)
        {
            Debug.Log($"[RaceController] SpawnMarble: slotIndex={slotIndex}, countryId={countryId}, isServer={_isServer}");

            Vector3 spawnPos;
            Quaternion spawnRot;

            if (spawnPoints != null)
            {
                spawnPos = spawnPoints.GetSpawnPosition(slotIndex);
                spawnRot = spawnPoints.GetSpawnRotation(slotIndex);
            }
            else if (track != null && track.CheckpointCount > 0)
            {
                var firstCheckpoint = track.GetCheckpoint(0);
                spawnPos = firstCheckpoint.Position + Vector3.up * 0.5f;
                spawnRot = Quaternion.LookRotation(firstCheckpoint.Forward);
            }
            else
            {
                spawnPos = Vector3.up * 0.5f;
                spawnRot = Quaternion.identity;
            }

            Debug.Log($"[RaceController] Instantiating marble prefab at {spawnPos}. Prefab null: {marblePhysicsPrefab == null}, Container null: {marbleContainer == null}");

            GameObject marbleGO = Instantiate(marblePhysicsPrefab, spawnPos, spawnRot, marbleContainer);
            Debug.Log($"[RaceController] Marble instantiated: {marbleGO.name}");

            var rb = marbleGO.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            string countryName = spawnPoints != null ? spawnPoints.GetName(countryId) : $"Marble_{countryId}";
            marbleGO.name = countryName;
            marbleGO.tag = MarbleConstants.MarbleTag;

            if (_isServer)
            {
                var controller = marbleGO.GetComponent<MarblePhysicsController>();
                if (controller == null)
                {
                    controller = marbleGO.AddComponent<MarblePhysicsController>();
                }
                controller.Initialize(slotIndex, track, spawnPos, spawnRot);
                _physicsControllers[slotIndex] = controller;

                controller.OnLapCompleted += HandleLapCompleted;
                controller.OnFinished += HandleMarbleFinished;
            }
            else
            {
                Debug.Log($"[RaceController] Adding MarblePhysicsVisual to marble {slotIndex}");
                var visual = marbleGO.GetComponent<MarblePhysicsVisual>();
                if (visual == null)
                {
                    visual = marbleGO.AddComponent<MarblePhysicsVisual>();
                }
                visual.Initialize(slotIndex, track);
                _physicsVisuals[slotIndex] = visual;

                var meshFilter = marbleGO.GetComponent<MeshFilter>();
                var meshRenderer = marbleGO.GetComponent<MeshRenderer>();
                Debug.Log($"[RaceController] Marble {slotIndex} visual state: MeshFilter={meshFilter != null}, MeshRenderer={meshRenderer != null}, RendererEnabled={meshRenderer?.enabled ?? false}, HasMaterial={meshRenderer?.material != null}");
            }

            ApplyMarbleMaterial(marbleGO, countryId);

            Debug.Log($"[RaceController] Marble {slotIndex} spawn complete");
        }

        private void SpawnClientMarbles(byte[] selectedIds)
        {
            Debug.Log($"[RaceController] SpawnClientMarbles: MarbleCount={MarbleConstants.MarbleCount}, selectedIds.Length={selectedIds?.Length ?? 0}");

            _physicsControllers = new MarblePhysicsController[MarbleConstants.MarbleCount];
            _physicsVisuals = new MarblePhysicsVisual[MarbleConstants.MarbleCount];

            for (int i = 0; i < MarbleConstants.MarbleCount && i < selectedIds.Length; i++)
            {
                byte countryId = selectedIds[i];
                Debug.Log($"[RaceController] Spawning marble {i} with countryId {countryId}");
                SpawnMarble((byte)i, countryId);
            }

            if (_networkSync != null)
            {
                Debug.Log("[RaceController] Initializing network sync with visuals");
                _networkSync.InitializeClient(_physicsVisuals);
            }

            if (_positionTracker != null)
            {
                Debug.Log("[RaceController] Initializing position tracker for client");
                _positionTracker.InitializeClient(_physicsVisuals);
            }

            Debug.Log($"[RaceController] Client spawned {MarbleConstants.MarbleCount} marbles successfully!");

            for (int i = 0; i < _physicsVisuals.Length; i++)
            {
                if (_physicsVisuals[i] != null)
                {
                    var renderer = _physicsVisuals[i].GetComponent<Renderer>();
                    Debug.Log($"[RaceController] Marble {i}: Position={_physicsVisuals[i].transform.position}, HasRenderer={renderer != null}, RendererEnabled={renderer?.enabled ?? false}");
                }
            }
        }

        private void ApplyMarbleMaterial(GameObject marbleGO, byte marbleId)
        {
            var renderer = marbleGO.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"[RaceController] No Renderer found on marble {marbleId}!");
                return;
            }

            if (spawnPoints != null)
            {
                Material mat = spawnPoints.GetMaterial(marbleId);
                if (mat != null)
                {
                    renderer.material = mat;
                    Debug.Log($"[RaceController] Applied material '{mat.name}' to marble {marbleId}");
                    return;
                }
                else
                {
                    Debug.LogWarning($"[RaceController] No material for marble {marbleId}! marbles.Count={spawnPoints.marbles.Count}. Run menu: Marble > Assign Materials to SpawnPoints");
                }
            }
            else
            {
                Debug.LogWarning($"[RaceController] SpawnPoints is null! Cannot apply material to marble {marbleId}");
            }
        }

        private void InitializeLateJoinHandler()
        {
            _lateJoinHandler = gameObject.AddComponent<MarbleLateJoinHandler>();
            _lateJoinHandler.Initialize(
                _networkManager,
                _networkSync,
                _raceStateMachine,
                _positionTracker,
                _physicsControllers,
                _physicsVisuals
            );
        }

        private void SubscribeToEvents()
        {
            if (_raceStateMachine != null)
            {
                _raceStateMachine.OnPhaseChanged += HandlePhaseChanged;
                _raceStateMachine.OnCountdownTick += HandleCountdownTick;
                _raceStateMachine.OnRaceStarted += HandleRaceStarted;
                _raceStateMachine.OnRaceFinished += HandleRaceFinished;
            }

            if (!_isServer && _networkSync != null)
            {
                _networkSync.OnRacePhaseChanged += HandleNetworkPhaseChanged;
            }
        }

        private void HandleNetworkPhaseChanged(RacePhase phase, float serverTime)
        {
            if (_raceStateMachine != null)
            {
                _raceStateMachine.ForcePhase(phase);
            }

            if (phase == RacePhase.Racing)
            {
                HandleRaceStarted();
            }
            else if (phase == RacePhase.Finished)
            {
                HandleRaceFinished();
            }
        }

        private void OnDestroy()
        {
            if (_raceStateMachine != null)
            {
                _raceStateMachine.OnPhaseChanged -= HandlePhaseChanged;
                _raceStateMachine.OnCountdownTick -= HandleCountdownTick;
                _raceStateMachine.OnRaceStarted -= HandleRaceStarted;
                _raceStateMachine.OnRaceFinished -= HandleRaceFinished;
            }

            if (_networkSync != null)
            {
                _networkSync.OnClientInitDataReceived -= HandleClientInitDataReceived;
                _networkSync.OnRacePhaseChanged -= HandleNetworkPhaseChanged;
            }

            if (_physicsControllers != null)
            {
                foreach (var controller in _physicsControllers)
                {
                    if (controller != null)
                    {
                        controller.OnLapCompleted -= HandleLapCompleted;
                        controller.OnFinished -= HandleMarbleFinished;
                    }
                }
            }
        }

        private void Update()
        {
            if (!_isInitialized || !_isServer) return;

            if (_raceStateMachine.IsRacing)
            {
                _raceTime = _raceStateMachine.RaceElapsedTime;
            }
        }

        #region Public API

        public void StartCountdown()
        {
            bool isServer = _networkManager != null ? _networkManager.IsServer : _isServer;

            if (!isServer)
            {
                return;
            }

            if (!_isServer && isServer)
            {
                _isServer = isServer;
                ReinitializeForServer();
            }
            else
            {
                _isServer = isServer;
            }

            _raceStateMachine?.SetServerStatus(isServer);
            _raceStateMachine.StartCountdown();
        }

        private void ReinitializeForServer()
        {
            if (_physicsControllers == null)
            {
                _physicsControllers = new MarblePhysicsController[MarbleConstants.MarbleCount];
            }

            for (int i = 0; i < MarbleConstants.MarbleCount; i++)
            {
                byte marbleId = (byte)i;
                GameObject marbleGO = null;

                if (_physicsVisuals != null && _physicsVisuals[i] != null)
                {
                    marbleGO = _physicsVisuals[i].gameObject;
                }
                else if (marbleContainer != null)
                {
                    var child = marbleContainer.Find($"Marble_{marbleId}");
                    if (child != null)
                    {
                        marbleGO = child.gameObject;
                    }
                }

                if (marbleGO == null)
                {
                    continue;
                }

                var controller = marbleGO.GetComponent<MarblePhysicsController>();
                if (controller == null)
                {
                    controller = marbleGO.AddComponent<MarblePhysicsController>();
                }

                Vector3 spawnPos = spawnPoints != null ? spawnPoints.GetSpawnPosition(marbleId) : marbleGO.transform.position;
                Quaternion spawnRot = spawnPoints != null ? spawnPoints.GetSpawnRotation(marbleId) : marbleGO.transform.rotation;
                controller.Initialize(marbleId, track, spawnPos, spawnRot);
                _physicsControllers[marbleId] = controller;

                var visual = marbleGO.GetComponent<MarblePhysicsVisual>();
                if (visual != null)
                {
                    visual.enabled = false;
                }

                controller.OnLapCompleted += HandleLapCompleted;
                controller.OnFinished += HandleMarbleFinished;
            }

            _positionTracker?.Initialize(_physicsControllers);
            _networkSync?.InitializeServer(_physicsControllers);
        }

        public void ResetRace()
        {
            _raceStateMachine.Reset();
            _raceTime = 0f;

            if (_physicsControllers != null)
            {
                foreach (var controller in _physicsControllers)
                {
                    controller?.Reset();
                }
            }

            if (_physicsVisuals != null)
            {
                foreach (var visual in _physicsVisuals)
                {
                    visual?.Reset();
                }
            }

            _positionTracker?.ForceUpdate();
        }

        public float GetRaceTime()
        {
            return _raceTime;
        }

        public RankingData[] GetRankingData()
        {
            return _positionTracker?.GetRankingData() ?? Array.Empty<RankingData>();
        }

        #endregion

        #region Event Handlers

        private void HandleClientInitDataReceived(ClientInitData initData)
        {
            Debug.Log($"[RaceController] HandleClientInitDataReceived called! _marblesSpawned: {_marblesSpawned}");
            if (_marblesSpawned)
            {
                Debug.Log("[RaceController] Marbles already spawned, skipping");
                return;
            }

            MarbleConstants.MarbleCount = initData.MarbleCount;

            var selectedIds = initData.GetSelectedIds();
            Debug.Log($"[RaceController] Client received init data: {initData.MarbleCount} marbles, phase {(RacePhase)initData.CurrentPhase}, IDs: {string.Join(", ", selectedIds)}");

            _selectedMarbleIds = selectedIds;
            Debug.Log("[RaceController] About to spawn client marbles...");
            SpawnClientMarbles(selectedIds);
            Debug.Log("[RaceController] Client marbles spawned, initializing late join handler...");
            InitializeLateJoinHandler();

            RacePhase currentPhase = (RacePhase)initData.CurrentPhase;
            if (_raceStateMachine != null)
            {
                _raceStateMachine.ForcePhase(currentPhase);
            }

            _marblesSpawned = true;

            if (currentPhase == RacePhase.Racing)
            {
                if (_physicsVisuals != null)
                {
                    foreach (var visual in _physicsVisuals)
                    {
                        visual?.StartRacing();
                    }
                }
            }

            OnRaceReady?.Invoke();
        }

        private void HandlePhaseChanged(RacePhase phase)
        {
            if (!_isServer) return;
            _networkSync?.ServerNotifyPhaseChange(phase);
        }

        private void HandleCountdownTick(int seconds)
        {
            if (!_isServer) return;
            _networkSync?.ServerNotifyCountdown(seconds);
        }

        private void HandleRaceStarted()
        {
            if (_isServer && _physicsControllers != null)
            {
                foreach (var controller in _physicsControllers)
                {
                    controller?.StartRacing();
                }
            }
            else if (!_isServer && _physicsVisuals != null)
            {
                foreach (var visual in _physicsVisuals)
                {
                    visual?.StartRacing();
                }
            }

            OnRaceStarted?.Invoke();
        }

        private void HandleRaceFinished()
        {
            if (_isServer && _physicsControllers != null)
            {
                foreach (var controller in _physicsControllers)
                {
                    controller?.StopRacing();
                }
            }
            else if (!_isServer && _physicsVisuals != null)
            {
                foreach (var visual in _physicsVisuals)
                {
                    visual?.StopRacing();
                }
            }

            OnAllMarblesFinished?.Invoke();
        }

        private void HandleLapCompleted(byte marbleId, byte newLapCount)
        {
            _networkSync?.ServerNotifyLapComplete(marbleId, newLapCount);
        }

        private void HandleMarbleFinished(byte marbleId, int position, float finishTime)
        {
            int actualPosition = _raceStateMachine.RecordFinish();
            _physicsControllers[marbleId]?.SetFinishPosition(actualPosition);

            _networkSync?.ServerNotifyMarbleFinished(marbleId, (byte)actualPosition, finishTime);
            OnMarbleFinished?.Invoke(marbleId, actualPosition, finishTime);
        }

        #endregion
    }
}
