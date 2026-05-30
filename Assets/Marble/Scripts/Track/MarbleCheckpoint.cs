using UnityEngine;

namespace Marble
{
    public class MarbleCheckpoint : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private int checkpointIndex = -1;
        [SerializeField] private bool isFinishLine;
        [SerializeField] private float trackWidth = 3f;
        [SerializeField] private float triggerDepth = 1f;

        public event System.Action<MarbleCheckpoint, MarblePhysicsController> OnMarbleCrossed;

        public int Index => checkpointIndex;
        public bool IsFinishLine => isFinishLine;
        public float TrackWidth => trackWidth;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public Vector3 Right => transform.right;

        public void SetIndex(int index)
        {
            checkpointIndex = index;
        }

        public void SetAsFinishLine(bool value)
        {
            isFinishLine = value;
        }

        public float GetSignedDistance(Vector3 point)
        {
            Vector3 toPoint = point - Position;
            return Vector3.Dot(toPoint, Forward);
        }

        public float GetLateralOffset(Vector3 point)
        {
            Vector3 toPoint = point - Position;
            return Vector3.Dot(toPoint, Right);
        }

        public bool HasPassed(Vector3 point)
        {
            return GetSignedDistance(point) > 0;
        }

        public void SetupTrigger()
        {
            var existingCollider = GetComponent<BoxCollider>();
            if (existingCollider != null && existingCollider.isTrigger)
            {
                DestroyImmediate(existingCollider);
            }

            var trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(trackWidth, 3f, triggerDepth);
            trigger.center = new Vector3(0, 1.5f, 0);
        }

        private void OnTriggerEnter(Collider other)
        {
            var marble = other.GetComponent<MarblePhysicsController>();
            if (marble == null)
            {
                marble = other.GetComponentInParent<MarblePhysicsController>();
            }

            if (marble != null)
            {
                OnMarbleCrossed?.Invoke(this, marble);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isFinishLine ? Color.green : Color.yellow;

            Vector3 left = Position - Right * trackWidth * 0.5f;
            Vector3 right = Position + Right * trackWidth * 0.5f;

            Gizmos.DrawLine(left, right);
            Gizmos.DrawLine(left, left + Vector3.up * 2f);
            Gizmos.DrawLine(right, right + Vector3.up * 2f);
            Gizmos.DrawLine(left + Vector3.up * 2f, right + Vector3.up * 2f);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(Position, Forward * 2f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(Position, 0.5f);
        }
    }
}
