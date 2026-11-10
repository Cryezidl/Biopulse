using UnityEngine;

namespace Biopulse.Neon
{
    // Small pooled particle field without ParticleSystem setup: bubbles in tanks, steam puffs, sparks, floating dust.
    // Purely decorative; positions are local to this transform (so it follows parallax layers).
    public sealed class NeonMotes : MonoBehaviour
    {
        public enum Kind { Bubbles, Steam, Sparks, Dust, Rain }

        public Kind Mode = Kind.Bubbles;
        public Sprite Sprite;
        public Color Color = Color.white;
        public Vector2 Area = new Vector2(1, 3);   // spawn box (bottom-centre origin); Dust uses it centred
        public int Count = 12;
        public float Speed = .6f, Size = .08f, SizeJitter = .5f;
        public int SortingOrder;
        public Material Material;

        private Transform[] items;
        private SpriteRenderer[] renderers;
        private float[] life, span, seed;

        private void Start()
        {
            items = new Transform[Count]; renderers = new SpriteRenderer[Count];
            life = new float[Count]; span = new float[Count]; seed = new float[Count];
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("mote"); go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = Sprite; sr.sortingOrder = SortingOrder;
                if (Material != null) sr.sharedMaterial = Material;
                items[i] = go.transform; renderers[i] = sr;
                Respawn(i, true);
            }
        }

        private void Respawn(int i, bool scatter)
        {
            seed[i] = Random.value * 100;
            float h = Area.y;
            switch (Mode)
            {
                case Kind.Bubbles: span[i] = h / Mathf.Max(.05f, Speed * Random.Range(.6f, 1.4f)); break;
                case Kind.Steam: span[i] = Random.Range(1.6f, 2.6f); break;
                case Kind.Sparks: span[i] = Random.Range(.5f, 1.1f); break;
                case Kind.Rain: span[i] = Area.y / Mathf.Max(.1f, Speed) * Random.Range(.8f, 1.2f); break;
                default: span[i] = Random.Range(6f, 12f); break;
            }
            life[i] = scatter ? Random.value * span[i] : 0;
            float x = Random.Range(-Area.x / 2, Area.x / 2);
            float y = Mode == Kind.Dust ? Random.Range(-Area.y / 2, Area.y / 2) : Mode == Kind.Rain ? Area.y / 2 : 0;
            items[i].localPosition = new Vector3(x, y, 0);
            float s = Size * (1 + Random.Range(-SizeJitter, SizeJitter));
            items[i].localScale = Mode == Kind.Rain ? new Vector3(s * .05f, s, 1) : Vector3.one * s;
            if (Mode == Kind.Rain) items[i].localRotation = Quaternion.Euler(0, 0, 12);
            if (Mode == Kind.Sparks) velocity(i) = new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(.2f, 1.5f));
        }

        private Vector2[] vel;
        private ref Vector2 velocity(int i) { if (vel == null || vel.Length != Count) vel = new Vector2[Count]; return ref vel[i]; }

        private void Update()
        {
            if (items == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < Count; i++)
            {
                life[i] += dt;
                float k = life[i] / span[i];
                if (k >= 1) { Respawn(i, false); continue; }
                var p = items[i].localPosition;
                float a;
                switch (Mode)
                {
                    case Kind.Bubbles:
                        p.y += Speed * dt * (1 + .3f * Mathf.Sin(seed[i]));
                        p.x += Mathf.Sin(Time.time * 3 + seed[i]) * .15f * dt;
                        if (p.y > Area.y) { Respawn(i, false); continue; }
                        a = Mathf.Clamp01(k * 6) * Mathf.Clamp01((Area.y - p.y) * 4);
                        break;
                    case Kind.Steam:
                        p.y += Speed * dt * (1 - k * .5f);
                        p.x += Mathf.Sin(Time.time * 1.3f + seed[i]) * .2f * dt + .15f * dt;
                        items[i].localScale = Vector3.one * Size * (1 + k * 3);
                        a = Mathf.Sin(k * Mathf.PI) * .55f;
                        break;
                    case Kind.Sparks:
                        ref var v = ref velocity(i);
                        v.y -= 9 * dt;
                        p += (Vector3)(v * dt);
                        a = 1 - k;
                        break;
                    case Kind.Rain:
                        p += new Vector3(-.21f, -1, 0) * Speed * dt;
                        if (p.y < -Area.y / 2) { Respawn(i, false); continue; }
                        a = .5f + .5f * Mathf.Sin(seed[i]);
                        break;
                    default:
                        p.x += Mathf.Sin(Time.time * .3f + seed[i]) * .12f * dt + Speed * .2f * dt;
                        p.y += Mathf.Cos(Time.time * .23f + seed[i] * 1.7f) * .1f * dt + Speed * .1f * dt;
                        if (p.x > Area.x / 2) p.x -= Area.x;
                        if (p.y > Area.y / 2) p.y -= Area.y;
                        a = Mathf.Sin(k * Mathf.PI) * (.5f + .5f * Mathf.Sin(Time.time * 2 + seed[i]));
                        break;
                }
                items[i].localPosition = p;
                var c = Color; c.a *= a; renderers[i].color = c;
            }
        }
    }
}
