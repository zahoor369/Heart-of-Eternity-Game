using UnityEngine;

namespace NED2024.Temple
{
    /// <summary>Spins an object around an axis (rings of the heart, floating glyphs).</summary>
    public class Spin : MonoBehaviour
    {
        [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 40f, 0f);
        [SerializeField] private bool local = true;
        private void Update() { transform.Rotate(degreesPerSecond * Time.deltaTime, local ? Space.Self : Space.World); }
    }
}
