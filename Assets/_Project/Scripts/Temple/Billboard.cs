using UnityEngine;

namespace NED2024.Temple
{
    /// <summary>Makes a flat holographic glyph always face the camera around the vertical axis.</summary>
    public class Billboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 d = transform.position - cam.transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
    }
}
