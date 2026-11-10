using Biopulse.Gameplay;
using UnityEngine;

namespace Biopulse.Neon
{
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class NeonCoin : MonoBehaviour
    {
        public bool Collected { get; private set; }
        private void Awake()
        {
            var trigger = GetComponent<CircleCollider2D>();
            trigger.isTrigger = true; trigger.radius = .32f;
        }
        private void OnEnable() { Collected = false; }
        private void OnTriggerEnter2D(Collider2D other) => TryCollect(RunnerLookup.From(other));
        private void OnTriggerStay2D(Collider2D other) => TryCollect(RunnerLookup.From(other));

        public bool TryCollect(RunnerMotor2D runner)
        {
            var session = NeonLevelSession.Current;
            if (Collected || !gameObject.activeInHierarchy || runner == null || session == null ||
                session.Runner != runner || runner.IsSuspended || session.State != NeonSessionState.Playing) return false;
            Collected = true;
            session.CollectCoin(transform.position);
            gameObject.SetActive(false);
            return true;
        }
    }
}
