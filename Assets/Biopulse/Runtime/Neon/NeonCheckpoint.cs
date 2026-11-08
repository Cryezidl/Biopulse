using Biopulse.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Biopulse.Neon
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class NeonCheckpoint : MonoBehaviour
    {
        public SpriteRenderer Beacon;
        public Light2D Glow;
        public bool Reached { get; private set; }
        public Vector2 SpawnPoint => (Vector2)transform.position + new Vector2(0, .85f);
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Reached || RunnerLookup.From(other) == null || NeonLevelSession.Current == null) return;
            Reached = true;
            NeonLevelSession.Current.ReachCheckpoint(this);
            if (Beacon != null) Beacon.color = NeonPalette.Success;
            if (Glow != null) { Glow.color = NeonPalette.Success; Glow.intensity = 2.2f; }
        }
    }
}
