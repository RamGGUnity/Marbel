using UnityEngine;

namespace Marble
{
    [RequireComponent(typeof(Collider))]
    public class TrackObstacle : MonoBehaviour
    {
        [Header("Bounce Settings")]
        [SerializeField] private bool addBounceForce = true;
        [SerializeField] private float bounceForce = 1f;
        [SerializeField] private float upwardBias = 0.1f;

        [Header("Rotation (Optional)")]
        [SerializeField] private bool rotate = false;
        [SerializeField] private Vector3 rotationSpeed = new Vector3(0, 45f, 0);

        [Header("Movement (Optional)")]
        [SerializeField] private bool move = false;
        [SerializeField] private Vector3 moveDirection = Vector3.right;
        [SerializeField] private float moveDistance = 2f;
        [SerializeField] private float moveSpeed = 2f;

        private Vector3 _startPosition;
        private float _moveProgress;

        private void Start()
        {
            _startPosition = transform.position;
        }

        private void Update()
        {
            if (rotate)
            {
                transform.Rotate(rotationSpeed * Time.deltaTime);
            }

            if (move)
            {
                _moveProgress += Time.deltaTime * moveSpeed;
                float offset = Mathf.Sin(_moveProgress) * moveDistance;
                transform.position = _startPosition + moveDirection.normalized * offset;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!addBounceForce) return;

            var rb = collision.rigidbody;
            if (rb == null) return;

            if (!collision.gameObject.CompareTag(MarbleConstants.MarbleTag)) return;

            Vector3 normal = collision.contacts[0].normal;
            Vector3 bounceDir = Vector3.Reflect(rb.linearVelocity.normalized, normal);
            bounceDir.y += upwardBias;
            bounceDir.Normalize();

            rb.AddForce(bounceDir * bounceForce, ForceMode.Impulse);
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);

            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            }
            else if (col is CapsuleCollider capsule)
            {
                Gizmos.DrawWireSphere(transform.position, capsule.radius);
            }

            if (move)
            {
                Gizmos.color = Color.yellow;
                Vector3 start = Application.isPlaying ? _startPosition : transform.position;
                Vector3 dir = moveDirection.normalized;
                Gizmos.DrawLine(start - dir * moveDistance, start + dir * moveDistance);
            }
        }
    }
}
