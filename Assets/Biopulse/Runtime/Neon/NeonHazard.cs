using UnityEngine;

namespace Biopulse.Neon
{
    // Touching a hazard is a game event, never a diagnosis: the session turns it into a quick, funny retry.
    [RequireComponent(typeof(Collider2D))]
    public sealed class NeonHazard : MonoBehaviour
    {
        public NeonDeath Kind = NeonDeath.Laser;
        private void OnTriggerEnter2D(Collider2D other) { if (RunnerLookup.From(other) != null) NeonLevelSession.Current?.Kill(Kind); }
        private void OnTriggerStay2D(Collider2D other) => OnTriggerEnter2D(other);
    }
}
