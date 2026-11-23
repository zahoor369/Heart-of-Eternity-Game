using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NED2024.Core;

namespace NED2024.Temple
{
    /// <summary>
    /// The great boulder released when the Heart is taken. It rolls along the escape route behind the explorer at a speed
    /// between jogging and sprinting, so the player has to sprint. Touching it is fatal.
    /// </summary>
    public class RollingBoulder : MonoBehaviour
    {
        [SerializeField] private float speed = 4.7f;
        [SerializeField] private float radius = 1.7f;
        [SerializeField] private Transform visual;
        [SerializeField] private AudioSource rumbleSource;

        [SerializeField] private List<Vector3> route = new List<Vector3>();
        private int target;
        private bool running;

        public bool Running => running;

        public void SetRoute(IEnumerable<Vector3> points)
        {
            route.Clear();
            route.AddRange(points);
        }

        public void Begin(float delay)
        {
            gameObject.SetActive(true);
            if (route.Count > 0) transform.position = route[0];
            target = 1;
            StartCoroutine(Go(delay));
        }

        private IEnumerator Go(float delay)
        {
            yield return new WaitForSeconds(delay);
            running = true;
            if (rumbleSource != null) rumbleSource.Play();
            GameManager.Instance.ShowMessage("The great boulder is rolling! RUN!", 3f);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (!running || gm == null || gm.State != GameState.Playing) { if (rumbleSource != null && gm != null && gm.State != GameState.Playing) rumbleSource.Pause(); return; }
            if (rumbleSource != null && !rumbleSource.isPlaying) rumbleSource.UnPause();

            if (target < route.Count)
            {
                Vector3 to = route[target] - transform.position;
                to.y = 0f;
                float step = speed * Time.deltaTime;
                if (to.magnitude <= step) { transform.position = new Vector3(route[target].x, transform.position.y, route[target].z); target++; }
                else
                {
                    transform.position += to.normalized * step;
                    if (visual != null) visual.Rotate(Vector3.Cross(Vector3.up, to.normalized), -(step / radius) * Mathf.Rad2Deg, Space.World);
                }
            }

            var player = gm.PlayerTransform;
            if (player != null)
            {
                Vector3 d = player.position - transform.position; d.y = 0f;
                if (d.magnitude < radius + 0.35f)
                {
                    var hp = player.GetComponent<Health>();
                    if (hp != null && !hp.IsDead)
                    {
                        Sfx.PlayAt("door_slam", transform.position, 1f);
                        hp.TakeDamage(new DamageInfo { Amount = 9999f, Point = player.position, Direction = d.normalized });
                        gm.ShowMessage("Crushed by the boulder.", 2f);
                    }
                }
            }
        }
    }
}
