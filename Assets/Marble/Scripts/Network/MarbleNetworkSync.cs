using System;
using UnityEngine;
using Unity.Netcode;

namespace Marble
{
    public class MarbleNetworkSync : NetworkBehaviour
    {
        [Header("Sync Configuration")]
        [SerializeField] private float syncInterval = MarbleConstants.SyncInterval;

        private float _nextSyncTime;
        private uint _sequenceNumber;
        private MarblePhysicsController[] _physicsControllers;

        private MarblePhysicsVisual[] _physicsVisuals;

        public event Action<MarbleStateBatch> OnMarbleStatesReceived;
        public event Action<RacePhase, float> OnRacePhaseChanged;
        public event Action<int> OnCountdownTick;
        public event Action<byte, byte> OnLapCompleted;
        public event Action<byte, byte, float> OnMarbleFinished;
        public event Action<RaceSnapshot> OnSnapshotReceived;
        public event Action<byte[]> OnSelectedMarblesReceived;
        public event Action<ClientInitData> OnClientInitDataReceived;

        public void InitializeServer(MarblePhysicsController[] controllers)
        {
            _physicsControllers = controllers;
            _sequenceNumber = 0;
            _nextSyncTime = 0f;
        }

        public void InitializeClient(MarblePhysicsVisual[] visuals)
        {
            _physicsVisuals = visuals;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer && _physicsControllers != null)
            {
                if (Time.time >= _nextSyncTime)
                {
                    BroadcastMarbleStates();
                    _nextSyncTime = Time.time + syncInterval;
                }
            }
        }

        private void BroadcastMarbleStates()
        {
            float serverTime = NetworkManager.ServerTime.TimeAsFloat;

            ushort activeMask = (ushort)((1 << MarbleConstants.MarbleCount) - 1);
            var batch = new MarbleStateBatch
            {
                SequenceNumber = _sequenceNumber++,
                UpdateMask = activeMask
            };

            for (int i = 0; i < _physicsControllers.Length && i < MarbleConstants.MarbleCount; i++)
            {
                var controller = _physicsControllers[i];
                if (controller == null) continue;

                byte flags = 0;
                if (controller.IsFinished) flags |= MarbleConstants.StatusFinished;

                batch[i] = MarbleState.Create(
                    controller.MarbleId,
                    controller.Progress,
                    controller.Velocity.magnitude / 20f,
                    controller.LapCount,
                    serverTime,
                    controller.Position,
                    controller.Velocity,
                    flags,
                    (byte)controller.CurrentCheckpoint
                );
            }

            BroadcastMarbleStatesClientRpc(batch);
        }

        #region ClientRPCs

        [ClientRpc]
        public void BroadcastMarbleStatesClientRpc(MarbleStateBatch batch)
        {
            if (IsHost) return;

            if (_physicsVisuals != null)
            {
                for (int i = 0; i < _physicsVisuals.Length && i < MarbleConstants.MarbleCount; i++)
                {
                    if (batch.HasUpdate(i))
                    {
                        _physicsVisuals[i]?.ReceiveState(batch[i]);
                    }
                }
            }

            OnMarbleStatesReceived?.Invoke(batch);
        }

        [ClientRpc]
        public void OnRacePhaseChangedClientRpc(byte newPhase, float serverTime)
        {
            OnRacePhaseChanged?.Invoke((RacePhase)newPhase, serverTime);
        }

        [ClientRpc]
        public void OnCountdownTickClientRpc(int secondsRemaining)
        {
            OnCountdownTick?.Invoke(secondsRemaining);
        }

        [ClientRpc]
        public void OnLapCompletedClientRpc(byte marbleId, byte newLapCount)
        {
            OnLapCompleted?.Invoke(marbleId, newLapCount);
        }

        [ClientRpc]
        public void OnMarbleFinishedClientRpc(byte marbleId, byte position, float finishTime)
        {
            OnMarbleFinished?.Invoke(marbleId, position, finishTime);
        }

        [ClientRpc]
        public void SyncSelectedMarblesClientRpc(byte[] selectedIds)
        {
            if (IsHost) return;
            OnSelectedMarblesReceived?.Invoke(selectedIds);
        }

        [ClientRpc]
        private void SyncClientInitDataClientRpc(ClientInitData initData, ClientRpcParams rpcParams = default)
        {
            Debug.Log($"[NetworkSync] SyncClientInitDataClientRpc received! IsHost: {IsHost}, MarbleCount: {initData.MarbleCount}");
            if (IsHost) return;
            Debug.Log($"[NetworkSync] Invoking OnClientInitDataReceived. HasSubscribers: {OnClientInitDataReceived != null}");
            OnClientInitDataReceived?.Invoke(initData);
        }

        #endregion

        #region Late Joiner Sync

        public void SendSnapshotToClient(ulong clientId, RaceSnapshot snapshot)
        {
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };

            SyncFullSnapshotClientRpc(snapshot, rpcParams);
        }

        [ClientRpc]
        private void SyncFullSnapshotClientRpc(RaceSnapshot snapshot, ClientRpcParams rpcParams = default)
        {
            if (_physicsVisuals != null)
            {
                for (int i = 0; i < _physicsVisuals.Length && i < MarbleConstants.MarbleCount; i++)
                {
                    _physicsVisuals[i]?.ApplySnapshot(snapshot.MarbleStates[i]);
                }
            }

            OnSnapshotReceived?.Invoke(snapshot);
        }

        #endregion

        #region Server Methods

        public void ServerNotifyPhaseChange(RacePhase phase)
        {
            if (!IsServer) return;
            float serverTime = NetworkManager.ServerTime.TimeAsFloat;
            OnRacePhaseChangedClientRpc((byte)phase, serverTime);
        }

        public void ServerNotifyCountdown(int seconds)
        {
            if (!IsServer) return;
            OnCountdownTickClientRpc(seconds);
        }

        public void ServerNotifyLapComplete(byte marbleId, byte newLapCount)
        {
            if (!IsServer) return;
            OnLapCompletedClientRpc(marbleId, newLapCount);
        }

        public void ServerNotifyMarbleFinished(byte marbleId, byte position, float finishTime)
        {
            if (!IsServer) return;
            OnMarbleFinishedClientRpc(marbleId, position, finishTime);
        }

        public void ServerBroadcastSelectedMarbles(byte[] selectedIds)
        {
            if (!IsServer) return;
            SyncSelectedMarblesClientRpc(selectedIds);
        }

        public void ServerSendInitDataToClient(ulong clientId, ClientInitData initData)
        {
            if (!IsServer) return;

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };

            SyncClientInitDataClientRpc(initData, rpcParams);
        }

        public RaceSnapshot CreateSnapshot(RaceState raceState, byte[] positionRanking)
        {
            if (!IsServer || _physicsControllers == null)
            {
                return new RaceSnapshot();
            }

            float serverTime = NetworkManager.ServerTime.TimeAsFloat;

            var snapshot = new RaceSnapshot
            {
                RaceState = raceState
            };

            for (int i = 0; i < _physicsControllers.Length && i < MarbleConstants.MarbleCount; i++)
            {
                var controller = _physicsControllers[i];
                if (controller == null) continue;

                byte flags = 0;
                if (controller.IsFinished) flags |= MarbleConstants.StatusFinished;

                snapshot.MarbleStates[i] = MarbleState.Create(
                    controller.MarbleId,
                    controller.Progress,
                    controller.Velocity.magnitude / 20f,
                    controller.LapCount,
                    serverTime,
                    controller.Position,
                    controller.Velocity,
                    flags,
                    (byte)controller.CurrentCheckpoint
                );
                snapshot.MarbleStates.SetUpdate(i);
            }

            if (positionRanking != null)
            {
                for (int i = 0; i < positionRanking.Length && i < MarbleConstants.MarbleCount; i++)
                {
                    snapshot.SetPositionMarbleId(i, positionRanking[i]);
                }
            }

            snapshot.MarbleStates.SequenceNumber = _sequenceNumber;

            return snapshot;
        }

        #endregion
    }

    public struct ClientInitData : INetworkSerializable
    {
        public byte MarbleCount;
        public byte CurrentPhase;
        public float RaceTime;
        public float ServerTime;

        private byte _id0, _id1, _id2, _id3, _id4, _id5, _id6, _id7;
        private byte _id8, _id9, _id10, _id11, _id12, _id13, _id14, _id15;

        public void SetSelectedIds(byte[] ids)
        {
            if (ids == null) return;
            if (ids.Length > 0) _id0 = ids[0];
            if (ids.Length > 1) _id1 = ids[1];
            if (ids.Length > 2) _id2 = ids[2];
            if (ids.Length > 3) _id3 = ids[3];
            if (ids.Length > 4) _id4 = ids[4];
            if (ids.Length > 5) _id5 = ids[5];
            if (ids.Length > 6) _id6 = ids[6];
            if (ids.Length > 7) _id7 = ids[7];
            if (ids.Length > 8) _id8 = ids[8];
            if (ids.Length > 9) _id9 = ids[9];
            if (ids.Length > 10) _id10 = ids[10];
            if (ids.Length > 11) _id11 = ids[11];
            if (ids.Length > 12) _id12 = ids[12];
            if (ids.Length > 13) _id13 = ids[13];
            if (ids.Length > 14) _id14 = ids[14];
            if (ids.Length > 15) _id15 = ids[15];
        }

        public byte[] GetSelectedIds()
        {
            var ids = new byte[MarbleCount];
            if (MarbleCount > 0) ids[0] = _id0;
            if (MarbleCount > 1) ids[1] = _id1;
            if (MarbleCount > 2) ids[2] = _id2;
            if (MarbleCount > 3) ids[3] = _id3;
            if (MarbleCount > 4) ids[4] = _id4;
            if (MarbleCount > 5) ids[5] = _id5;
            if (MarbleCount > 6) ids[6] = _id6;
            if (MarbleCount > 7) ids[7] = _id7;
            if (MarbleCount > 8) ids[8] = _id8;
            if (MarbleCount > 9) ids[9] = _id9;
            if (MarbleCount > 10) ids[10] = _id10;
            if (MarbleCount > 11) ids[11] = _id11;
            if (MarbleCount > 12) ids[12] = _id12;
            if (MarbleCount > 13) ids[13] = _id13;
            if (MarbleCount > 14) ids[14] = _id14;
            if (MarbleCount > 15) ids[15] = _id15;
            return ids;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MarbleCount);
            serializer.SerializeValue(ref CurrentPhase);
            serializer.SerializeValue(ref RaceTime);
            serializer.SerializeValue(ref ServerTime);
            serializer.SerializeValue(ref _id0);
            serializer.SerializeValue(ref _id1);
            serializer.SerializeValue(ref _id2);
            serializer.SerializeValue(ref _id3);
            serializer.SerializeValue(ref _id4);
            serializer.SerializeValue(ref _id5);
            serializer.SerializeValue(ref _id6);
            serializer.SerializeValue(ref _id7);
            serializer.SerializeValue(ref _id8);
            serializer.SerializeValue(ref _id9);
            serializer.SerializeValue(ref _id10);
            serializer.SerializeValue(ref _id11);
            serializer.SerializeValue(ref _id12);
            serializer.SerializeValue(ref _id13);
            serializer.SerializeValue(ref _id14);
            serializer.SerializeValue(ref _id15);
        }
    }
}
