using Biopulse.Gameplay;
using UnityEngine;

namespace Biopulse.Neon
{
    // Dresses the grab controller's arm line: the cable visibly shoots out, twangs when it bites, and stays
    // taut while swinging. Also shows the reach radius while reaching (spec §8) and a lock-on ring.
    [DefaultExecutionOrder(200)]
    public sealed class NeonArm : MonoBehaviour
    {
        public RunnerGrabController Grab;
        public LineRenderer Cable;
        public SpriteRenderer Claw, AimRing;
        public LineRenderer ReachArc;
        private const int ArcPoints = 28, RopePoints = 16;
        private Vector3 trueEnd;
        private float shoot = 1, twang, clawSpin;
        private bool wasShown, wasGrabbed;

        private void Awake()
        {
            if (ReachArc != null) { ReachArc.positionCount = ArcPoints; ReachArc.enabled = false; }
        }

        private void LateUpdate()
        {
            if (Grab == null || Cable == null || Grab.Runner == null) return;
            float dt = Time.unscaledDeltaTime;
            bool grabbed = Grab.Runner.IsGrabbed;
            bool show = Cable.enabled && Cable.positionCount >= 2;
            // The grab controller writes a fresh 2-point line when it updates; otherwise keep the cached tip.
            if (show && Cable.positionCount == 2) trueEnd = Cable.GetPosition(1);
            if (show)
            {
                Vector3 a = Grab.Runner.transform.position;
                if (!wasShown) shoot = 0;
                if (grabbed && !wasGrabbed) twang = 1;
                shoot = Mathf.Min(1, shoot + dt / .07f);
                float e = 1 - Mathf.Pow(1 - shoot, 3);
                Vector3 end = Vector3.Lerp(a, trueEnd, e);
                Vector3 dir = end - a;
                Vector3 perp = new Vector3(-dir.y, dir.x, 0).normalized;
                float wave = Mathf.Sin(Time.unscaledTime * 55) * twang * .35f;
                Cable.positionCount = RopePoints;
                for (int i = 0; i < RopePoints; i++)
                {
                    float t = i / (RopePoints - 1f);
                    Cable.SetPosition(i, a + dir * t + perp * Mathf.Sin(t * Mathf.PI) * wave);
                }
                twang = Mathf.Max(0, twang - dt * 3.5f);
                Cable.widthMultiplier = grabbed ? .11f : .08f;

                if (Claw != null)
                {
                    Claw.enabled = true;
                    Claw.transform.position = end;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    clawSpin = grabbed ? 0 : clawSpin + dt * 900;
                    Claw.transform.rotation = Quaternion.Euler(0, 0, angle + (grabbed ? 0 : Mathf.Sin(clawSpin * Mathf.Deg2Rad) * 12));
                    Claw.transform.localScale = Vector3.one * (grabbed ? .8f + twang * .5f : 1f);
                }
            }
            else if (Claw != null) Claw.enabled = false;
            wasShown = show; wasGrabbed = grabbed;

            float pulse = .5f + .5f * Mathf.Sin(Time.time * 18);
            var cableColor = grabbed ? Color.Lerp(NeonPalette.Flex, Color.white, .3f + twang * .5f) : Color.Lerp(NeonPalette.Flex, Color.white, pulse * .4f);
            Cable.startColor = NeonPalette.Alpha(cableColor, .9f); Cable.endColor = cableColor;

            if (ReachArc != null)
            {
                ReachArc.enabled = Grab.IsReaching;
                if (Grab.IsReaching)
                {
                    Vector2 o = Grab.Runner.Body.position;
                    for (int i = 0; i < ArcPoints; i++)
                    {
                        float ang = Mathf.Lerp(-35, 95, i / (ArcPoints - 1f)) * Mathf.Deg2Rad;
                        ReachArc.SetPosition(i, o + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Grab.CurrentReach);
                    }
                    ReachArc.startColor = ReachArc.endColor = NeonPalette.Alpha(NeonPalette.Flex, .35f);
                }
            }
            if (AimRing != null)
            {
                var aim = grabbed ? Grab.Target : Grab.AimTarget;
                AimRing.enabled = aim != null && (Grab.IsReaching || grabbed);
                if (AimRing.enabled)
                {
                    AimRing.transform.position = aim.transform.position;
                    AimRing.transform.localScale = Vector3.one * (grabbed ? .55f + twang * .6f : .8f + pulse * .25f);
                    AimRing.transform.Rotate(0, 0, dt * 180);
                    AimRing.color = grabbed ? NeonPalette.Success : NeonPalette.Flex;
                }
            }
        }
    }
}
