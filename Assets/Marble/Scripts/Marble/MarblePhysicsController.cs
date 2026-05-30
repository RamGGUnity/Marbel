using UnityEngine;

namespace Marble
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class MarblePhysicsController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private byte marbleId;

        [Header("Physics Settings")]
        [SerializeField] private float maxSpeed = 20f;

        private Rigidbody _rigidbody;

        private MarbleTrack _track;
        private int _currentCheckpoint;
        private float _currentProgress;
        private byte _currentLap;
        private int _highestCheckpointReached;

        private bool _isInitialized;
        private bool _isRacing;
        private bool _isFinished;
        private int _finishPosition = -1;
        private float _finishTime;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        public event System.Action<byte, byte> OnLapCompleted;
        public event System.Action<byte, int, float> OnFinished;

        public byte MarbleId => marbleId;
        public float Progress => _currentProgress;
        public byte LapCount => _currentLap;
        public bool IsFinished => _isFinished;
        public int FinishPosition => _finishPosition;
        public float FinishTime => _finishTime;
        public Vector3 Velocity => _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
        public Vector3 Position => transform.position;
        public int CurrentCheckpoint => _currentCheckpoint;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
        }

        public void Initialize(byte id, MarbleTrack track, Vector3 spawnPos, Quaternion spawnRot)
        {
            marbleId = id;
            _track = track;
            _spawnPosition = spawnPos;
            _spawnRotation = spawnRot;

            if (_track != null)
            {
                foreach (var checkpoint in _track.Checkpoints)
                {
                    checkpoint.OnMarbleCrossed += HandleCheckpointCrossed;
                }
            }

            Reset();
            _isInitialized = true;
        }

        private void OnDestroy()
        {
            if (_track != null)
            {
                foreach (var checkpoint in _track.Checkpoints)
                {
                    checkpoint.OnMarbleCrossed -= HandleCheckpointCrossed;
                }
            }
        }

        public void Reset()
        {
            _currentCheckpoint = 0;
            _currentProgress = 0f;
            _currentLap = 0;
            _highestCheckpointReached = 0;
            _isFinished = false;
            _isRacing = false;
            _finishPosition = -1;
            _finishTime = 0f;

            if (_rigidbody != null)
            {
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                _rigidbody.isKinematic = true;
            }

            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;
        }

        private void HandleCheckpointCrossed(MarbleCheckpoint checkpoint, MarblePhysicsController marble)
        {
            if (marble != this) return;
            if (!_isRacing || _isFinished) return;

            int crossedIndex = checkpoint.Index;

            _currentCheckpoint = crossedIndex;

            if (crossedIndex > _highestCheckpointReached)
            {
                _highestCheckpointReached = crossedIndex;
            }

            if (checkpoint.IsFinishLine && _highestCheckpointReached >= _track.CheckpointCount / 2)
            {
                _currentLap++;
                _highestCheckpointReached = 0;

                Debug.Log($"[LAP] Marble {marbleId}: LAP COMPLETED! Now on lap {_currentLap}/{MarbleConstants.TotalLaps}");
                OnLapCompleted?.Invoke(marbleId, _currentLap);

                if (_currentLap >= MarbleConstants.TotalLaps)
                {
                    Debug.Log($"[FINISH] Marble {marbleId}: RACE FINISHED!");
                    CompleteRace(Time.time);
                }
            }
        }

        public void StartRacing()
        {
            _isRacing = true;

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }
        }

        public void StopRacing()
        {
            _isRacing = false;
        }

        private void FixedUpdate()
        {
            if (!_isInitialized || !_isRacing || _isFinished) return;

            UpdateCheckpointProgress();
            ClampSpeed();
        }

        private void UpdateCheckpointProgress()
        {
            if (_track == null) return;

            _currentProgress = _track.CalculateProgress(transform.position, _currentCheckpoint, _currentLap);
        }

        private void ClampSpeed()
        {
            if (_rigidbody.linearVelocity.magnitude > maxSpeed)
            {
                _rigidbody.linearVelocity = _rigidbody.linearVelocity.normalized * maxSpeed;
            }
        }

        private void CompleteRace(float raceTime)
        {
            _isFinished = true;
            _isRacing = false;
            _finishTime = raceTime;
        }

        public void SetFinishPosition(int position)
        {
            _finishPosition = position;
            OnFinished?.Invoke(marbleId, position, _finishTime);
        }

        public float GetRaceScore()
        {
            if (_isFinished)
            {
                return (MarbleConstants.TotalLaps + 1) * 1000f - _finishTime;
            }
            return (_currentLap * 1000f) + (_currentProgress * 1000f);
        }

        private void OnDrawGizmosSelected()
        {
            if (_track == null) return;

            var nextCheckpoint = _track.GetCheckpoint(_track.GetNextCheckpointIndex(_currentCheckpoint));
            if (nextCheckpoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(nextCheckpoint.Position, 1f);
                Gizmos.DrawLine(transform.position, nextCheckpoint.Position);
            }
        }
    }
}
