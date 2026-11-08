using UnityEngine;

namespace Biopulse.Neon
{
    // Horizontal parallax relative to the camera; depth comes from factor (0 = moves with world, 1 = glued to camera,
    // negative = foreground that slides past faster than the world).
    public sealed class NeonParallax : MonoBehaviour
    {
        public float Factor = .5f, FactorY = .3f;
        // Foreground bands: keep the sprite's bottom edge this far below the top of the screen (follows zoom too).
        public bool AnchorTop;
        public float TopInset = 2.5f;
        private Transform cam;
        private Camera camera_;
        private Vector3 start, camStart;

        private void Start()
        {
            camera_ = Camera.main; if (camera_ == null) { enabled = false; return; }
            cam = camera_.transform; start = transform.position; camStart = cam.position;
        }

        private void LateUpdate()
        {
            Vector3 d = cam.position - camStart;
            float y = AnchorTop
                ? cam.position.y + camera_.orthographicSize - TopInset
                : start.y + d.y * FactorY;
            transform.position = new Vector3(start.x + d.x * Factor, y, start.z);
        }
    }
}
