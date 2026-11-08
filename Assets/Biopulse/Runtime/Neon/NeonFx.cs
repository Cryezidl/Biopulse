using UnityEngine;

namespace Biopulse.Neon
{
    // Pooled, allocation-free bursts. One ParticleSystem per look, emitted with EmitParams.
    public sealed class NeonFx : MonoBehaviour
    {
        [SerializeField] private Material unlitMaterial;
        public Material UnlitMaterial { get => unlitMaterial; set => unlitMaterial = value; }
        private ParticleSystem sparks, puffs, streaks;
        private readonly SpriteRenderer[] rings = new SpriteRenderer[8];
        private readonly float[] ringAge = new float[8], ringLife = new float[8], ringSize = new float[8];
        private int nextRing;

        private void Awake()
        {
            sparks = Make("Sparks", NeonArt.Dot, 40, .0f, 1.2f);
            puffs = Make("Puffs", NeonArt.SoftDot, 39, .25f, -.05f);
            streaks = Make("Streaks", NeonArt.Dot, 37, 0f, 0f);
            var streakRenderer = streaks.GetComponent<ParticleSystemRenderer>();
            streakRenderer.renderMode = ParticleSystemRenderMode.Stretch; streakRenderer.velocityScale = .09f; streakRenderer.lengthScale = 1;
            for (int i = 0; i < rings.Length; i++)
            {
                var go = new GameObject("Ring " + i); go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = NeonArt.Ring; sr.sortingOrder = 38;
                if (unlitMaterial != null) sr.sharedMaterial = unlitMaterial;
                sr.enabled = false; rings[i] = sr;
            }
        }

        private ParticleSystem Make(string name, Sprite sprite, int order, float drag, float gravity)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0; main.startLifetime = 1; main.gravityModifier = gravity;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .5f), new GradientAlphaKey(0, 1) });
            col.color = g;
            if (drag > 0) { var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.drag = drag * 10; lim.dampen = drag; }
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = unlitMaterial != null ? new Material(unlitMaterial) : new Material(Shader.Find("Sprites/Default"));
            mat.mainTexture = sprite.texture;
            r.sharedMaterial = mat; r.sortingOrder = order;
            ps.Play();
            return ps;
        }

        public void Sparks(Vector2 at, Color color, int count, float speed = 6, float gravity = 1, float size = .14f, float life = .5f)
        {
            if (sparks == null) return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                p.position = at + dir * .1f;
                p.velocity = dir * speed * Random.Range(.4f, 1.1f) + Vector2.up * speed * .25f;
                p.startColor = Color.Lerp(color, Color.white, Random.value * .5f);
                p.startSize = size * Random.Range(.6f, 1.3f);
                p.startLifetime = life * Random.Range(.6f, 1.2f);
                sparks.Emit(p, 1);
            }
        }

        // Dust sideways from the feet; strength scales count and spread (proportional feedback for jump power).
        public void Dust(Vector2 feet, float strength, Color tint)
        {
            if (puffs == null) return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            int count = 4 + Mathf.RoundToInt(strength * 14);
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                p.position = feet + new Vector2(Random.Range(-.25f, .25f), .05f);
                p.velocity = new Vector2(side * Random.Range(.6f, 2.8f) * (.5f + strength), Random.Range(.2f, 1.4f));
                p.startColor = Color.Lerp(tint, new Color(.7f, .75f, .85f), .55f) * new Color(1, 1, 1, .7f);
                p.startSize = Random.Range(.25f, .55f) * (.7f + strength * .6f);
                p.startLifetime = Random.Range(.35f, .7f);
                puffs.Emit(p, 1);
            }
        }

        // Speed lines trailing a fast-moving runner.
        public void Streaks(Vector2 at, Vector2 velocity)
        {
            if (streaks == null || Random.value > .55f) return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            p.position = at + Random.insideUnitCircle * 1.1f;
            p.velocity = -velocity * Random.Range(.25f, .5f);
            p.startColor = Color.Lerp(NeonPalette.Flex, Color.white, Random.value * .6f) * new Color(1, 1, 1, .7f);
            p.startSize = Random.Range(.05f, .09f);
            p.startLifetime = Random.Range(.12f, .25f);
            streaks.Emit(p, 1);
        }

        public void Ring(Vector2 at, Color color, float size, float life = .4f)
        {
            var sr = rings[nextRing];
            ringAge[nextRing] = 0; ringLife[nextRing] = life; ringSize[nextRing] = size;
            sr.transform.position = at; sr.color = color; sr.enabled = true;
            nextRing = (nextRing + 1) % rings.Length;
        }

        private void Update()
        {
            for (int i = 0; i < rings.Length; i++)
            {
                var sr = rings[i]; if (!sr.enabled) continue;
                ringAge[i] += Time.deltaTime;
                float t = ringAge[i] / ringLife[i];
                if (t >= 1) { sr.enabled = false; continue; }
                float e = 1 - (1 - t) * (1 - t);
                sr.transform.localScale = Vector3.one * Mathf.Lerp(.2f, ringSize[i], e);
                var c = sr.color; c.a = 1 - t; sr.color = c;
            }
        }
    }
}
