using System;
using UnityEngine;

namespace NED2024.Core
{
    /// <summary>Information passed along when something takes damage.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public bool IsHeadshot;
    }

    /// <summary>Shared health component for the player and zombies. Uses C# events (delegates) to tell other scripts what happened.</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;

        private float current;

        public float Current => current;
        public float Max => maxHealth;
        public bool IsDead { get; private set; }
        /// <summary>Used by the self-playing demo for the gameplay video: damage is shown but health never drops below 25.</summary>
        public bool GodMode { get; set; }

        public event Action<DamageInfo> Damaged;
        public event Action<float, float> Changed; // current, max
        public event Action Died;

        private void Awake()
        {
            current = maxHealth;
        }

        public void SetMax(float value, bool refill)
        {
            maxHealth = value;
            if (refill) current = maxHealth;
            Changed?.Invoke(current, maxHealth);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead) return;
            current = Mathf.Max(GodMode ? 25f : 0f, current - info.Amount);
            Damaged?.Invoke(info);
            Changed?.Invoke(current, maxHealth);
            if (current <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }

        /// <summary>Sets the current health directly (used when progress is restored).</summary>
        public void SetCurrent(float value)
        {
            if (IsDead) return;
            current = Mathf.Clamp(value, 1f, maxHealth);
            Changed?.Invoke(current, maxHealth);
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            current = Mathf.Min(maxHealth, current + amount);
            Changed?.Invoke(current, maxHealth);
        }
    }
}
