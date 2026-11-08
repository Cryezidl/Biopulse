using Biopulse.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Biopulse.Neon
{
    // Visual state of a C+P terminal; the LockChallenge itself stays in Core.
    public sealed class NeonTerminalVisual : MonoBehaviour
    {
        public LockTerminal Terminal;
        public SpriteRenderer Screen, Gate;
        public Light2D Glow;
        private bool wasOpen;
        public float Progress => Terminal != null ? Terminal.Challenge.Progress : 0;
        private void Update()
        {
            if (Terminal == null) return;
            bool open = Terminal.IsOpen;
            Color c = open ? NeonPalette.Success : Color.Lerp(NeonPalette.Combo, Color.white, Progress * .6f);
            float flicker = open ? 1 : .8f + .2f * Mathf.PerlinNoise(Time.time * 8, transform.position.x);
            if (Screen != null) Screen.color = c * flicker;
            if (Glow != null) { Glow.color = c; Glow.intensity = (open ? 2.5f : 1.2f + Progress * 2) * flicker; }
            if (open && !wasOpen) NeonLevelSession.Current?.TerminalOpened(this);
            wasOpen = open;
        }
    }
}
