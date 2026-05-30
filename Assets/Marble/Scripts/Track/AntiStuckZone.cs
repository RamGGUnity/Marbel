using UnityEngine;

namespace Marble
{
    [RequireComponent(typeof(Collider))]
    public class AntiStuckZone : MonoBehaviour
    {
        [Header("Force Settings")]
        [SerializeField] private float unstuckForce = 15f;
        [SerializeField] private float stuckSpeedThreshold = 0.5f;
        [SerializeField] private float stuckTimeThreshold = 0.5f;

        [Header("Direction")]
        [SerializeField] private ForceDirection forceDirection = ForceDirection.Up;
        [SerializeField] private Vector3 customDirection = Vector3.up;

        public enum ForceDirection
        {
            Up,
            Down,
            Forward,
            Backward,
            Left,
            Right,
            AwayFromCenter,
            TowardCenter,
            Custom
        }

        private void OnTriggerStay(Collider other)
        {
            var rb = other.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = other.GetComponentInParent<Rigidbody>();
            }

            if (rb == null) return;

            if (rb.linearVelocity.magnitude < stuckSpeedThreshold)
            {
                Vector3 dir = GetForceDirection(other.transform.position);
                rb.AddForce(dir * unstuckForce, ForceMode.Impulse);
            }
        }

        private Vector3 GetForceDirection(Vector3 targetPosition)
        {
            switch (forceDirection)
            {
                case ForceDirection.Up:
                    return Vector3.up;
                case ForceDirection.Down:
                    return Vector3.down;
                case ForceDirection.Forward:
                    return transform.forward;
                case ForceDirection.Backward:
                    return -transform.forward;
                case ForceDirection.Left:
                    return -transform.right;
                case ForceDirection.Right:
                    return transform.right;
                case ForceDirection.AwayFromCenter:
                    Vector3 away = (targetPosition - transform.position);
                    away.y = 0.5f;
                    return away.normalized;
                case ForceDirection.TowardCenter:
                    Vector3 toward = (transform.position - targetPosition);
                    toward.y = 0.5f;
                    return toward.normalized;
                case ForceDirection.Custom:
                    return customDirection.normalized;
                default:
                    return Vector3.up;
            }
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);

            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            }

            Gizmos.color = Color.cyan;
            Vector3 dir = GetForceDirection(transform.position);
            Gizmos.DrawRay(transform.position, dir * 2f);
        }
    }
}
