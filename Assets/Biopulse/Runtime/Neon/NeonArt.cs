using UnityEngine;
using UnityEngine.UI;

namespace Biopulse.Neon
{
    // One palette for HUD, FX and world so the two channels are always recognisable by colour AND label.
    public static class NeonPalette
    {
        public static readonly Color Flex = new Color(.15f, .95f, 1f);      // С — сгибатели
        public static readonly Color Extend = new Color(1f, .22f, .72f);    // Р — разгибатели
        public static readonly Color Combo = new Color(.72f, .45f, 1f);     // С+Р
        public static readonly Color Gold = new Color(1f, .78f, .18f);
        public static readonly Color Danger = new Color(1f, .2f, .16f);
        public static readonly Color Success = new Color(.45f, 1f, .4f);
        public static readonly Color Ink = new Color(.03f, .035f, .06f);
        public static readonly Color Panel = new Color(.05f, .06f, .1f, .82f);
        public static readonly Color Text = new Color(.92f, .95f, 1f);
        public static readonly Color Muted = new Color(.55f, .6f, .72f);
        public static Color Alpha(Color c, float a) { c.a = a; return c; }
    }

    // Procedural sprites: no external textures, crisp at any resolution, identical in Editor and on device.
    public static class NeonArt
    {
        private static Sprite rounded, roundedSmall, ring, dot, softDot, white;
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(96, 26);
        public static Sprite RoundedSmall => roundedSmall != null ? roundedSmall : roundedSmall = MakeRounded(48, 12);
        public static Sprite Ring => ring != null ? ring : ring = MakeRing(128, .78f);
        public static Sprite Dot => dot != null ? dot : dot = MakeDot(64, .85f);
        public static Sprite SoftDot => softDot != null ? softDot : softDot = MakeDot(64, 0f);
        public static Sprite White => white != null ? white : white = MakeWhite();

        private static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        }

        private static Sprite MakeRounded(int size, int radius)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + .5f - half) - (half - radius), 0);
                    float dy = Mathf.Max(Mathf.Abs(y + .5f - half) - (half - radius), 0);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    byte a = (byte)(Mathf.Clamp01(.5f - d) * 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            float b = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static Sprite MakeRing(int size, float inner)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01((1 - r) * half) * Mathf.Clamp01((r - inner) * half);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }

        private static Sprite MakeDot(int size, float hardness)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(half, half)) / half;
                    float soft = Mathf.Clamp01(1 - r); soft *= soft;
                    float hard = Mathf.Clamp01((1 - r) * half * .5f);
                    float a = Mathf.Lerp(soft, hard, hardness);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }

        private static Sprite MakeWhite()
        {
            var tex = NewTexture(4);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
        }
    }

    // Tiny uGUI construction kit; the HUD is built in code so scenes stay small and merge-friendly.
    public static class NeonUi
    {
        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = position; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var img = Node(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : NeonArt.Rounded;
            img.type = Image.Type.Sliced; img.color = color; img.raycastTarget = false;
            return img;
        }

        public static Text Label(string name, Transform parent, Font font, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, string text = "")
        {
            var t = Node(name, parent).gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = color; t.alignment = anchor; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; t.supportRichText = true;
            return t;
        }

        public static Shadow Glow(this Graphic g, Color color, float distance = 2)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = color; s.effectDistance = new Vector2(0, -distance);
            return s;
        }
    }
}
