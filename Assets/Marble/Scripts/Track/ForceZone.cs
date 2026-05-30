using UnityEngine;

namespace Marble
{
    [RequireComponent(typeof(Collider))]
    public class ForceZone : MonoBehaviour
    {
        [Header("Force Settings")]
        [SerializeField] private float force = 10f;
        [SerializeField] private ForceMode forceMode = ForceMode.Impulse;

        [Header("Direction")]
        [SerializeField] private DirectionType directionType = DirectionType.Forward;
        [SerializeField] private Vector3 customDirection = Vector3.forward;

        [Header("Options")]
        [SerializeField] private bool continuousForce = false;

        public enum DirectionType
        {
            Forward,
            Backward,
            Up,
            Custom,
            AwayFromCenter
        }

        private void OnTriggerEnter(Collider other)
        {
            if (continuousForce) return;
            TryApplyForce(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!continuousForce) return;
            TryApplyForce(other);
        }

        private void TryApplyForce(Collider other)
        {
            var rb = other.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = other.GetComponentInParent<Rigidbody>();
            }

            if (rb == null) return;

            Vector3 dir = GetForceDirection(other.transform.position);
            rb.AddForce(dir * force, forceMode);
        }

        private Vector3 GetForceDirection(Vector3 targetPosition)
        {
            switch (directionType)
            {
                case DirectionType.Forward:
                    return transform.forward;
                case DirectionType.Backward:
                    return -transform.forward;
                case DirectionType.Up:
                    return transform.up;
                case DirectionType.Custom:
                    return customDirection.normalized;
                case DirectionType.AwayFromCenter:
                    return (targetPosition - transform.position).normalized;
                default:
                    return transform.forward;
            }
        }

        private void OnDrawGizmos()
        {
            Vector3 dir = GetForceDirection(transform.position);

            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }

            Gizmos.color = Color.green;
            Vector3 start = transform.position;
            Vector3 end = start + dir * 2f;
            Gizmos.DrawLine(start, end);

            Vector3 right = Vector3.Cross(dir, Vector3.up).normalized * 0.3f;
            if (right.magnitude < 0.1f) right = Vector3.Cross(dir, Vector3.right).normalized * 0.3f;
            Gizmos.DrawLine(end, end - dir * 0.4f + right);
            Gizmos.DrawLine(end, end - dir * 0.4f - right);
        }
    }
}
