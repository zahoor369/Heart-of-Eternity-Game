using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using NED2024.Core;

namespace NED2024.Temple
{
    public enum SentinelState { Patrol, Alert, Chase, Attack, Stunned, Search }

    /// <summary>
    /// A stone guardian that patrols the maze. It sees the explorer in front of it, hears running nearby, chases at a walking pace
    /// (sprinting outruns it) and strikes when close. The Echo Lens stuns it for a few seconds. Its limbs are moved by code.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class SentinelAI : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform legL, legR, armL, armR;
        [SerializeField] private Renderer eyeRenderer;
        [SerializeField] private Light eyeLight;
        [SerializeField] private Transform eyes;

        [Header("Behaviour")]
        [SerializeField] private float patrolSpeed = 1.5f;
        [SerializeField] private float chaseSpeed = 3.0f;
        [SerializeField] private float sightRange = 13f;
        [SerializeField] private float fieldOfView = 150f;
        [SerializeField] private float hearingRange = 7f;
        [SerializeField] private float attackRange = 1.9f;
        [SerializeField] private float attackDamage = 30f;
        [SerializeField] private LayerMask sightMask = ~0;
        [SerializeField] private List<Vector3> waypoints = new List<Vector3>();

        public SentinelState State { get; private set; } = SentinelState.Patrol;

        private NavMeshAgent agent;
        private Transform player;
        private int waypointIndex;
        private float stateTimer;
        private float lastSeen;
        private Vector3 lastKnown;
        private float nextAttack;
        private float attackWind;
        private float stunUntil;
        private float stepClock;
        private MaterialPropertyBlock block;
        private bool lastStepSide;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            block = new MaterialPropertyBlock();
        }

        public void SetWaypoints(IEnumerable<Vector3> points) { waypoints = new List<Vector3>(points); }

        private void Start()
        {
            // The NavMesh is baked into the scene, so the agent can be switched on now.
            agent.enabled = true;
            if (agent.isOnNavMesh) { agent.speed = patrolSpeed; GoToNextWaypoint(); }
            Paint(new Color(1f, 0.65f, 0.15f), 2.2f);
        }

        private bool Active
        {
            get { var gm = GameManager.Instance; return gm != null && gm.State == GameState.Playing; }
        }

        /// <summary>Freezes the sentinel for a while. Returns true when it was not already stunned.</summary>
        public bool Stun(float seconds)
        {
            bool fresh = State != SentinelState.Stunned;
            stunUntil = Time.time + seconds;
            State = SentinelState.Stunned;
            if (agent.isOnNavMesh) agent.isStopped = true;
            Paint(new Color(0.2f, 0.7f, 1f), 0.5f);
            return fresh;
        }

        private void Update()
        {
            if (player == null && GameManager.Instance != null) player = GameManager.Instance.PlayerTransform;
            if (!agent.isOnNavMesh) return;

            if (!Active) { agent.isStopped = true; return; }

            switch (State)
            {
                case SentinelState.Patrol: UpdatePatrol(); break;
                case SentinelState.Alert: UpdateAlert(); break;
                case SentinelState.Chase: UpdateChase(); break;
                case SentinelState.Attack: UpdateAttack(); break;
                case SentinelState.Stunned: UpdateStunned(); break;
                case SentinelState.Search: UpdateSearch(); break;
            }
            Animate();
        }

        // ---------------------------------------------------------------- states

        private void UpdatePatrol()
        {
            agent.isStopped = false; agent.speed = patrolSpeed;
            if (!agent.pathPending && agent.remainingDistance < 0.8f)
            {
                stateTimer += Time.deltaTime;
                if (stateTimer > 1.6f) { GoToNextWaypoint(); stateTimer = 0f; }
            }
            if (CanNoticePlayer()) Notice();
        }

        private void Notice()
        {
            State = SentinelState.Alert; stateTimer = 0f;
            agent.isStopped = true;
            Sfx.PlayAt("sentinel_alert", transform.position, 0.9f);
            Paint(new Color(1f, 0.15f, 0.1f), 4f);
        }

        private void UpdateAlert()
        {
            // a short moment of surprise: turn towards the explorer, then give chase
            if (player != null) Face(player.position, 360f);
            stateTimer += Time.deltaTime;
            if (stateTimer > 0.7f) { State = SentinelState.Chase; lastSeen = Time.time; }
        }

        private void UpdateChase()
        {
            if (player == null) { State = SentinelState.Patrol; return; }
            agent.isStopped = false; agent.speed = chaseSpeed;
            bool sees = HasLineOfSight() && Distance() < sightRange * 1.3f;
            if (sees) { lastSeen = Time.time; lastKnown = player.position; }
            agent.SetDestination(sees ? player.position : lastKnown);

            if (Distance() < attackRange && sees) { State = SentinelState.Attack; attackWind = 0f; agent.isStopped = true; return; }
            if (!sees && Time.time - lastSeen > 4f)
            {
                State = SentinelState.Search; stateTimer = 0f;
                Paint(new Color(1f, 0.65f, 0.15f), 2.2f);
            }
        }

        private void UpdateAttack()
        {
            if (player != null) Face(player.position, 480f);
            attackWind += Time.deltaTime;
            if (attackWind > 0.55f && Time.time >= nextAttack)
            {
                nextAttack = Time.time + 1.3f;
                if (player != null && Distance() < attackRange + 0.7f)
                {
                    var hp = player.GetComponent<Health>();
                    if (hp != null) hp.TakeDamage(new DamageInfo { Amount = attackDamage, Point = player.position + Vector3.up, Direction = (player.position - transform.position).normalized });
                    Sfx.PlayAt("door_slam", transform.position, 0.45f);
                }
                State = SentinelState.Chase;
            }
        }

        private void UpdateStunned()
        {
            if (Time.time >= stunUntil)
            {
                State = SentinelState.Search; stateTimer = 0f;
                agent.isStopped = false;
                Paint(new Color(1f, 0.65f, 0.15f), 2.2f);
            }
        }

        private void UpdateSearch()
        {
            agent.isStopped = false; agent.speed = patrolSpeed;
            agent.SetDestination(lastKnown == Vector3.zero ? transform.position : lastKnown);
            stateTimer += Time.deltaTime;
            if (CanNoticePlayer()) { Notice(); return; }
            if (stateTimer > 5f || (!agent.pathPending && agent.remainingDistance < 1f && stateTimer > 1.5f)) { State = SentinelState.Patrol; GoToNextWaypoint(); }
        }

        // ---------------------------------------------------------------- senses

        private float Distance() { return player == null ? 999f : Vector3.Distance(transform.position, player.position); }

        private bool CanNoticePlayer()
        {
            if (player == null) return false;
            var hp = player.GetComponent<Health>();
            if (hp != null && hp.IsDead) return false;
            float d = Distance();
            if (d < 2.2f) return true;
            // hearing: running makes noise
            var pc = player.GetComponent<NED2024.Player.PlayerController>();
            if (pc != null && pc.IsSprinting && d < hearingRange) { lastKnown = player.position; return true; }
            if (d > sightRange) return false;
            Vector3 to = player.position - transform.position;
            if (Vector3.Angle(transform.forward, to) > fieldOfView * 0.5f) return false;
            return HasLineOfSight();
        }

        private bool HasLineOfSight()
        {
            if (player == null) return false;
            Vector3 from = (eyes != null ? eyes.position : transform.position + Vector3.up * 1.8f);
            Vector3 to = player.position + Vector3.up * 1.2f;
            if (Physics.Linecast(from, to, out RaycastHit hit, sightMask, QueryTriggerInteraction.Ignore))
                return hit.transform.IsChildOf(player) || hit.transform == player;
            return true;
        }

        // ---------------------------------------------------------------- movement helpers

        private void GoToNextWaypoint()
        {
            if (waypoints.Count == 0 || !agent.isOnNavMesh) return;
            waypointIndex = (waypointIndex + 1) % waypoints.Count;
            agent.SetDestination(waypoints[waypointIndex]);
        }

        private void Face(Vector3 point, float degreesPerSecond)
        {
            Vector3 d = point - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), degreesPerSecond * Time.deltaTime);
        }

        private void Paint(Color c, float intensity)
        {
            if (eyeRenderer != null)
            {
                eyeRenderer.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", c * intensity);
                block.SetColor("_BaseColor", c * 0.3f);
                eyeRenderer.SetPropertyBlock(block);
            }
            if (eyeLight != null) { eyeLight.color = c; eyeLight.intensity = intensity * 0.9f; }
        }

        // ---------------------------------------------------------------- procedural animation

        private void Animate()
        {
            float speed = agent.velocity.magnitude;
            float amount = Mathf.Clamp01(speed / 2.5f);
            stepClock += speed * Time.deltaTime * 1.6f;
            float swing = Mathf.Sin(stepClock) * 32f * amount;
            if (legL != null) legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            float armRaise = 0f;
            if (State == SentinelState.Attack) armRaise = Mathf.Lerp(0f, -130f, Mathf.Clamp01(attackWind / 0.5f));
            if (State == SentinelState.Stunned) armRaise = 0f;
            if (armL != null) armL.localRotation = Quaternion.Euler(-swing * 0.8f + armRaise, 0f, 0f);
            if (armR != null) armR.localRotation = Quaternion.Euler(swing * 0.8f + armRaise, 0f, 0f);
            if (body != null) body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(stepClock)) * 0.06f * amount, 0f);

            bool side = Mathf.Sin(stepClock) > 0f;
            if (amount > 0.3f && side != lastStepSide && Distance() < 22f) Sfx.PlayAt("sentinel_step", transform.position, 0.5f);
            lastStepSide = side;
        }
    }
}
