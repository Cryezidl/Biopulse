using Biopulse.Gameplay;
using UnityEngine;

namespace Biopulse.Neon
{
    // Flip-book animation of the hero sprites rendered from the Blender rig (ArtSource/Blender/hero.py).
    // Purely presentational: reads the motor and grab state, never touches physics. Runs after NeonRunnerVisuals,
    // which still adds squash/stretch, tilt and the death tumble on top.
    [DefaultExecutionOrder(50)]
    public sealed class NeonHeroAnimator : MonoBehaviour
    {
        public RunnerMotor2D Motor;
        public RunnerGrabController Grab;
        public NeonRunnerVisuals Visuals;
        public SpriteRenderer Body;
        public Sprite[] Run, Idle;
        public Sprite Rise, Apex, Fall, Land, Hang, SwingForward, SwingBack, Reach, Death;
        [Tooltip("World units travelled per full run cycle (stride).")]
        public float StrideLength = 2.4f;
        public float IdleFps = 6, LandSeconds = .12f, ApexBand = 2.2f, SwingBand = 1.6f;

        private float runPhase, idleTime, landTimer;
        private bool wasGrounded = true;

        private void LateUpdate()
        {
            if (Motor == null || Body == null) return;
            float dt = Time.deltaTime;
            Body.sprite = Pick(dt) ?? Body.sprite;
        }

        private Sprite Pick(float dt)
        {
            if (Visuals != null && Visuals.IsDying) return Death;
            Vector2 v = Motor.Body != null ? Motor.Body.linearVelocity : Vector2.zero;
            bool grounded = Motor.IsGrounded && !Motor.IsGrabbed;
            if (grounded && !wasGrounded) landTimer = LandSeconds;
            wasGrounded = grounded;
            landTimer = Mathf.Max(0, landTimer - dt);

            if (Motor.IsGrabbed)
            {
                if (v.x > SwingBand) return SwingForward;
                if (v.x < -SwingBand) return SwingBack;
                return Hang;
            }
            if (Grab != null && Grab.IsReaching && !grounded) return Reach;
            if (!grounded)
            {
                if (v.y > ApexBand) return Rise;
                if (v.y < -ApexBand) return Fall;
                return Apex;
            }
            if (landTimer > 0 && Land != null) return Land;
            float speed = Mathf.Abs(v.x);
            if (Motor.IsAtLock || speed < .4f)
            {
                idleTime += dt;
                return Frame(Idle, idleTime * IdleFps);
            }
            idleTime = 0;
            runPhase += speed * dt / Mathf.Max(.1f, StrideLength);
            return Frame(Run, runPhase * (Run != null ? Run.Length : 1));
        }

        private static Sprite Frame(Sprite[] frames, float index)
        {
            if (frames == null || frames.Length == 0) return null;
            int i = (int)Mathf.Repeat(Mathf.Floor(index), frames.Length);
            return frames[i];
        }
    }
}
