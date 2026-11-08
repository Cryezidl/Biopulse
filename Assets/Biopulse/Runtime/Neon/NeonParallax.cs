using Biopulse.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Biopulse.Neon
{
    // Horizontal parallax relative to the camera; depth comes from factor (0 = moves with world, 1 = glued to camera).
    public sealed class NeonParallax : MonoBehaviour
    {
        public float Factor = .5f, FactorY = .3f;
        private Transform cam;
        private Vector3 start, camStart;
        private void Start()
        {
            var c = Camera.main; if (c == null) { enabled = false; return; }
            cam = c.transform; start = transform.position; camStart = cam.position;
        }
        private void LateUpdate()
        {
            Vector3 d = cam.position - camStart;
            transform.position = new Vector3(start.x + d.x * Factor, start.y + d.y * FactorY, start.z);
        }
    }
}
