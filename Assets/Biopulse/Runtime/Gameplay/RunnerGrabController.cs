using UnityEngine;

namespace Biopulse.Gameplay
{
    public sealed class RunnerGrabController : MonoBehaviour
    {
        [SerializeField] private RunnerMotor2D runner;
        [SerializeField] private LineRenderer arm;
        [SerializeField] private float minReach = 1;
        [SerializeField] private float maxReach = 4.5f;
        [SerializeField] private float reachSeconds = .15f;
        [SerializeField] private LayerMask queryLayers = ~0;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private double remaining;
        private Vector2 anchor;
        private int resetVersion;
        public RunnerMotor2D Runner { get => runner; set => runner = value; }
        public LineRenderer Arm { get => arm; set => arm = value; }
        public LayerMask QueryLayers { get => queryLayers; set => queryLayers = value; }
        public GrabTarget Target { get; private set; }
        public GrabTarget AimTarget { get; private set; }
        public bool IsReaching { get; private set; }
        public float CurrentReach { get; private set; }
        public string Feedback { get; private set; } = "F / С: reach a yellow target";

        public bool BeginReach(float amplitude)
        {
            if (runner == null || runner.IsSuspended || runner.IsGrabbed || IsReaching) return false;
            IsReaching = true; remaining = reachSeconds; resetVersion = runner.ResetVersion;
            UpdateReach(amplitude); Feedback = "Reaching"; return true;
        }
        public void CancelReach() { IsReaching = false; AimTarget = null; if (arm != null && (runner == null || !runner.IsGrabbed)) arm.enabled = false; }
        public void Release()
        {
            runner?.ReleaseGrab(); CancelReach(); Target = null;
            Feedback = "Released by Р (no jump impulse)";
        }
        private void OnDisable() { runner?.ReleaseGrab(); CancelReach(); Target = null; }
        private void LateUpdate()
        {
            if (runner != null && runner.IsGrabbed && Target != null) DrawArm(anchor);
            else if (!IsReaching && arm != null) arm.enabled = false;
        }
        public void UpdateReach(float amplitude) => CurrentReach = Mathf.Lerp(minReach, maxReach, Mathf.Clamp01(amplitude));

        public void Step(float amplitude, bool inputReady, float deltaSeconds)
        {
            if (runner == null) return;
            if (runner.IsGrabbed)
            {
                if (Target == null || !Target.isActiveAndEnabled) { Release(); Feedback = "Target lost"; }
                else
                {
                    if (inputReady && !runner.IsSuspended)
                    {
                        anchor = Target.transform.position;
                        runner.UpdateGrabAnchor(anchor);
                        if (!Visible(runner.Body.position, anchor)) { Release(); Feedback = "Arm blocked"; return; }
                    }
                    DrawArm(anchor);
                }
                return;
            }
            Target = null;
            if (IsReaching && resetVersion != runner.ResetVersion) CancelReach();
            if (!inputReady || runner.IsSuspended) { CancelReach(); return; }
            if (!IsReaching) { if (arm != null) arm.enabled = false; return; }
            UpdateReach(amplitude);
            Vector2 origin = runner.Body.position;
            GrabTarget best = null; float bestDistance = float.PositiveInfinity;
            foreach (var target in GrabTarget.Active)
            {
                if (target == null || !target.isActiveAndEnabled || (queryLayers.value & (1 << target.gameObject.layer)) == 0) continue;
                Vector2 delta = (Vector2)target.transform.position - origin;
                float distance = delta.magnitude;
                // Aim at the nearest visible target in front, including level/below the airborne player.
                // Search the full possible reach so a weak attempt still points toward its target.
                if (delta.x < 0 || distance > maxReach || !Visible(origin, target.transform.position)) continue;
                bool better = distance < bestDistance - .001f || (Mathf.Abs(distance - bestDistance) <= .001f &&
                    (best == null || string.CompareOrdinal(target.StableId, best.StableId) < 0));
                if (better) { best = target; bestDistance = distance; }
            }
            AimTarget = best;
            if (best != null && bestDistance <= CurrentReach && runner.BeginGrab(best.transform.position))
            {
                Target = best; anchor = best.transform.position; IsReaching = false;
                Feedback = "Pulling / swinging: relax; Р releases with momentum"; DrawArm(anchor); return;
            }
            Vector2 direction = best == null ? Vector2.right : ((Vector2)best.transform.position - origin).normalized;
            DrawArm(origin + direction * CurrentReach);
            remaining -= deltaSeconds;
            if (remaining <= 0) { CancelReach(); Feedback = "No reachable visible target; release С and retry"; }
        }
        private bool Visible(Vector2 origin, Vector2 end)
        {
            var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(queryLayers);
            int count = Physics2D.Linecast(origin, end, filter, hits);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++) if (hits[i].collider.attachedRigidbody != runner.Body) return false;
            return true;
        }
        private void DrawArm(Vector2 end)
        {
            if (arm == null) return;
            arm.enabled = true; arm.positionCount = 2;
            arm.SetPosition(0, runner.transform.position); arm.SetPosition(1, end);
        }
    }
}
