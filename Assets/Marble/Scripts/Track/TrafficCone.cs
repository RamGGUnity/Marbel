using UnityEngine;

namespace Marble
{
    [RequireComponent(typeof(Collider))]
    public class TrafficCone : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField] private bool canBeKnockedOver = true;
        [SerializeField] private float mass = 0.5f;
        [SerializeField] private float drag = 1f;
        [SerializeField] private float angularDrag = 0.5f;

        [Header("Reset")]
        [SerializeField] private bool autoReset = true;
        [SerializeField] private float resetDelay = 5f;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Rigidbody _rigidbody;
        private bool _isKnockedOver;
        private float _knockedTime;

        private void Awake()
        {
            _startPosition = transform.position;
            _startRotation = transform.rotation;

            if (canBeKnockedOver)
            {
                SetupRigidbody();
            }
        }

        private void SetupRigidbody()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
            }

            _rigidbody.mass = mass;
            _rigidbody.linearDamping = drag;
            _rigidbody.angularDamping = angularDrag;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void Update()
        {
            if (!autoReset || !_isKnockedOver) return;

            if (Time.time - _knockedTime >= resetDelay)
            {
                ResetCone();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!canBeKnockedOver) return;
            if (!collision.gameObject.CompareTag(MarbleConstants.MarbleTag)) return;

            _isKnockedOver = true;
            _knockedTime = Time.time;
        }

        public void ResetCone()
        {
            _isKnockedOver = false;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            transform.position = _startPosition;
            transform.rotation = _startRotation;
        }

        public void ResetAllCones()
        {
            var cones = FindObjectsByType<TrafficCone>(FindObjectsSortMode.None);
            foreach (var cone in cones)
            {
                cone.ResetCone();
            }
        }
    }
}
