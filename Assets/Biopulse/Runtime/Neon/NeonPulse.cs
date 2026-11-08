using Biopulse.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Biopulse.Neon
{
    // Cheap ambient life: flickering tubes, pulsing indicators, breathing lights.
    public sealed class NeonPulse : MonoBehaviour
    {
        public float Speed = 2, Depth = .35f;
        public bool Flicker;
        private SpriteRenderer sprite;
        private Light2D glow;
        private Color baseColor;
        private float baseIntensity, seed;
        private void Awake()
        {
            sprite = GetComponent<SpriteRenderer>(); glow = GetComponent<Light2D>();
            if (sprite != null) baseColor = sprite.color;
            if (glow != null) baseIntensity = glow.intensity;
            seed = Random.value * 100;
        }
        private void Update()
        {
            float k = 1 - Depth * (.5f + .5f * Mathf.Sin(Time.time * Speed + seed));
            if (Flicker && Mathf.PerlinNoise(Time.time * 9, seed) > .82f) k *= .25f;
            if (sprite != null) { var c = baseColor * k; c.a = baseColor.a; sprite.color = c; }
            if (glow != null) glow.intensity = baseIntensity * k;
        }
    }
}
