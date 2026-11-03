using System.Collections.Generic;
using UnityEngine;

namespace Biopulse.Gameplay
{
    public sealed class LockTerminal : MonoBehaviour
    {
        internal static readonly List<LockTerminal> Active = new List<LockTerminal>();
        [SerializeField] private Collider2D activationZone;
        [SerializeField] private Collider2D gate;
        [SerializeField] private SpriteRenderer indicator;
        public LockChallenge Challenge { get; } = new LockChallenge();
        public bool IsOpen => Challenge.State == LockState.Opened;
        public void Configure(Collider2D zone, Collider2D barrier, SpriteRenderer display)
        { activationZone = zone; gate = barrier; indicator = display; }
        public bool Contains(Vector2 point) => activationZone != null && activationZone.enabled && activationZone.OverlapPoint(point);
        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); ResetTerminal(); }
        private void OnDisable() => Active.Remove(this);
        public void ResetTerminal() { Challenge.Reset(); ApplyVisuals(); }
        public void ApplyVisuals()
        {
            if (gate != null)
            {
                gate.enabled = !IsOpen;
                var renderer = gate.GetComponent<SpriteRenderer>(); if (renderer != null) renderer.enabled = !IsOpen;
            }
            if (indicator != null) indicator.color = IsOpen ? Color.green : new Color(.2f, .6f, 1);
        }
    }
}
