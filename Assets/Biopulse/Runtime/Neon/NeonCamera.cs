using UnityEngine;

namespace Biopulse.Neon
{
    // Side-scroller camera: look-ahead, lazy vertical follow, trauma shake and zoom punch.
    // Runs before parallax layers so they capture the snapped camera position in Start.
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Camera))]
    public sealed class NeonCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector2 Offset = new Vector2(5.2f, 1.55f);
        public float FollowX = .08f, FollowY = .35f;
        public float BaseSize = 6.4f;
        // Dynamic framing, fed every frame by the session: extra zoom-out and extra look-ahead (world units).
        public float ExtraZoom, ExtraLookAhead;
        private float zoom, zoomVelocity, look, lookVelocity;
        public float MaxShake = .5f;
        [Range(0, .4f)] public float BottomReserve = .2f;   // screen fraction covered by HUD panels (set by the HUD)
        private Camera cam;
        private Vector3 focus, velocity;
        private float trauma, punch, punchVelocity;

        private void Awake() { cam = GetComponent<Camera>(); cam.orthographicSize = BaseSize; }
        private void Start() { if (Target != null) Snap(); }

        public void Snap()
        {
            if (Target == null) return;
            float offsetY = -.8f + BaseSize * (1 - 2 * Mathf.Clamp(BottomReserve + .06f, .1f, .45f));
            focus = (Vector2)Target.position + new Vector2(Offset.x, offsetY); velocity = Vector3.zero;
            transform.position = new Vector3(focus.x, focus.y, -10);
        }

        public void Shake(float amount) => trauma = Mathf.Clamp01(Mathf.Max(trauma, amount));
        public void Punch(float amount) => punch = Mathf.Clamp(punch + amount, -1.2f, 1.2f);

        private void LateUpdate()
        {
            if (Target == null) return;
            // Keep the runner's feet just above the HUD channel panels whatever the aspect/resolution.
            float feetFraction = Mathf.Clamp(BottomReserve + .06f, .1f, .45f);
            float offsetY = -.8f + BaseSize * (1 - 2 * feetFraction);
            zoom = Mathf.SmoothDamp(zoom, ExtraZoom, ref zoomVelocity, .35f);
            look = Mathf.SmoothDamp(look, ExtraLookAhead, ref lookVelocity, .3f);
            float size = BaseSize + zoom;
            offsetY = -.8f + size * (1 - 2 * feetFraction);
            Vector2 goal = (Vector2)Target.position + new Vector2(Offset.x + look, offsetY);
            focus.x = Mathf.SmoothDamp(focus.x, goal.x, ref velocity.x, FollowX);
            focus.y = Mathf.SmoothDamp(focus.y, goal.y, ref velocity.y, FollowY);
            float s = trauma * trauma;
            float t = Time.time * 25;
            Vector2 shake = new Vector2(Mathf.PerlinNoise(t, 1.7f) - .5f, Mathf.PerlinNoise(3.1f, t) - .5f) * 2 * MaxShake * s;
            transform.position = new Vector3(focus.x + shake.x, focus.y + shake.y, -10);
            transform.rotation = Quaternion.Euler(0, 0, (Mathf.PerlinNoise(t, 9.3f) - .5f) * 3 * s);
            trauma = Mathf.Max(0, trauma - Time.deltaTime * 1.6f);
            punch = Mathf.SmoothDamp(punch, 0, ref punchVelocity, .18f);
            cam.orthographicSize = BaseSize + zoom - punch;
        }
    }
}
