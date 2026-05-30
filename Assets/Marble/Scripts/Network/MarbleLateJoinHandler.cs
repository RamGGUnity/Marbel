using UnityEngine;
using Unity.Netcode;

namespace Marble
{
    public class MarbleLateJoinHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MarbleNetworkManager networkManager;
        [SerializeField] private MarbleNetworkSync networkSync;
        [SerializeField] private RaceStateMachine raceStateMachine;
        [SerializeField] private RacePositionTracker positionTracker;
        [SerializeField] private MarblePhysicsController[] physicsControllers;
        [SerializeField] private MarblePhysicsVisual[] physicsVisuals;

        private bool _isInitialized;

        public void Initialize(
            MarbleNetworkManager netManager,
            MarbleNetworkSync netSync,
            RaceStateMachine stateMachine,
            RacePositionTracker tracker,
            MarblePhysicsController[] controllers,
            MarblePhysicsVisual[] visuals)
        {
            networkManager = netManager;
            networkSync = netSync;
            raceStateMachine = stateMachine;
            positionTracker = tracker;
            physicsControllers = controllers;
            physicsVisuals = visuals;

            if (networkManager != null)
            {
                networkManager.OnPlayerJoined += HandlePlayerJoined;
            }

            if (networkSync != null)
            {
                networkSync.OnSnapshotReceived += HandleSnapshotReceived;
            }

            _isInitialized = true;
        }

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnPlayerJoined -= HandlePlayerJoined;
            }

            if (networkSync != null)
            {
                networkSync.OnSnapshotReceived -= HandleSnapshotReceived;
            }
        }

        private void HandlePlayerJoined(ulong clientId)
        {
            if (!networkManager.IsServer) return;

            Debug.Log($"Late joiner detected: {clientId}. Sending snapshot...");

            RaceSnapshot snapshot = CreateSnapshot();
            networkSync.SendSnapshotToClient(clientId, snapshot);

            Debug.Log($"Snapshot sent to client {clientId}");
        }

        private RaceSnapshot CreateSnapshot()
        {
            RaceState raceState = raceStateMachine.GetCurrentState();
            byte[] positions = positionTracker.GetPositionsCopy();
            return networkSync.CreateSnapshot(raceState, positions);
        }

        private void HandleSnapshotReceived(RaceSnapshot snapshot)
        {
            Debug.Log("Received race snapshot from server. Applying...");

            if (raceStateMachine != null)
            {
                raceStateMachine.ApplyServerState(snapshot.RaceState);
            }

            if (physicsVisuals != null)
            {
                for (int i = 0; i < physicsVisuals.Length && i < MarbleConstants.MarbleCount; i++)
                {
                    if (physicsVisuals[i] != null)
                    {
                        physicsVisuals[i].ApplySnapshot(snapshot.MarbleStates[i]);
                    }
                }
            }

            if (positionTracker != null)
            {
                byte[] positions = new byte[MarbleConstants.MarbleCount];
                for (int i = 0; i < MarbleConstants.MarbleCount; i++)
                {
                    positions[i] = snapshot.GetPositionMarbleId(i);
                }
                positionTracker.ApplySnapshot(positions);
            }

            Debug.Log($"Snapshot applied. Race phase: {snapshot.RaceState.Phase}");
        }

        public void RequestSnapshot()
        {
            if (!networkManager.IsClient || networkManager.IsHost)
            {
                Debug.LogWarning("Can only request snapshot as a non-host client.");
                return;
            }

            Debug.Log("Snapshot request would be sent here.");
        }

        public bool IsReady => _isInitialized && networkManager != null && networkSync != null;
    }
}
