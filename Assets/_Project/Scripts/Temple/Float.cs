using UnityEngine;

namespace NED2024.Temple
{
    /// <summary>Slow floating motion for crystals and holograms.</summary>
    public class Float : MonoBehaviour
    {
        [SerializeField] private float height = 0.15f;
        [SerializeField] private float speed = 1.2f;
        private Vector3 start; private float phase;
        private void Start() { start = transform.localPosition; phase = Random.value * 6.28f; }
        private void Update() { transform.localPosition = start + Vector3.up * (Mathf.Sin(Time.time * speed + phase) * height); }
    }
}
