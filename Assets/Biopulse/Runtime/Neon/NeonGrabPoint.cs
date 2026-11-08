using Biopulse.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Biopulse.Neon
{
    // Grab points glow brighter when within the arm's maximum reach: the "available target" highlight.
    public sealed class NeonGrabPoint : MonoBehaviour
    {
        public SpriteRenderer Core, Halo;
        public float MaxReach = 4.5f;
        private float phase;
        private void Awake() => phase = Random.value * 6f;
        private void Update()
        {
            var session = NeonLevelSession.Current;
            float near = 0;
            if (session != null && session.Runner != null)
            {
                Vector2 delta = (Vector2)transform.position - session.Runner.Body.position;
                near = delta.x < -.2f ? 0 : Mathf.Clamp01(1.4f - delta.magnitude / MaxReach);
                if (session.Grab != null && (session.Grab.AimTarget != null && session.Grab.AimTarget.transform == transform ||
                    session.Grab.Target != null && session.Grab.Target.transform == transform)) near = 1.4f;
            }
            float pulse = .5f + .5f * Mathf.Sin(Time.time * 6 + phase);
            if (Core != null) Core.color = Color.Lerp(NeonPalette.Alpha(NeonPalette.Flex, .55f), Color.white, near * .6f);
            if (Halo != null)
            {
                Halo.color = NeonPalette.Alpha(NeonPalette.Flex, .15f + near * (.35f + .2f * pulse));
                Halo.transform.localScale = Vector3.one * (1f + near * .5f + pulse * .12f);
            }
        }
    }
}
