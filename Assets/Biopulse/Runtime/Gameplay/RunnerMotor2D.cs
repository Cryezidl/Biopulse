using UnityEngine;

namespace Biopulse.Gameplay
{
    public enum RunnerMotionState { Airborne, Running, Blocked, Suspended, Grabbed, AtLock }

    // Physics only. No keyboard, touch or EMG dependencies.
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RunnerMotor2D : MonoBehaviour
    {
        [SerializeField, Min(0)] private float runSpeed = 4;
        [SerializeField, Range(.1f, 1)] private float minimumGroundNormalY = .65f;
        [SerializeField] private LayerMask solidLayers = ~0;
        [SerializeField, Min(0)] private float minJumpImpulse = 6;
        [SerializeField, Min(0)] private float maxJumpImpulse = 14;
        [SerializeField, Min(0)] private float coyoteSeconds = .08f;
        [SerializeField, Min(0)] private float jumpBufferSeconds = .08f;
        private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private Vector2 suspendedVelocity;
        private GameCommand pendingJump;
        private bool hasPendingJump, jumpConsumed, queuedInAir, trackingJump;
        private long lastCommandId;
        private double queuedAt, lastGroundedAt = double.NegativeInfinity;
        private float jumpStartY;

        public float RunSpeed { get => runSpeed; set => runSpeed = Mathf.Max(0, value); }
        public Rigidbody2D Body => body;
        public bool IsGrounded { get; private set; }
        public bool IsBlocked { get; private set; }
        public bool IsSuspended { get; private set; }
        public bool IsGrabbed { get; private set; }
        public bool IsAtLock { get; private set; }
        public int ResetVersion { get; private set; }
        public int JumpCount { get; private set; }
        public float LastJumpAmplitude { get; private set; }
        public float LastJumpImpulse { get; private set; }
        public float LastJumpHeight { get; private set; }
        public bool LastJumpUsedCoyote { get; private set; }
        public bool LastJumpUsedBuffer { get; private set; }
        public RunnerMotionState State => IsSuspended ? RunnerMotionState.Suspended :
            (IsAtLock ? RunnerMotionState.AtLock : (IsGrabbed ? RunnerMotionState.Grabbed : (IsBlocked ? RunnerMotionState.Blocked : (IsGrounded ? RunnerMotionState.Running : RunnerMotionState.Airborne))));

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider == null || bodyCollider.isTrigger)
            {
                Debug.LogError("RunnerMotor2D needs a solid Collider2D.", this);
                enabled = false; return;
            }
            body.bodyType = RigidbodyType2D.Dynamic;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void FixedUpdate()
        {
            if (IsSuspended || IsGrabbed || IsAtLock) return;
            ReadContacts();
            // A just-launched body may retain floor contacts until the next simulation step.
            if (body.linearVelocity.y > .05f) IsGrounded = false;
            double now = Time.fixedTimeAsDouble;
            if (IsGrounded) { lastGroundedAt = now; jumpConsumed = false; }
            if (trackingJump)
            {
                LastJumpHeight = Mathf.Max(LastJumpHeight, body.position.y - jumpStartY);
                if (IsGrounded) trackingJump = false;
            }
            // Vertical speed belongs to gravity/physics; horizontal motion is autorun.
            body.linearVelocity = new Vector2(IsBlocked ? 0 : runSpeed, body.linearVelocity.y);
            if (!hasPendingJump) return;
            double lifetime = queuedInAir ? jumpBufferSeconds : Mathf.Max(jumpBufferSeconds, Time.fixedDeltaTime);
            if (now - queuedAt > lifetime + .000001) { ClearPendingJump(); return; }
            bool canJump = !jumpConsumed && (IsGrounded || now - lastGroundedAt <= coyoteSeconds + .000001);
            if (!canJump) return;
            LastJumpUsedCoyote = !IsGrounded;
            LastJumpUsedBuffer = queuedInAir && IsGrounded;
            LastJumpAmplitude = pendingJump.Intensity;
            LastJumpImpulse = Mathf.Lerp(minJumpImpulse, Mathf.Max(minJumpImpulse, maxJumpImpulse), LastJumpAmplitude);
            jumpStartY = body.position.y; LastJumpHeight = 0; JumpCount++; trackingJump = true;
            // Cancel downward speed so the same amplitude has the same launch impulse when buffered/coyote-assisted.
            body.linearVelocity = new Vector2(body.linearVelocity.x, 0);
            body.AddForce(Vector2.up * LastJumpImpulse, ForceMode2D.Impulse);
            jumpConsumed = true; lastGroundedAt = double.NegativeInfinity; IsGrounded = false;
            ClearPendingJump();
        }

        public bool QueueCommand(GameCommand command)
        {
            if (!isActiveAndEnabled || IsSuspended || body == null || !body.simulated ||
                command.Kind != GameCommandKind.Jump || command.Id <= lastCommandId ||
                float.IsNaN(command.Intensity) || float.IsInfinity(command.Intensity) ||
                command.Intensity < 0 || command.Intensity > 1) return false;
            lastCommandId = command.Id;
            // One pending action, never a backlog of jumps after landing.
            if (hasPendingJump) return false;
            pendingJump = command; hasPendingJump = true;
            queuedAt = Time.timeAsDouble; queuedInAir = !IsGrounded;
            return true;
        }

        public void ClearPendingJump() => hasPendingJump = false;
        public bool BeginGrab()
        {
            if (IsSuspended || IsGrabbed || IsAtLock || body == null || !enabled) return false;
            IsGrabbed = true; ClearPendingJump();
            body.linearVelocity = suspendedVelocity = Vector2.zero;
            body.simulated = false; IsGrounded = IsBlocked = false;
            lastGroundedAt = double.NegativeInfinity;
            return true;
        }
        public void ReleaseGrab()
        {
            if (!IsGrabbed) return;
            IsGrabbed = false; body.simulated = !IsSuspended;
            suspendedVelocity = new Vector2(runSpeed, 0);
            body.linearVelocity = suspendedVelocity;
            ClearPendingJump(); lastGroundedAt = double.NegativeInfinity;
        }
        public void SetAtLock(bool active)
        {
            if (body == null || IsAtLock == active) return;
            IsAtLock = active; ClearPendingJump(); lastGroundedAt = double.NegativeInfinity;
            suspendedVelocity = active ? Vector2.zero : new Vector2(runSpeed, 0);
            body.linearVelocity = suspendedVelocity; body.simulated = !active && !IsGrabbed && !IsSuspended;
        }
        private void OnDisable() => ClearPendingJump();

        private void ReadContacts()
        {
            IsGrounded = IsBlocked = false;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(solidLayers);
            int count = bodyCollider.GetContacts(filter, contacts);
            for (int i = 0; i < count; i++)
            {
                Vector2 normal = contacts[i].normal;
                if (normal.y >= minimumGroundNormalY) IsGrounded = true;
                if (normal.x <= -.65f) IsBlocked = true;
            }
        }

        public void SetSuspended(bool suspended)
        {
            if (body == null || suspended == IsSuspended) return;
            IsSuspended = suspended;
            if (suspended)
            {
                ClearPendingJump(); lastGroundedAt = double.NegativeInfinity;
                suspendedVelocity = body.linearVelocity;
                body.simulated = false;
            }
            else
            {
                body.simulated = !IsGrabbed && !IsAtLock;
                body.linearVelocity = suspendedVelocity;
            }
        }

        public void ResetTo(Vector2 position)
        {
            if (body == null) return;
            ResetVersion++;
            IsGrabbed = IsAtLock = false; body.simulated = !IsSuspended;
            body.position = position;
            body.rotation = 0;
            body.linearVelocity = suspendedVelocity = Vector2.zero;
            body.angularVelocity = 0;
            IsGrounded = IsBlocked = false;
            ClearPendingJump(); jumpConsumed = false; lastGroundedAt = double.NegativeInfinity;
            LastJumpHeight = 0; JumpCount = 0; trackingJump = false;
            LastJumpAmplitude = LastJumpImpulse = 0; LastJumpUsedBuffer = LastJumpUsedCoyote = false;
            // Move the presentation immediately too; Rigidbody interpolation resumes next step.
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }
}
