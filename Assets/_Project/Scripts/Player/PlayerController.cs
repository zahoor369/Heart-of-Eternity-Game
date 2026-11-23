using System.Collections;
using UnityEngine;
using NED2024.Core;

namespace NED2024.Player
{
    /// <summary>
    /// Third-person character controller: camera-relative movement with acceleration, jog / sprint / aim-walk,
    /// jump, ground detection, smooth turning, footsteps, hit reactions and death. Drives the Animator.
    /// Uses the legacy Input Manager (through GameInput).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private NED2024.CameraSystem.ThirdPersonCameraController cameraController;
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField] private AudioClip hurtClip;
        [SerializeField] private AudioClip jumpClip;

        [Header("Movement")]
        [SerializeField] private float jogSpeed = 3.6f;
        [SerializeField] private float sprintSpeed = 5.6f;
        [SerializeField] private float aimSpeed = 2.0f;
        [SerializeField] private float acceleration = 14f;     // metres per second squared
        [SerializeField] private float deceleration = 18f;
        [SerializeField] private float turnSpeed = 600f;       // degrees per second
        [SerializeField] private float jumpHeight = 1.0f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float pushImpulse = 2.5f;     // impulse applied to loose physics props we walk into
        [SerializeField] private float stepDistance = 1.9f;

        private CharacterController controller;
        private Health health;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float distanceSinceStep;
        private int stepIndex;
        private float animSpeed;
        private float animReverse = 1f;
        private float aimPitch;
        private bool dead;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int AimingHash = Animator.StringToHash("Aiming");
        private static readonly int AimPitchHash = Animator.StringToHash("AimPitch");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int ReverseHash = Animator.StringToHash("Reverse");
        private static readonly int WeaponHash = Animator.StringToHash("Weapon");

        public bool IsAiming { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsDead => dead;
        public Health Health => health;
        public float HorizontalSpeed => horizontalVelocity.magnitude;
        /// <summary>Yaw (degrees) the camera is looking along. The torso twists towards this while the legs follow the movement.</summary>
        public float AimYaw { get; private set; }
        
        private bool InputAllowed =>
            !dead && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (GameManager.Instance != null) GameManager.Instance.RegisterPlayer(transform);
            AimYaw = transform.eulerAngles.y;
        }

        /// <summary>Called by the weapon system to allow or block the aim pose.</summary>
        public bool AimAllowed { get; set; } = false;   // the explorer carries no weapon

        private void Update()
        {
            // The gameplay camera is switched on after the intro cinematic, so look it up lazily.
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) AimYaw = cameraTransform.eulerAngles.y;

            Vector2 input = Vector2.zero;
            bool jumpPressed = false;
            IsAiming = false;
            IsSprinting = false;

            if (InputAllowed)
            {
                input = new Vector2(GameInput.Horizontal, GameInput.Vertical);
                input = Vector2.ClampMagnitude(input, 1f);
                IsAiming = GameInput.Aim && AimAllowed;
                IsSprinting = GameInput.Sprint && !IsAiming && input.y > 0.1f;
                jumpPressed = GameInput.JumpDown;
            }

            // Camera-relative direction: use only the camera's yaw so looking up or down does not slow us.
            Vector3 moveDir = Vector3.zero;
            if (cameraTransform != null && input.sqrMagnitude > 0.01f)
            {
                Quaternion yaw = Quaternion.Euler(0f, AimYaw, 0f);
                moveDir = yaw * new Vector3(input.x, 0f, input.y);
            }

            float targetSpeed = IsAiming ? aimSpeed : (IsSprinting ? sprintSpeed : jogSpeed);
            Vector3 targetVelocity = moveDir * targetSpeed;
            float rate = (targetVelocity.sqrMagnitude > 0.01f) ? acceleration : deceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);

            ApplyRotation(moveDir);
            ApplyVertical(jumpPressed);

            Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);

            UpdateFootsteps();
            UpdateAnimator(moveDir);
        }

        private void ApplyRotation(Vector3 moveDir)
        {
            if (dead || cameraTransform == null) return;
            Quaternion target = transform.rotation;
            Quaternion cameraFacing = Quaternion.Euler(0f, AimYaw, 0f);
            bool wantsToShoot = IsAiming;

            if (wantsToShoot)
            {
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    // Moving while aiming: the legs run in the movement direction (or face the camera when
                    // backing away) and the torso twist in PlayerLookIK keeps the weapon pointing at the target.
                    Vector3 face = moveDir;
                    if (Vector3.Dot(moveDir, cameraFacing * Vector3.forward) < -0.3f) face = -moveDir;
                    target = Quaternion.LookRotation(face, Vector3.up);
                }
                else
                {
                    // Standing still: only turn the feet once the torso would twist too far.
                    float delta = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, AimYaw));
                    target = delta > 55f ? cameraFacing : transform.rotation;
                }
            }
            else if (moveDir.sqrMagnitude > 0.01f)
            {
                target = Quaternion.LookRotation(moveDir, Vector3.up);
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        private void ApplyVertical(bool jumpPressed)
        {
            bool grounded = controller.isGrounded;
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (grounded && jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                if (animator != null) animator.SetTrigger(JumpHash);
                AudioManager.Instance.PlaySfx(jumpClip, 0.3f);
            }
            verticalVelocity += gravity * Time.deltaTime;
        }

        private void UpdateFootsteps()
        {
            if (!controller.isGrounded || horizontalVelocity.magnitude < 0.5f) return;
            distanceSinceStep += horizontalVelocity.magnitude * Time.deltaTime;
            float needed = IsSprinting ? stepDistance * 1.2f : stepDistance;
            if (distanceSinceStep >= needed && footstepClips != null && footstepClips.Length > 0)
            {
                distanceSinceStep = 0f;
                // The modulo operator makes the index wrap around the clip list so the sounds alternate.
                stepIndex = (stepIndex + 1) % footstepClips.Length;
                AudioManager.Instance.PlaySfx(footstepClips[stepIndex], IsSprinting ? 0.6f : 0.35f);
            }
        }

        private void UpdateAnimator(Vector3 moveDir)
        {
            if (animator == null) return;
            float normalised = Mathf.Clamp01(horizontalVelocity.magnitude / sprintSpeed);
            animSpeed = Mathf.Lerp(animSpeed, normalised, 10f * Time.deltaTime);
            animator.SetFloat(SpeedHash, animSpeed);
            animator.SetBool(GroundedHash, controller.isGrounded);
            animator.SetBool(AimingHash, IsAiming);
            if (animator.layerCount > 1)
            {
                // While sprinting the upper-body layer fades out so the arms swing naturally; aiming or standing brings it back.
                float wantedWeight = IsSprinting ? 0.15f : 1f;
                animator.SetLayerWeight(1, Mathf.MoveTowards(animator.GetLayerWeight(1), dead ? 0f : wantedWeight, 5f * Time.deltaTime));
            }

            // Walking backwards while aiming: play the walk animation in reverse instead of turning the character around.
            bool backwards = IsAiming && moveDir.sqrMagnitude > 0.01f &&
                             Vector3.Dot(moveDir, Quaternion.Euler(0f, AimYaw, 0f) * Vector3.forward) < -0.3f;
            animReverse = Mathf.MoveTowards(animReverse, backwards ? -1f : 1f, 8f * Time.deltaTime);
            animator.SetFloat(ReverseHash, animReverse);

            // Aim pitch: camera looking up gives +1, looking down gives -1 (blends the aim-up / aim-down poses).
            if (cameraController != null)
            {
                // Pitch of the line from the chest to the point under the crosshair. The camera sits above the player,
                // so the camera angle itself would make the character bend over; this angle is what the gun really points at.
                Vector3 toLook = cameraController.LookPoint.position - (transform.position + Vector3.up * 1.4f);
                float pitchDeg = Mathf.Asin(Mathf.Clamp(toLook.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;   // positive = up
                aimPitch = Mathf.Lerp(aimPitch, Mathf.Clamp(pitchDeg / 30f, -0.35f, 1f), 10f * Time.deltaTime);
                animator.SetFloat(AimPitchHash, aimPitch);
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            AudioManager.Instance.PlaySfx(hurtClip, 0.8f);
            if (animator != null && !health.IsDead) animator.SetTrigger(HitHash);
        }

        private void OnDied()
        {
            dead = true;
            horizontalVelocity = Vector3.zero;
            if (animator != null) animator.SetTrigger(DieHash);
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            yield return new WaitForSeconds(2.4f);
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.PlayerDied();
                gm.FailRun(string.IsNullOrEmpty(gm.EndReason) ? "The temple claimed another explorer." : gm.EndReason);
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Walking into a loose prop gives it a small push (an impulse force).
            Rigidbody body = hit.collider.attachedRigidbody;
            if (body != null && !body.isKinematic && hit.moveDirection.y > -0.3f)
            {
                Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
                body.AddForceAtPosition(push * pushImpulse, hit.point, ForceMode.Impulse);
            }
        }
    }
}
