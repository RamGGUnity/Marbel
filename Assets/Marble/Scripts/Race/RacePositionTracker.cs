using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marble
{
    public class RacePositionTracker : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float updateInterval = 0.1f;

        private MarblePhysicsController[] _physicsControllers;
        private MarblePhysicsVisual[] _physicsVisuals;
        private bool _isInitialized;
        private bool _isClient;

        private byte[] _positionToMarbleId;
        private byte[] _marbleIdToPosition;
        private float[] _marbleScores;

        private List<(byte id, float score)> _sortingList;

        private float _nextUpdateTime;

        public event Action<byte[]> OnPositionsChanged;

        public byte[] PositionRanking => _positionToMarbleId;

        private void Awake()
        {
            _positionToMarbleId = new byte[MarbleConstants.MarbleCount];
            _marbleIdToPosition = new byte[MarbleConstants.MarbleCount];
            _marbleScores = new float[MarbleConstants.MarbleCount];
            _sortingList = new List<(byte, float)>(MarbleConstants.MarbleCount);

            for (int i = 0; i < MarbleConstants.MarbleCount; i++)
            {
                _positionToMarbleId[i] = (byte)i;
                _marbleIdToPosition[i] = (byte)i;
            }
        }

        public void Initialize(MarblePhysicsController[] controllers)
        {
            _physicsControllers = controllers;
            _isInitialized = controllers != null && controllers.Length > 0;
            _isClient = false;
            _nextUpdateTime = 0f;

            for (int i = 0; i < MarbleConstants.MarbleCount; i++)
            {
                _positionToMarbleId[i] = (byte)i;
                _marbleIdToPosition[i] = (byte)i;
            }
        }

        public void InitializeClient(MarblePhysicsVisual[] visuals)
        {
            _physicsVisuals = visuals;
            _isInitialized = visuals != null && visuals.Length > 0;
            _isClient = true;
            _nextUpdateTime = 0f;

            for (int i = 0; i < MarbleConstants.MarbleCount; i++)
            {
                _positionToMarbleId[i] = (byte)i;
                _marbleIdToPosition[i] = (byte)i;
            }
        }

        public void ForceUpdate()
        {
            if (!_isInitialized) return;
            UpdatePositions();
        }

        private void Update()
        {
            if (!_isInitialized) return;

            if (Time.time >= _nextUpdateTime)
            {
                UpdatePositions();
                _nextUpdateTime = Time.time + updateInterval;
            }
        }

        private void UpdatePositions()
        {
            _sortingList.Clear();

            if (_isClient)
            {
                if (_physicsVisuals == null) return;

                for (int i = 0; i < _physicsVisuals.Length && i < MarbleConstants.MarbleCount; i++)
                {
                    var visual = _physicsVisuals[i];
                    if (visual == null) continue;

                    float score = visual.GetRaceScore();
                    _marbleScores[i] = score;
                    _sortingList.Add((visual.MarbleId, score));
                }
            }
            else
            {
                if (_physicsControllers == null) return;

                for (int i = 0; i < _physicsControllers.Length && i < MarbleConstants.MarbleCount; i++)
                {
                    var controller = _physicsControllers[i];
                    if (controller == null) continue;

                    float score = controller.GetRaceScore();
                    _marbleScores[i] = score;
                    _sortingList.Add((controller.MarbleId, score));
                }
            }

            _sortingList.Sort((a, b) => b.score.CompareTo(a.score));

            bool changed = false;
            for (int position = 0; position < _sortingList.Count; position++)
            {
                byte marbleId = _sortingList[position].id;

                if (_positionToMarbleId[position] != marbleId)
                {
                    changed = true;
                }

                _positionToMarbleId[position] = marbleId;
                _marbleIdToPosition[marbleId] = (byte)position;
            }

            if (changed)
            {
                OnPositionsChanged?.Invoke(_positionToMarbleId);
            }
        }

        public int GetPosition(byte marbleId)
        {
            if (marbleId >= MarbleConstants.MarbleCount) return -1;
            return _marbleIdToPosition[marbleId];
        }

        public byte GetMarbleAtPosition(int position)
        {
            if (position < 0 || position >= MarbleConstants.MarbleCount) return 0;
            return _positionToMarbleId[position];
        }

        public static string GetPositionString(int position)
        {
            int displayPos = position + 1;
            return displayPos switch
            {
                1 => "1st",
                2 => "2nd",
                3 => "3rd",
                _ => $"{displayPos}th"
            };
        }

        public byte[] GetPositionsCopy()
        {
            var copy = new byte[MarbleConstants.MarbleCount];
            Array.Copy(_positionToMarbleId, copy, MarbleConstants.MarbleCount);
            return copy;
        }

        public void ApplySnapshot(byte[] positions)
        {
            if (positions == null || positions.Length != MarbleConstants.MarbleCount) return;

            Array.Copy(positions, _positionToMarbleId, MarbleConstants.MarbleCount);

            for (int position = 0; position < MarbleConstants.MarbleCount; position++)
            {
                byte marbleId = _positionToMarbleId[position];
                if (marbleId < MarbleConstants.MarbleCount)
                {
                    _marbleIdToPosition[marbleId] = (byte)position;
                }
            }

            OnPositionsChanged?.Invoke(_positionToMarbleId);
        }

        public RankingData[] GetRankingData()
        {
            if (!_isInitialized || _physicsControllers == null) return Array.Empty<RankingData>();

            var data = new RankingData[_physicsControllers.Length];

            for (int position = 0; position < _physicsControllers.Length; position++)
            {
                byte marbleId = _positionToMarbleId[position];
                var controller = marbleId < _physicsControllers.Length ? _physicsControllers[marbleId] : null;

                if (controller != null)
                {
                    data[position] = new RankingData
                    {
                        Position = position,
                        MarbleId = marbleId,
                        LapCount = controller.LapCount,
                        Progress = controller.Progress,
                        IsFinished = controller.IsFinished,
                        FinishTime = controller.FinishTime
                    };
                }
                else
                {
                    data[position] = new RankingData
                    {
                        Position = position,
                        MarbleId = marbleId,
                        LapCount = 0,
                        Progress = 0f,
                        IsFinished = false,
                        FinishTime = 0f
                    };
                }
            }

            return data;
        }
    }

    public struct RankingData
    {
        public int Position;
        public byte MarbleId;
        public byte LapCount;
        public float Progress;
        public bool IsFinished;
        public float FinishTime;
    }
}
