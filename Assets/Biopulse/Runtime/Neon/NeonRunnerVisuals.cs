using Biopulse.Gameplay;
using UnityEngine;

namespace Biopulse.Neon
{
    // Procedural animation of a single sprite: run bob, lean, squash & stretch, swing tilt and a comedic death tumble.
    // Purely presentational: reads the motor, never writes to physics.
    public sealed class NeonRunnerVisuals : MonoBehaviour
    {
        public RunnerMotor2D Motor;
        public RunnerGrabController Grab;
        public SpriteRenderer Body;
        public Transform Pivot;           // at the feet
        public TrailRenderer Trail;
        private Vector2 squash, squashVelocity;
        private float tilt, tiltVelocity, runPhase;
        private bool dying;
        private Vector2 deathVelocity;
        private float deathSpin, deathTime, blink;
        private Vector3 baseScale = Vector3.one;

        private void Awake() { if (Pivot != null) baseScale = Pivot.localScale; }

        public void Kick(Vector2 amount) => squashVelocity += amount;

        // Kicks are scale-velocities; the spring (omega ~16) turns ~5 into a ~0.3 peak deformation.
        public void OnJump(float amplitude) => Kick(new Vector2(-1.2f - amplitude * 1.8f, 2f + amplitude * 3.2f));
        public void OnLand(float impact) => Kick(new Vector2(1.5f + impact * 2.5f, -2f - impact * 3.2f));
        public void OnGrab() => Kick(new Vector2(-1f, 2.5f));

        public void PlayDeath(bool launchBackwards)
        {
            dying = true; deathTime = 0;
            deathVelocity = new Vector2(launchBackwards ? -3.5f : 2.5f, 9f);
            deathSpin = launchBackwards ? 540 : -720;
            if (Trail != null) Trail.emitting = false;
        }

        public void Respawn()
        {
            dying = false; blink = 1f; squash = Vector2.zero; squashVelocity = new Vector2(-1.5f, 3.5f);
            if (Pivot != null) { Pivot.localPosition = Vector3.zero; Pivot.localRotation = Quaternion.identity; }
            if (Body != null) Body.color = Color.white;
            if (Trail != null) Trail.Clear();
        }

        private void LateUpdate()
        {
            if (Motor == null || Pivot == null) return;
            float dt = Time.deltaTime;
            if (dying)
            {
                deathTime += dt;
                deathVelocity.y -= 30 * dt;
                Pivot.localPosition += (Vector3)(deathVelocity * dt);
                Pivot.localRotation = Quaternion.Euler(0, 0, deathSpin * deathTime);
                if (Body != null) Body.color = new Color(1, 1, 1, Mathf.Clamp01(1.4f - deathTime));
                return;
            }

            Vector2 v = Motor.Body != null ? Motor.Body.linearVelocity : Vector2.zero;
            bool grounded = Motor.IsGrounded && !Motor.IsGrabbed;
            float targetTilt;
            float bob = 0;
            if (Motor.IsGrabbed && Grab != null && Grab.Target != null)
            {
                Vector2 toAnchor = (Vector2)Grab.Target.transform.position - Motor.Body.position;
                targetTilt = -Vector2.SignedAngle(Vector2.up, toAnchor) * .6f;
            }
            else if (grounded && Motor.State == RunnerMotionState.Running)
            {
                runPhase += dt * Mathf.Max(4, Motor.RunSpeed) * 2.6f;
                bob = Mathf.Abs(Mathf.Sin(runPhase)) * .07f;
                targetTilt = -7 + Mathf.Sin(runPhase * 2) * 2;
            }
            else if (!grounded)
                targetTilt = Mathf.Clamp(v.y * 1.4f, -14, 14) - 4;
            else targetTilt = 0;

            tilt = Mathf.SmoothDamp(tilt, targetTilt, ref tiltVelocity, .08f);
            // Critically-damped-ish spring for squash.
            squashVelocity += (-squash * 260f - squashVelocity * 16f) * dt;
            squash += squashVelocity * dt;
            float sx = Mathf.Clamp(1 + squash.x, .6f, 1.5f), sy = Mathf.Clamp(1 + squash.y, .6f, 1.5f);
            if (!grounded && !Motor.IsGrabbed) { sx *= 1 - Mathf.Clamp(Mathf.Abs(v.y) * .006f, 0, .06f); sy *= 1 + Mathf.Clamp(Mathf.Abs(v.y) * .008f, 0, .08f); }

            Pivot.localPosition = new Vector3(0, bob, 0);
            Pivot.localRotation = Quaternion.Euler(0, 0, tilt);
            Pivot.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, 1);
            if (Trail != null) Trail.emitting = !grounded || Motor.IsGrabbed;

            if (Body != null)
            {
                blink = Mathf.Max(0, blink - dt);
                bool hidden = blink > 0 && Mathf.Repeat(blink * 12, 1) > .5f;
                Body.color = hidden ? new Color(1, 1, 1, .25f) : Color.white;
            }
        }
    }
}
