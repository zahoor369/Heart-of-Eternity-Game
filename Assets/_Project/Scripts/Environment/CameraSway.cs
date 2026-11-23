using UnityEngine;

namespace NED2024.Environment
{
    /// <summary>Slow drifting camera movement for the main menu backdrop.</summary>
    public class CameraSway : MonoBehaviour
    {
        [SerializeField] private Vector3 amplitude = new Vector3(0.6f, 0.15f, 0.4f);
        [SerializeField] private float speed = 0.15f;

        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Start()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void Update()
        {
            float t = Time.time * speed;
            Vector3 offset = new Vector3(Mathf.Sin(t) * amplitude.x, Mathf.Sin(t * 1.7f) * amplitude.y, Mathf.Cos(t * 0.8f) * amplitude.z);
            transform.position = startPosition + offset;
            transform.rotation = startRotation * Quaternion.Euler(0f, Mathf.Sin(t * 0.9f) * 2.5f, 0f);
        }
    }
}
