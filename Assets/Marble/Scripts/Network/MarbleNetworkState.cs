using System;
using Unity.Netcode;
using UnityEngine;

namespace Marble
{
    public enum RacePhase : byte
    {
        Lobby = 0,
        Countdown = 1,
        Racing = 2,
        Finished = 3
    }

    [Serializable]
    public struct MarbleState : INetworkSerializable, IEquatable<MarbleState>
    {
        public ushort ProgressFixed;
        public ushort SpeedFixed;
        public byte LapCount;
        public byte MarbleId;
        public byte CheckpointIndex;
        public float ServerTime;
        public byte StatusFlags;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float VelocityX;
        public float VelocityY;
        public float VelocityZ;

        public float Progress => ProgressFixed * MarbleConstants.FixedToProgress;
        public float Speed => SpeedFixed * MarbleConstants.FixedToSpeed;
        public bool IsFinished => (StatusFlags & MarbleConstants.StatusFinished) != 0;
        public bool IsEliminated => (StatusFlags & MarbleConstants.StatusEliminated) != 0;
        public Vector3 Position => new Vector3(PositionX, PositionY, PositionZ);
        public Vector3 Velocity => new Vector3(VelocityX, VelocityY, VelocityZ);

        public static MarbleState Create(byte marbleId, float progress, float speed, byte lapCount, float serverTime,
            Vector3 position, Vector3 velocity, byte flags = 0, byte checkpointIndex = 0)
        {
            return new MarbleState
            {
                MarbleId = marbleId,
                ProgressFixed = (ushort)(progress * MarbleConstants.ProgressToFixed),
                SpeedFixed = (ushort)(speed * MarbleConstants.SpeedToFixed),
                LapCount = lapCount,
                CheckpointIndex = checkpointIndex,
                ServerTime = serverTime,
                StatusFlags = flags,
                PositionX = position.x,
                PositionY = position.y,
                PositionZ = position.z,
                VelocityX = velocity.x,
                VelocityY = velocity.y,
                VelocityZ = velocity.z
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ProgressFixed);
            serializer.SerializeValue(ref SpeedFixed);
            serializer.SerializeValue(ref LapCount);
            serializer.SerializeValue(ref MarbleId);
            serializer.SerializeValue(ref CheckpointIndex);
            serializer.SerializeValue(ref ServerTime);
            serializer.SerializeValue(ref StatusFlags);
            serializer.SerializeValue(ref PositionX);
            serializer.SerializeValue(ref PositionY);
            serializer.SerializeValue(ref PositionZ);
            serializer.SerializeValue(ref VelocityX);
            serializer.SerializeValue(ref VelocityY);
            serializer.SerializeValue(ref VelocityZ);
        }

        public bool Equals(MarbleState other)
        {
            return MarbleId == other.MarbleId &&
                   ProgressFixed == other.ProgressFixed &&
                   LapCount == other.LapCount;
        }

        public override bool Equals(object obj) => obj is MarbleState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(MarbleId, ProgressFixed, LapCount);
    }

    [Serializable]
    public struct MarbleStateBatch : INetworkSerializable
    {
        public MarbleState Marble0;
        public MarbleState Marble1;
        public MarbleState Marble2;
        public MarbleState Marble3;
        public MarbleState Marble4;
        public MarbleState Marble5;
        public MarbleState Marble6;
        public MarbleState Marble7;
        public MarbleState Marble8;
        public MarbleState Marble9;
        public MarbleState Marble10;
        public MarbleState Marble11;
        public MarbleState Marble12;
        public MarbleState Marble13;
        public MarbleState Marble14;
        public MarbleState Marble15;

        public ushort UpdateMask;
        public uint SequenceNumber;

        public MarbleState this[int index]
        {
            get => index switch
            {
                0 => Marble0, 1 => Marble1, 2 => Marble2, 3 => Marble3, 4 => Marble4,
                5 => Marble5, 6 => Marble6, 7 => Marble7, 8 => Marble8, 9 => Marble9,
                10 => Marble10, 11 => Marble11, 12 => Marble12, 13 => Marble13,
                14 => Marble14, 15 => Marble15,
                _ => default
            };
            set
            {
                switch (index)
                {
                    case 0: Marble0 = value; break;
                    case 1: Marble1 = value; break;
                    case 2: Marble2 = value; break;
                    case 3: Marble3 = value; break;
                    case 4: Marble4 = value; break;
                    case 5: Marble5 = value; break;
                    case 6: Marble6 = value; break;
                    case 7: Marble7 = value; break;
                    case 8: Marble8 = value; break;
                    case 9: Marble9 = value; break;
                    case 10: Marble10 = value; break;
                    case 11: Marble11 = value; break;
                    case 12: Marble12 = value; break;
                    case 13: Marble13 = value; break;
                    case 14: Marble14 = value; break;
                    case 15: Marble15 = value; break;
                }
            }
        }

        public bool HasUpdate(int marbleIndex) => (UpdateMask & (1 << marbleIndex)) != 0;

        public void SetUpdate(int marbleIndex) => UpdateMask |= (ushort)(1 << marbleIndex);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Marble0);
            serializer.SerializeValue(ref Marble1);
            serializer.SerializeValue(ref Marble2);
            serializer.SerializeValue(ref Marble3);
            serializer.SerializeValue(ref Marble4);
            serializer.SerializeValue(ref Marble5);
            serializer.SerializeValue(ref Marble6);
            serializer.SerializeValue(ref Marble7);
            serializer.SerializeValue(ref Marble8);
            serializer.SerializeValue(ref Marble9);
            serializer.SerializeValue(ref Marble10);
            serializer.SerializeValue(ref Marble11);
            serializer.SerializeValue(ref Marble12);
            serializer.SerializeValue(ref Marble13);
            serializer.SerializeValue(ref Marble14);
            serializer.SerializeValue(ref Marble15);
            serializer.SerializeValue(ref UpdateMask);
            serializer.SerializeValue(ref SequenceNumber);
        }
    }

    [Serializable]
    public struct RaceState : INetworkSerializable
    {
        public byte CurrentPhase;
        public float RaceStartTime;
        public float CountdownRemaining;
        public byte TotalLaps;
        public byte FinishedCount;

        public RacePhase Phase
        {
            get => (RacePhase)CurrentPhase;
            set => CurrentPhase = (byte)value;
        }

        public static RaceState CreateDefault()
        {
            return new RaceState
            {
                CurrentPhase = (byte)RacePhase.Lobby,
                RaceStartTime = 0f,
                CountdownRemaining = MarbleConstants.CountdownDuration,
                TotalLaps = MarbleConstants.TotalLaps,
                FinishedCount = 0
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CurrentPhase);
            serializer.SerializeValue(ref RaceStartTime);
            serializer.SerializeValue(ref CountdownRemaining);
            serializer.SerializeValue(ref TotalLaps);
            serializer.SerializeValue(ref FinishedCount);
        }
    }

    [Serializable]
    public struct RaceSnapshot : INetworkSerializable
    {
        public RaceState RaceState;
        public MarbleStateBatch MarbleStates;

        public byte Position0;
        public byte Position1;
        public byte Position2;
        public byte Position3;
        public byte Position4;
        public byte Position5;
        public byte Position6;
        public byte Position7;
        public byte Position8;
        public byte Position9;
        public byte Position10;
        public byte Position11;
        public byte Position12;
        public byte Position13;
        public byte Position14;
        public byte Position15;

        public byte GetPositionMarbleId(int position)
        {
            return position switch
            {
                0 => Position0, 1 => Position1, 2 => Position2, 3 => Position3, 4 => Position4,
                5 => Position5, 6 => Position6, 7 => Position7, 8 => Position8, 9 => Position9,
                10 => Position10, 11 => Position11, 12 => Position12, 13 => Position13,
                14 => Position14, 15 => Position15,
                _ => 0
            };
        }

        public void SetPositionMarbleId(int position, byte marbleId)
        {
            switch (position)
            {
                case 0: Position0 = marbleId; break;
                case 1: Position1 = marbleId; break;
                case 2: Position2 = marbleId; break;
                case 3: Position3 = marbleId; break;
                case 4: Position4 = marbleId; break;
                case 5: Position5 = marbleId; break;
                case 6: Position6 = marbleId; break;
                case 7: Position7 = marbleId; break;
                case 8: Position8 = marbleId; break;
                case 9: Position9 = marbleId; break;
                case 10: Position10 = marbleId; break;
                case 11: Position11 = marbleId; break;
                case 12: Position12 = marbleId; break;
                case 13: Position13 = marbleId; break;
                case 14: Position14 = marbleId; break;
                case 15: Position15 = marbleId; break;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref RaceState);
            serializer.SerializeValue(ref MarbleStates);
            serializer.SerializeValue(ref Position0);
            serializer.SerializeValue(ref Position1);
            serializer.SerializeValue(ref Position2);
            serializer.SerializeValue(ref Position3);
            serializer.SerializeValue(ref Position4);
            serializer.SerializeValue(ref Position5);
            serializer.SerializeValue(ref Position6);
            serializer.SerializeValue(ref Position7);
            serializer.SerializeValue(ref Position8);
            serializer.SerializeValue(ref Position9);
            serializer.SerializeValue(ref Position10);
            serializer.SerializeValue(ref Position11);
            serializer.SerializeValue(ref Position12);
            serializer.SerializeValue(ref Position13);
            serializer.SerializeValue(ref Position14);
            serializer.SerializeValue(ref Position15);
        }
    }

    [Serializable]
    public struct MarbleFinishResult : INetworkSerializable
    {
        public byte MarbleId;
        public byte FinishPosition;
        public float FinishTime;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MarbleId);
            serializer.SerializeValue(ref FinishPosition);
            serializer.SerializeValue(ref FinishTime);
        }
    }
}
