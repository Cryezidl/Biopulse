using UnityEngine;

namespace Biopulse.Neon
{
    // Authored decision window (spec §11): time inside counts as timing-dependent EMG play.
    // Leaving through the far edge alive counts as a clear and scores.
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class NeonTimingZone : MonoBehaviour
    {
        public string Id = "zone";
        public NeonChallenge Challenge;
        public string Hint = "";
        public bool HintIsFlex;
        public int Points = 100;
        public float TargetX;   // obstacle centre / pit edge / grab point / terminal — used by the autopilot
        private BoxCollider2D box;
        public float RightEdge => (box != null ? box : box = GetComponent<BoxCollider2D>()).bounds.max.x;
        private void OnTriggerEnter2D(Collider2D other)
        { if (RunnerLookup.From(other) != null) NeonLevelSession.Current?.EnterZone(this); }
        private void OnTriggerExit2D(Collider2D other)
        {
            var runner = RunnerLookup.From(other);
            if (runner != null) NeonLevelSession.Current?.ExitZone(this, runner.Body.position.x >= RightEdge - .6f);
        }
    }
}
