using System;
using UnityEngine;

namespace Marble
{
    public class RaceStateMachine : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float countdownDuration = MarbleConstants.CountdownDuration;

        private RacePhase _currentPhase = RacePhase.Lobby;
        private float _countdownRemaining;
        private float _raceStartTime;
        private float _raceElapsedTime;
        private int _finishedCount;
        private bool _isServer;

        public event Action<RacePhase> OnPhaseChanged;
        public event Action<int> OnCountdownTick;
        public event Action OnRaceStarted;
        public event Action OnRaceFinished;

        public RacePhase CurrentPhase => _currentPhase;
        public float CountdownRemaining => _countdownRemaining;
        public float RaceStartTime => _raceStartTime;
        public float RaceElapsedTime => _raceElapsedTime;
        public int FinishedCount => _finishedCount;
        public bool IsRacing => _currentPhase == RacePhase.Racing;
        public bool IsInLobby => _currentPhase == RacePhase.Lobby;

        public void Initialize(bool isServer)
        {
            _isServer = isServer;
            Reset();
        }

        public void SetServerStatus(bool isServer)
        {
            _isServer = isServer;
        }

        public void Reset()
        {
            _currentPhase = RacePhase.Lobby;
            _countdownRemaining = countdownDuration;
            _raceStartTime = 0f;
            _raceElapsedTime = 0f;
            _finishedCount = 0;
            _lastCountdownSecond = -1;
            _hasLoggedUpdateCheck = false;
            OnPhaseChanged?.Invoke(_currentPhase);
        }

        public void StartCountdown()
        {
            Debug.Log($"[RaceStateMachine] StartCountdown called. _isServer: {_isServer}, _currentPhase: {_currentPhase}");

            if (!_isServer)
            {
                Debug.LogWarning("[RaceStateMachine] Not server, returning.");
                return;
            }
            if (_currentPhase != RacePhase.Lobby)
            {
                Debug.LogWarning($"[RaceStateMachine] Not in Lobby phase ({_currentPhase}), returning.");
                return;
            }

            _currentPhase = RacePhase.Countdown;
            _countdownRemaining = countdownDuration;
            _lastCountdownSecond = -1;
            Debug.Log($"[RaceStateMachine] Phase changed to Countdown. Duration: {countdownDuration}, enabled: {enabled}");
            OnPhaseChanged?.Invoke(_currentPhase);
            OnCountdownTick?.Invoke(Mathf.CeilToInt(_countdownRemaining));
        }

        public void ApplyServerState(RaceState state)
        {
            RacePhase newPhase = (RacePhase)state.CurrentPhase;

            if (newPhase != _currentPhase)
            {
                _currentPhase = newPhase;
                OnPhaseChanged?.Invoke(_currentPhase);

                if (_currentPhase == RacePhase.Racing)
                {
                    OnRaceStarted?.Invoke();
                }
                else if (_currentPhase == RacePhase.Finished)
                {
                    OnRaceFinished?.Invoke();
                }
            }

            _raceStartTime = state.RaceStartTime;
            _countdownRemaining = state.CountdownRemaining;
            _finishedCount = state.FinishedCount;
        }

        public RaceState GetCurrentState()
        {
            return new RaceState
            {
                CurrentPhase = (byte)_currentPhase,
                RaceStartTime = _raceStartTime,
                CountdownRemaining = _countdownRemaining,
                TotalLaps = MarbleConstants.TotalLaps,
                FinishedCount = (byte)_finishedCount
            };
        }

        private void Update()
        {
            if (_currentPhase == RacePhase.Countdown && !_hasLoggedUpdateCheck)
            {
                _hasLoggedUpdateCheck = true;
                Debug.Log($"[RaceStateMachine] Update running - _isServer: {_isServer}, phase: {_currentPhase}");
            }

            if (!_isServer)
            {
                return;
            }

            switch (_currentPhase)
            {
                case RacePhase.Countdown:
                    UpdateCountdown();
                    break;
                case RacePhase.Racing:
                    UpdateRacing();
                    break;
            }
        }

        private int _lastCountdownSecond = -1;
        private bool _hasLoggedUpdateCheck = false;

        private void UpdateCountdown()
        {
            _countdownRemaining -= Time.deltaTime;

            int currentSecond = Mathf.CeilToInt(_countdownRemaining);
            if (currentSecond != _lastCountdownSecond && currentSecond >= 0)
            {
                _lastCountdownSecond = currentSecond;
                Debug.Log($"[RaceStateMachine] Countdown: {currentSecond}");
                OnCountdownTick?.Invoke(currentSecond);
            }

            if (_countdownRemaining <= 0f)
            {
                Debug.Log("[RaceStateMachine] Countdown finished, starting race!");
                StartRace();
            }
        }

        private void StartRace()
        {
            _currentPhase = RacePhase.Racing;
            _raceStartTime = Time.time;
            _raceElapsedTime = 0f;
            _lastCountdownSecond = -1;

            OnPhaseChanged?.Invoke(_currentPhase);
            OnRaceStarted?.Invoke();
        }

        private void UpdateRacing()
        {
            _raceElapsedTime = Time.time - _raceStartTime;

            if (_finishedCount >= MarbleConstants.MarbleCount)
            {
                FinishRace();
            }
        }

        public int RecordFinish()
        {
            if (_currentPhase != RacePhase.Racing) return -1;

            _finishedCount++;
            return _finishedCount;
        }

        private void FinishRace()
        {
            _currentPhase = RacePhase.Finished;
            OnPhaseChanged?.Invoke(_currentPhase);
            OnRaceFinished?.Invoke();
        }

        public void ForcePhase(RacePhase phase, float raceStartTime = 0f)
        {
            _currentPhase = phase;
            _raceStartTime = raceStartTime;

            if (phase == RacePhase.Racing)
            {
                _raceElapsedTime = Time.time - _raceStartTime;
            }
        }
    }
}
