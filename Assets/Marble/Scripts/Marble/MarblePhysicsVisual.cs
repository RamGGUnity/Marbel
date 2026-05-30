using UnityEngine;

namespace Marble
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class MarblePhysicsVisual : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private byte marbleId;

        [Header("Interpolation Settings")]
        [SerializeField] private float interpolationDelay = MarbleConstants.InterpolationDelay;
        [SerializeField] private float maxExtrapolationTime = MarbleConstants.MaxExtrapolationTime;
        [SerializeField] private float baseSmoothing = 15f;

        [Header("Visual")]
        [SerializeField] private MeshRenderer meshRenderer;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        private Rigidbody _rigidbody;

        private const int BufferSize = 4;
        private MarbleState[] _stateBuffer = new MarbleState[BufferSize];
        private int _bufferHead = 0;
        private int _stateCount = 0;

        private float _serverTimeOffset = 0f;
        private float _lastServerTime = 0f;
        private bool _timeInitialized = false;

        private float _averageJitter = 0f;
        private float _lastReceiveTime = 0f;
        private float _expectedInterval = MarbleConstants.SyncInterval;

        private Vector3 _lastPosition;
        private Vector3 _currentVelocity;

        private byte _displayLap;
        private float _displayProgress;
        private bool _isFinished;

        private bool _isInitialized;
        private bool _hasReceivedFirstState;
        private bool _isRacing;

        public byte MarbleId => marbleId;
        public bool IsFinished => _isFinished;
        public float DisplayProgress => _displayProgress;
        public byte DisplayLap => _displayLap;

        public float GetRaceScore()
        {
            return _displayLap * 1000f + _displayProgress;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
        }

        public void Initialize(byte id, MarbleTrack track, Material material = null)
        {
            marbleId = id;

            if (material != null && meshRenderer != null)
            {
                meshRenderer.material = material;
            }

            Reset();
            _isInitialized = true;
        }

        public void Reset()
        {
            _displayLap = 0;
            _displayProgress = 0f;
            _isFinished = false;
            _hasReceivedFirstState = false;
            _isRacing = false;

            _bufferHead = 0;
            _stateCount = 0;
            _timeInitialized = false;
            _averageJitter = 0f;
            _lastReceiveTime = 0f;

            _lastPosition = transform.position;
            _currentVelocity = Vector3.zero;

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
            }
        }

        public void StartRacing()
        {
            _isRacing = true;
            if (_rigidbody != null && _hasReceivedFirstState)
            {
                _rigidbody.isKinematic = false;
            }
        }

        public void StopRacing()
        {
            _isRacing = false;
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        public void ReceiveState(MarbleState state)
        {
            if (state.MarbleId != marbleId) return;

            float receiveTime = Time.unscaledTime;

            if (!_timeInitialized)
            {
                _serverTimeOffset = receiveTime - state.ServerTime;
                _timeInitialized = true;
                _lastReceiveTime = receiveTime;
            }
            else
            {
                float timeSinceLastPacket = receiveTime - _lastReceiveTime;
                float jitter = Mathf.Abs(timeSinceLastPacket - _expectedInterval);
                _averageJitter = Mathf.Lerp(_averageJitter, jitter, 0.1f);
                _lastReceiveTime = receiveTime;

                float newOffset = receiveTime - state.ServerTime;
                _serverTimeOffset = Mathf.Lerp(_serverTimeOffset, newOffset, 0.01f);
            }

            _lastServerTime = state.ServerTime;

            AddStateToBuffer(state);

            if (!_hasReceivedFirstState)
            {
                transform.position = state.Position;
                _lastPosition = state.Position;
                _currentVelocity = state.Velocity;
                _hasReceivedFirstState = true;

                if (_isRacing && _rigidbody != null)
                {
                    _rigidbody.isKinematic = false;
                }
            }

            _displayLap = state.LapCount;
            _displayProgress = state.Progress;
            if (state.IsFinished)
            {
                _isFinished = true;
            }
        }

        private void AddStateToBuffer(MarbleState state)
        {
            _stateBuffer[_bufferHead] = state;
            _bufferHead = (_bufferHead + 1) % BufferSize;
            _stateCount = Mathf.Min(_stateCount + 1, BufferSize);
        }

        public void ApplySnapshot(MarbleState state)
        {
            _bufferHead = 0;
            _stateCount = 0;
            AddStateToBuffer(state);

            transform.position = state.Position;
            _lastPosition = state.Position;
            _currentVelocity = state.Velocity;
            _displayLap = state.LapCount;
            _displayProgress = state.Progress;
            _isFinished = state.IsFinished;
            _hasReceivedFirstState = true;

            _serverTimeOffset = Time.unscaledTime - state.ServerTime;
            _timeInitialized = true;

            if (_rigidbody != null && _isRacing)
            {
                _rigidbody.isKinematic = false;
            }
        }

        private void FixedUpdate()
        {
            if (!_isInitialized || !_hasReceivedFirstState || !_isRacing) return;

            float currentLocalTime = Time.unscaledTime;
            float renderServerTime = (currentLocalTime - _serverTimeOffset) - interpolationDelay;

            Vector3 targetPosition = CalculateTargetPosition(renderServerTime, out Vector3 targetVelocity);

            float adaptiveSmoothing = CalculateAdaptiveSmoothing();

            Vector3 newPosition = Vector3.Lerp(transform.position, targetPosition, adaptiveSmoothing * Time.fixedDeltaTime);

            Vector3 moveDelta = newPosition - _lastPosition;
            float moveDistance = moveDelta.magnitude;

            _rigidbody.MovePosition(newPosition);

            if (moveDistance > 0.001f)
            {
                Vector3 moveDir = moveDelta / moveDistance;

                Vector3 rotationAxis = Vector3.Cross(moveDir, Vector3.up);
                if (rotationAxis.sqrMagnitude < 0.001f)
                {
                    rotationAxis = Vector3.Cross(moveDir, Vector3.forward);
                }
                rotationAxis.Normalize();

                float rotationAngle = moveDistance / MarbleConstants.MarbleRadius * Mathf.Rad2Deg;
                _rigidbody.MoveRotation(_rigidbody.rotation * Quaternion.AngleAxis(rotationAngle, rotationAxis));
            }

            _lastPosition = newPosition;
            _currentVelocity = targetVelocity;
        }

        private Vector3 CalculateTargetPosition(float renderTime, out Vector3 velocity)
        {
            if (_stateCount == 0)
            {
                velocity = Vector3.zero;
                return transform.position;
            }

            MarbleState? beforeState = null;
            MarbleState? afterState = null;

            for (int i = 0; i < _stateCount; i++)
            {
                int idx = (_bufferHead - 1 - i + BufferSize) % BufferSize;
                MarbleState state = _stateBuffer[idx];

                if (state.ServerTime <= renderTime)
                {
                    if (!beforeState.HasValue || state.ServerTime > beforeState.Value.ServerTime)
                    {
                        beforeState = state;
                    }
                }
                else
                {
                    if (!afterState.HasValue || state.ServerTime < afterState.Value.ServerTime)
                    {
                        afterState = state;
                    }
                }
            }

            if (beforeState.HasValue && afterState.HasValue)
            {
                float t = Mathf.InverseLerp(beforeState.Value.ServerTime, afterState.Value.ServerTime, renderTime);
                velocity = Vector3.Lerp(beforeState.Value.Velocity, afterState.Value.Velocity, t);
                return Vector3.Lerp(beforeState.Value.Position, afterState.Value.Position, t);
            }

            if (beforeState.HasValue && !afterState.HasValue)
            {
                float extrapolateTime = renderTime - beforeState.Value.ServerTime;

                extrapolateTime = Mathf.Min(extrapolateTime, maxExtrapolationTime);

                velocity = beforeState.Value.Velocity;

                Vector3 extrapolatedPos = beforeState.Value.Position +
                                          beforeState.Value.Velocity * extrapolateTime +
                                          Physics.gravity * (0.5f * extrapolateTime * extrapolateTime);

                return extrapolatedPos;
            }

            if (!beforeState.HasValue && afterState.HasValue)
            {
                velocity = afterState.Value.Velocity;
                return afterState.Value.Position;
            }

            velocity = _currentVelocity;
            return transform.position;
        }

        private float CalculateAdaptiveSmoothing()
        {
            float jitterFactor = 1f + (_averageJitter / _expectedInterval);
            jitterFactor = Mathf.Clamp(jitterFactor, 0.5f, 2f);

            return baseSmoothing / jitterFactor;
        }

        public void SetMaterial(Material material)
        {
            if (meshRenderer != null && material != null)
            {
                meshRenderer.material = material;
            }
        }

        public void SetColor(Color color)
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.color = color;
            }
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !_isInitialized) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f);
            if (screenPos.z > 0)
            {
                float y = Screen.height - screenPos.y;
                GUI.Label(new Rect(screenPos.x - 50, y, 200, 100),
                    $"M{marbleId}\n" +
                    $"Jitter: {_averageJitter * 1000f:F1}ms\n" +
                    $"Buffer: {_stateCount}/{BufferSize}\n" +
                    $"InterpDelay: {interpolationDelay * 1000f:F0}ms");
            }
        }
#endif
    }
}
