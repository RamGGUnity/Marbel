using UnityEngine;
using System.Collections.Generic;

namespace Marble
{
    public class MarbleTrack : MonoBehaviour
    {
        [Header("Checkpoints")]
        [SerializeField] private List<MarbleCheckpoint> checkpoints = new List<MarbleCheckpoint>();

        [Header("Track Settings")]
        [SerializeField] private bool isLooping = true;

        private float[] _checkpointDistances;
        private float _totalLength;
        private bool _isInitialized;

        public int CheckpointCount => checkpoints.Count;
        public float TotalLength => _totalLength;
        public bool IsLooping => isLooping;
        public List<MarbleCheckpoint> Checkpoints => checkpoints;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            AssignCheckpointIndices();
            CalculateDistances();

            _isInitialized = true;
        }

        private void AssignCheckpointIndices()
        {
            for (int i = 0; i < checkpoints.Count; i++)
            {
                checkpoints[i].SetIndex(i);
                checkpoints[i].SetupTrigger();
            }

            if (checkpoints.Count > 0)
            {
                checkpoints[0].SetAsFinishLine(true);
            }
        }

        private void CalculateDistances()
        {
            if (checkpoints.Count < 2)
            {
                _checkpointDistances = new float[0];
                _totalLength = 0;
                return;
            }

            _checkpointDistances = new float[checkpoints.Count];
            _totalLength = 0;

            for (int i = 0; i < checkpoints.Count; i++)
            {
                int next = (i + 1) % checkpoints.Count;
                float dist = Vector3.Distance(checkpoints[i].Position, checkpoints[next].Position);
                _checkpointDistances[i] = dist;
                _totalLength += dist;
            }
        }

        public MarbleCheckpoint GetCheckpoint(int index)
        {
            if (index < 0 || index >= checkpoints.Count) return null;
            return checkpoints[index];
        }

        public int GetNextCheckpointIndex(int currentIndex)
        {
            int next = currentIndex + 1;
            if (isLooping)
            {
                return next % checkpoints.Count;
            }
            return Mathf.Min(next, checkpoints.Count - 1);
        }

        public Vector3 GetDirectionToNextCheckpoint(Vector3 position, int currentCheckpointIndex)
        {
            int nextIndex = GetNextCheckpointIndex(currentCheckpointIndex);
            var nextCheckpoint = GetCheckpoint(nextIndex);

            if (nextCheckpoint == null) return Vector3.forward;

            return (nextCheckpoint.Position - position).normalized;
        }

        public Vector3 GetTrackDirection(int checkpointIndex)
        {
            if (checkpoints.Count < 2) return Vector3.forward;

            var current = GetCheckpoint(checkpointIndex);
            if (current == null) return Vector3.forward;

            return current.Forward;
        }

        public Vector3 GetTrackCenter(int checkpointIndex, float t)
        {
            var current = GetCheckpoint(checkpointIndex);
            int nextIndex = GetNextCheckpointIndex(checkpointIndex);
            var next = GetCheckpoint(nextIndex);

            if (current == null || next == null) return Vector3.zero;

            return Vector3.Lerp(current.Position, next.Position, t);
        }

        public float CalculateProgress(Vector3 position, int currentCheckpointIndex, int lapCount)
        {
            if (checkpoints.Count < 2) return 0f;

            float distanceCovered = 0f;
            for (int i = 0; i < currentCheckpointIndex; i++)
            {
                distanceCovered += _checkpointDistances[i];
            }

            var current = GetCheckpoint(currentCheckpointIndex);
            int nextIndex = GetNextCheckpointIndex(currentCheckpointIndex);
            var next = GetCheckpoint(nextIndex);

            if (current != null && next != null)
            {
                float segmentLength = _checkpointDistances[currentCheckpointIndex];
                float distToNext = Vector3.Distance(position, next.Position);
                float segmentProgress = 1f - Mathf.Clamp01(distToNext / segmentLength);
                distanceCovered += segmentProgress * segmentLength;
            }

            float progress = _totalLength > 0 ? distanceCovered / _totalLength : 0f;
            return Mathf.Clamp01(progress);
        }

        public int FindCurrentCheckpoint(Vector3 position, int lastKnownCheckpoint)
        {
            if (checkpoints.Count == 0) return 0;

            int nextIndex = GetNextCheckpointIndex(lastKnownCheckpoint);
            var nextCheckpoint = GetCheckpoint(nextIndex);

            if (nextCheckpoint != null && nextCheckpoint.HasPassed(position))
            {
                float distanceToNext = Vector3.Distance(position, nextCheckpoint.Position);
                float segmentLength = _checkpointDistances[lastKnownCheckpoint];

                if (distanceToNext < segmentLength * 1.5f)
                {
                    return nextIndex;
                }
            }

            return lastKnownCheckpoint;
        }

        public Vector3 GetTrackCorrectionVector(Vector3 position, int checkpointIndex)
        {
            var checkpoint = GetCheckpoint(checkpointIndex);
            int nextIndex = GetNextCheckpointIndex(checkpointIndex);
            var nextCheckpoint = GetCheckpoint(nextIndex);

            if (checkpoint == null || nextCheckpoint == null) return Vector3.zero;

            Vector3 lineDir = (nextCheckpoint.Position - checkpoint.Position).normalized;
            Vector3 toPosition = position - checkpoint.Position;
            float projLength = Vector3.Dot(toPosition, lineDir);
            Vector3 projectedPoint = checkpoint.Position + lineDir * projLength;

            Vector3 correction = projectedPoint - position;
            correction.y = 0;

            return correction;
        }

        public bool IsOnTrack(Vector3 position, int checkpointIndex, float tolerance = 5f)
        {
            var checkpoint = GetCheckpoint(checkpointIndex);
            if (checkpoint == null) return true;

            float lateralOffset = Mathf.Abs(checkpoint.GetLateralOffset(position));
            return lateralOffset <= checkpoint.TrackWidth * 0.5f + tolerance;
        }

        private void OnDrawGizmos()
        {
            if (checkpoints == null || checkpoints.Count < 2) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < checkpoints.Count; i++)
            {
                if (checkpoints[i] == null) continue;

                int next = (i + 1) % checkpoints.Count;
                if (!isLooping && next == 0) continue;
                if (checkpoints[next] == null) continue;

                Gizmos.DrawLine(checkpoints[i].Position, checkpoints[next].Position);
            }
        }
    }
}
