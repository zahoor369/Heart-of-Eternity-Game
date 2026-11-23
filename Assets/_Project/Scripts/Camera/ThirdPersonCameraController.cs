using Unity.Cinemachine;
using UnityEngine;
using NED2024.Core;

namespace NED2024.CameraSystem
{
    /// <summary>
    /// Drives two Cinemachine cameras (normal and aim) with the mouse.
    /// Both use Orbital Follow to orbit the player and Hard Look At to keep the target centred.
    /// Aiming raises the priority of the aim camera, and the Brain blends between them.
    /// </summary>
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera normalCamera;
        [SerializeField] private CinemachineCamera aimCamera;
        [SerializeField] private CinemachineOrbitalFollow normalOrbit;
        [SerializeField] private CinemachineOrbitalFollow aimOrbit;
        [SerializeField] private float degreesPerMouseUnit = 2.2f;
        [SerializeField] private Vector2 pitchRange = new Vector2(-25f, 55f);
        [SerializeField] private float recoilRecovery = 8f;
        [SerializeField] private LayerMask lookMask = ~0;

        [Header("Over-the-shoulder pivots")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private float pivotHeight = 1.6f;
        [SerializeField] private float shoulderNormal = 0.65f;
        [SerializeField] private float shoulderAim = 0.95f;

        private float pendingRecoil;
        private bool aiming;
        private Transform normalPivot;
        private Transform aimPivot;

        public static ThirdPersonCameraController Instance { get; private set; }
        public Transform LookPoint { get; private set; }

        private float shakeAmplitude, shakeUntil, shakeStart;
        private Vector2 lastShake;

        /// <summary>Shakes the view (earthquake, door slam). Amplitude is in degrees.</summary>
        public static void Shake(float amplitude, float seconds)
        {
            if (Instance == null) return;
            Instance.shakeAmplitude = amplitude; Instance.shakeStart = Time.time; Instance.shakeUntil = Time.time + seconds;
        }
        public bool IsAimCamera => aiming;

        private void Awake()
        {
            // Empty object that always sits on the point the crosshair is aiming at (used by IK).
            Instance = this;
            LookPoint = new GameObject("AimPoint").transform;
            // Each camera follows and looks at its own pivot, placed to the right of the player.
            // Hard Look At keeps the pivot centred, so the player ends up on the left of the screen
            // (over-the-shoulder view) and the crosshair has a clear line of sight.
            normalPivot = new GameObject("PivotNormal").transform;
            aimPivot = new GameObject("PivotAim").transform;
        }

        private void Start()
        {
            ConfigureAxis(normalOrbit);
            ConfigureAxis(aimOrbit);
            normalCamera.Follow = normalPivot; normalCamera.LookAt = normalPivot;
            aimCamera.Follow = aimPivot; aimCamera.LookAt = aimPivot;
            UpdatePivots();
            normalCamera.Priority = 10;
            aimCamera.Priority = 5;
        }

        private void UpdatePivots()
        {
            if (playerTarget == null) return;
            var cam = Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : playerTarget.eulerAngles.y;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 basePos = playerTarget.position + Vector3.up * pivotHeight;
            normalPivot.position = basePos + right * shoulderNormal;
            aimPivot.position = basePos + right * shoulderAim;
        }

        private void ConfigureAxis(CinemachineOrbitalFollow orbit)
        {
            if (orbit == null) return;
            var v = orbit.VerticalAxis;
            v.Range = pitchRange;
            v.Value = 12f;
            v.Wrap = false;
            orbit.VerticalAxis = v;
            var h = orbit.HorizontalAxis;
            h.Wrap = true;
            orbit.HorizontalAxis = h;
        }

        public void SetAiming(bool value)
        {
            if (aiming == value) return;
            aiming = value;
            aimCamera.Priority = value ? 20 : 5;
        }

        /// <summary>Turns the camera sideways by a number of degrees (used by checkpoints and the automated playtest).</summary>
        public void AddYaw(float degrees)
        {
            Apply(normalOrbit, degrees, 0f, 0f);
            Apply(aimOrbit, degrees, 0f, 0f);
        }

        /// <summary>Changes the vertical angle of both cameras (used by checkpoints and the automated playtest).</summary>
        public void AddPitch(float degrees)
        {
            foreach (var orbit in new[] { normalOrbit, aimOrbit })
            {
                if (orbit == null) continue;
                var v = orbit.VerticalAxis;
                v.Value = Mathf.Clamp(v.Value + degrees, pitchRange.x, pitchRange.y);
                orbit.VerticalAxis = v;
            }
        }

        /// <summary>Sets the vertical angle of both cameras directly.</summary>
        public void SetPitch(float degrees)
        {
            foreach (var orbit in new[] { normalOrbit, aimOrbit })
            {
                if (orbit == null) continue;
                var v = orbit.VerticalAxis;
                v.Value = Mathf.Clamp(degrees, pitchRange.x, pitchRange.y);
                orbit.VerticalAxis = v;
            }
        }

        /// <summary>Kicks the view upwards a little when a weapon fires.</summary>
        public void AddRecoil(float degrees, float yawDegrees = 0f)
        {
            pendingRecoil += degrees;
            pendingYaw += yawDegrees;
        }

        private float pendingYaw;

        private void Update()
        {
            UpdatePivots();
            var gm = GameManager.Instance;
            bool allowed = gm != null && gm.State == GameState.Playing;

            float dx = 0f, dy = 0f;
            if (allowed)
            {
                float sens = degreesPerMouseUnit * GameSettings.MouseSensitivity * (aiming ? 0.6f : 1f);
                dx = GameInput.LookX * sens;
                dy = GameInput.LookY * sens;
            }

            // Recoil is applied smoothly over a few frames.
            float recoilStep = Mathf.Min(pendingRecoil, pendingRecoil * recoilRecovery * Time.deltaTime + 0.01f);
            pendingRecoil -= recoilStep;

            float yawStep = pendingYaw * Mathf.Min(1f, recoilRecovery * Time.deltaTime);
            pendingYaw -= yawStep;
            // Shake: a noise offset is added to the angles and the previous offset removed again, so nothing drifts.
            Vector2 shake = Vector2.zero;
            if (Time.time < shakeUntil)
            {
                float fade = Mathf.Clamp01((shakeUntil - Time.time) / Mathf.Max(0.5f, (shakeUntil - shakeStart) * 0.5f));
                shake = new Vector2(Mathf.PerlinNoise(Time.time * 18f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, Time.time * 18f) - 0.5f) * 2f * shakeAmplitude * fade;
            }
            Vector2 shakeDelta = shake - lastShake;
            lastShake = shake;
            Apply(normalOrbit, dx + yawStep + shakeDelta.x, dy - shakeDelta.y, recoilStep);
            Apply(aimOrbit, dx + yawStep + shakeDelta.x, dy - shakeDelta.y, recoilStep);

            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 origin = cam.transform.position, dir = cam.transform.forward;
                Vector3 point = origin + dir * 40f;
                if (Physics.Raycast(origin, dir, out RaycastHit lookHit, 60f, lookMask, QueryTriggerInteraction.Ignore))
                    point = origin + dir * Mathf.Max(lookHit.distance, 6f);   // never look at something extremely close
                LookPoint.position = point;
            }
        }

        private void Apply(CinemachineOrbitalFollow orbit, float dx, float dy, float recoil)
        {
            if (orbit == null) return;
            var h = orbit.HorizontalAxis;
            h.Value += dx;
            orbit.HorizontalAxis = h;

            var v = orbit.VerticalAxis;
            // Moving the mouse up lowers the camera angle so the view tilts up. Recoil does the same.
            v.Value = Mathf.Clamp(v.Value - dy - recoil, pitchRange.x, pitchRange.y);
            orbit.VerticalAxis = v;
        }
    }
}
