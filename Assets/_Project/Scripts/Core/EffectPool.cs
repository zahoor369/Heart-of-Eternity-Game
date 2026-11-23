using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NED2024.Core
{
    /// <summary>Small object pool for short effects (muzzle sparks, impacts, blood). Avoids creating and destroying objects every shot.</summary>
    public class EffectPool : MonoBehaviour
    {
        private static EffectPool instance;
        private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> prefabOf = new Dictionary<GameObject, GameObject>();

        public static EffectPool Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("EffectPool").AddComponent<EffectPool>();
                return instance;
            }
        }

        public void Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime = 1.5f)
        {
            if (prefab == null) return;
            if (!pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                pools[prefab] = queue;
            }

            GameObject fx = null;
            while (queue.Count > 0 && fx == null) fx = queue.Dequeue();
            if (fx == null)
            {
                fx = Instantiate(prefab, transform);
                prefabOf[fx] = prefab;
            }

            fx.transform.SetPositionAndRotation(position, rotation);
            fx.SetActive(true);
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Play(true);
            StartCoroutine(ReturnAfter(fx, lifetime));
        }

        private IEnumerator ReturnAfter(GameObject fx, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (fx == null) yield break;
            fx.SetActive(false);
            pools[prefabOf[fx]].Enqueue(fx);
        }
    }
}
