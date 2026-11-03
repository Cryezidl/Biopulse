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
        public bool IsReaching { get; private set; }
        public float CurrentReach { get; private set; }
        public string Feedback { get; private set; } = "F / С: reach a yellow target";

        public bool BeginReach(float amplitude)
        {
            if (runner == null || runner.IsSuspended || runner.IsGrabbed || IsReaching) return false;
            IsReaching = true; remaining = reachSeconds; resetVersion = runner.ResetVersion;
            UpdateReach(amplitude); Feedback = "Reaching"; return true;
        }
        public void CancelReach() { IsReaching = false; if (arm != null && (runner == null || !runner.IsGrabbed)) arm.enabled = false; }
        public void Release()
        {
            CancelReach(); Target = null; runner?.ReleaseGrab();
            Feedback = "Released by Р (no jump impulse)";
        }
        private void OnDisable() { CancelReach(); Target = null; runner?.ReleaseGrab(); }
        public void UpdateReach(float amplitude) => CurrentReach = Mathf.Lerp(minReach, maxReach, Mathf.Clamp01(amplitude));

        public void Step(float amplitude, bool inputReady, float deltaSeconds)
        {
            if (runner == null) return;
            if (runner.IsGrabbed)
            {
                if (Target == null || !Target.isActiveAndEnabled) { Release(); Feedback = "Target lost"; }
                else DrawArm(anchor);
                return;
            }
            Target = null;
            if (IsReaching && resetVersion != runner.ResetVersion) CancelReach();
            if (!inputReady || runner.IsSuspended) { CancelReach(); return; }
            if (!IsReaching) { if (arm != null) arm.enabled = false; return; }
            UpdateReach(amplitude);
            Vector2 origin = runner.Body.position;
            GrabTarget best = null; float bestAngle = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
            foreach (var target in GrabTarget.Active)
            {
                if (target == null || !target.isActiveAndEnabled || (queryLayers.value & (1 << target.gameObject.layer)) == 0) continue;
                Vector2 delta = (Vector2)target.transform.position - origin;
                float distance = delta.magnitude;
                float angle = Vector2.Angle(new Vector2(1, 1), delta);
                if (delta.x < -.05f || delta.y < 0 || distance > CurrentReach || angle > 60 || !Visible(origin, target.transform.position)) continue;
                bool better = angle < bestAngle - .001f || (Mathf.Abs(angle - bestAngle) <= .001f &&
                    (distance < bestDistance - .001f || (Mathf.Abs(distance - bestDistance) <= .001f &&
                    (best == null || string.CompareOrdinal(target.StableId, best.StableId) < 0))));
                if (better) { best = target; bestAngle = angle; bestDistance = distance; }
            }
            if (best != null && runner.BeginGrab())
            {
                Target = best; anchor = best.transform.position; IsReaching = false;
                Feedback = "Grabbed: relax; Р releases"; DrawArm(anchor); return;
            }
            DrawArm(origin + new Vector2(.7071f, .7071f) * CurrentReach);
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
