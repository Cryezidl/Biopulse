using Biopulse.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Biopulse.Neon
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class NeonFinish : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (RunnerLookup.From(other) != null) NeonLevelSession.Current?.Finish();
        }
    }
}
