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
        public float RepeatWidth;
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
            if (RepeatWidth > 0)
            {
                float span = RepeatWidth * transform.childCount;
                float centre = cam.position.x - transform.position.x;
                foreach (Transform copy in transform)
                {
                    Vector3 p = copy.localPosition;
                    p.x = centre + Mathf.Repeat(p.x - centre + span * .5f, span) - span * .5f;
                    copy.localPosition = p;
                }
            }
        }

        public void ShiftOrigin(float distance)
        {
            start.x -= distance; camStart.x -= distance;
            transform.position -= Vector3.right * distance;
        }
    }
}
