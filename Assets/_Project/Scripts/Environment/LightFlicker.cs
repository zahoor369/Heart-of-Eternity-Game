using UnityEngine;

namespace NED2024.Environment
{
    /// <summary>Makes a light flicker like a damaged lamp or a fire. Uses Perlin noise so it is smooth, not random.</summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [SerializeField] private float amount = 0.35f;
        [SerializeField] private float speed = 6f;
        [SerializeField] private bool occasionalBlackout;

        private Light target;
        private float baseIntensity;
        private float seed;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed);
            float intensity = baseIntensity * (1f - amount + amount * 2f * n);
            if (occasionalBlackout && Mathf.PerlinNoise(seed + 10f, Time.time * 0.8f) > 0.86f) intensity *= 0.1f;
            target.intensity = intensity;
        }
    }
}
