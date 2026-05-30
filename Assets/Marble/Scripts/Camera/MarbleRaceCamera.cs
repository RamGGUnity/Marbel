using UnityEngine;
using Unity.Cinemachine;

namespace Marble
{
    public class MarbleRaceCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MarbleRaceController raceController;
        // Assign Main Camera here, or leave empty to use Camera.main automatically.
        [SerializeField] private Transform cameraTransform;

        [Header("Camera Position")]
        [SerializeField] private float followDistance = 7f;
        [SerializeField] private float followHeight   = 3.5f;
        // Smaller = faster catch-up (0.1 = snappy, 0.3 = smooth, 0.5 = lazy)
        [SerializeField] private float positionSmoothTime = 0.2f;

        [Header("Turn Follow")]
        // How fast camera swings behind on direction change (higher = quicker)
        [SerializeField] private float directionSmoothing = 3f;

        [Header("Look At")]
        [SerializeField] private float lookAtHeightOffset = 0.5f;

        [Header("Target Transfer")]
        // How long (seconds) to glide from old marble to new when leader changes.
        [SerializeField] private float targetTransferSmoothTime = 0.4f;

        private Transform _currentTarget;
        private Transform _pendingTarget;
        private byte      _currentLeaderId = 255;

        private Vector3 _smoothBehindDir = Vector3.back;
        private Vector3 _smoothCamPos;
        private Vector3 _camVelocity;     // SmoothDamp velocity — camera position

        // Smoothed world position that glides between marbles on leader change.
        // Camera always reads from here, never from _currentTarget.position directly.
        private Vector3 _trackedPos;
        private Vector3 _trackedVelocity; // SmoothDamp velocity — tracked position

        private void Awake()
        {
            if (cameraTransform == null)
                cameraTransform = Camera.main?.transform;

            if (cameraTransform == null)
            {
                Debug.LogError("[MarbleRaceCamera] No camera found. Assign cameraTransform or tag a camera as MainCamera.");
                return;
            }

            // Seed smooth position from the scene-placed camera — prevents the
            // "wrong place then jumps to correct place" issue on first frame.
            _smoothCamPos = cameraTransform.position;

            // Disable Cinemachine Brain so this script has full transform control.
            var brain = cameraTransform.GetComponent<CinemachineBrain>();
            if (brain != null)
                brain.enabled = false;
        }

        private void Start()
        {
            if (raceController?.PositionTracker != null)
                raceController.PositionTracker.OnPositionsChanged += HandlePositionsChanged;
        }

        private void OnDestroy()
        {
            if (raceController?.PositionTracker != null)
                raceController.PositionTracker.OnPositionsChanged -= HandlePositionsChanged;
        }

        private void LateUpdate()
        {
            if (raceController == null || cameraTransform == null) return;

            UpdateLeaderTarget();

            if (_currentTarget != null)
                UpdateCamera();
        }

        // ── Leader tracking ──────────────────────────────────────────────────

        private void UpdateLeaderTarget()
        {
            if (raceController.PositionTracker == null) return;

            byte leaderId = raceController.PositionTracker.GetMarbleAtPosition(0);

            if (leaderId != _currentLeaderId || _currentTarget == null)
            {
                _currentLeaderId = leaderId;
                Transform newTarget = GetMarbleTransform(leaderId);

                if (newTarget != null)
                {
                    _pendingTarget = newTarget;

                    if (_currentTarget == null)
                    {
                        _currentTarget = newTarget;
                        InitializeFromScenePlacement();
                    }
                }
            }

            if (_pendingTarget != null && _pendingTarget != _currentTarget)
                _currentTarget = _pendingTarget;
        }

        // ── Camera init ──────────────────────────────────────────────────────

        // Called once when the first marble target is found.
        // Uses the camera's current scene-placed position to derive the initial
        // behind direction — so no jump on game start.
        private void InitializeFromScenePlacement()
        {
            if (_currentTarget == null || cameraTransform == null) return;

            Vector3 marblePos = _currentTarget.position;

            // Horizontal vector from marble to where camera already is in scene
            Vector3 toCamera = cameraTransform.position - marblePos;
            toCamera.y = 0f;

            if (toCamera.sqrMagnitude > 0.01f)
                _smoothBehindDir = toCamera.normalized;
            // else keep Vector3.back as fallback

            // Seed both smooth positions from scene placement — prevents jumps on start.
            _trackedPos      = marblePos;
            _trackedVelocity = Vector3.zero;
            _smoothCamPos    = cameraTransform.position;
            _camVelocity     = Vector3.zero;

            cameraTransform.LookAt(marblePos + Vector3.up * lookAtHeightOffset);
        }

        // ── GTA-style follow update ──────────────────────────────────────────

        private void UpdateCamera()
        {
            // Step 1 — glide the tracked point toward the current marble.
            // When the leader changes, _currentTarget.position jumps but _trackedPos
            // flows smoothly, so the camera never sees a sudden jump.
            _trackedPos = Vector3.SmoothDamp(
                _trackedPos,
                _currentTarget.position,
                ref _trackedVelocity,
                targetTransferSmoothTime
            );

            // Step 2 — update behind-direction from real marble velocity only.
            Vector3 velocity = GetMarbleVelocity(_currentLeaderId);
            Vector3 flatVel  = new Vector3(velocity.x, 0f, velocity.z);

            if (flatVel.sqrMagnitude > 0.5f)
            {
                float t = 1f - Mathf.Exp(-directionSmoothing * Time.deltaTime);
                _smoothBehindDir = Vector3.Slerp(_smoothBehindDir, -flatVel.normalized, t);
            }

            // Step 3 — desired camera position relative to the smoothed tracked point.
            Vector3 desiredCamPos = _trackedPos
                + _smoothBehindDir * followDistance
                + Vector3.up * followHeight;

            // Step 4 — spring-physics camera move (no stutter, no overshoot).
            _smoothCamPos = Vector3.SmoothDamp(
                _smoothCamPos,
                desiredCamPos,
                ref _camVelocity,
                positionSmoothTime
            );

            cameraTransform.position = _smoothCamPos;
            cameraTransform.LookAt(_trackedPos + Vector3.up * lookAtHeightOffset);
        }

        // ── Marble helpers ───────────────────────────────────────────────────

        private Transform GetMarbleTransform(byte marbleId)
        {
            var controllers = raceController.PhysicsControllers;
            if (controllers != null && marbleId < controllers.Length && controllers[marbleId] != null)
                return controllers[marbleId].transform;

            var visuals = raceController.PhysicsVisuals;
            if (visuals != null && marbleId < visuals.Length && visuals[marbleId] != null)
                return visuals[marbleId].transform;

            return null;
        }

        private Vector3 GetMarbleVelocity(byte marbleId)
        {
            var controllers = raceController.PhysicsControllers;
            if (controllers != null && marbleId < controllers.Length && controllers[marbleId] != null)
                return controllers[marbleId].Velocity;

            var visuals = raceController.PhysicsVisuals;
            if (visuals != null && marbleId < visuals.Length && visuals[marbleId] != null)
            {
                var rb = visuals[marbleId].GetComponent<Rigidbody>();
                if (rb != null) return rb.linearVelocity;
            }

            return Vector3.forward;
        }

        private void HandlePositionsChanged(byte[] _) { }

        // ── Public API ───────────────────────────────────────────────────────

        public void SetFollowTarget(byte marbleId)
        {
            Transform target = GetMarbleTransform(marbleId);
            if (target != null)
            {
                _currentTarget   = target;
                _currentLeaderId = marbleId;
            }
        }

        public void FollowLeader() => _currentLeaderId = 255;

        public void SnapToTarget()
        {
            if (_currentTarget != null)
                InitializeFromScenePlacement();
        }
    }
}
