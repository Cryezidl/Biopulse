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
        public static readonly Color Ink = new Color(.02f, .04f, .05f);
        public static readonly Color Panel = new Color(.025f, .055f, .065f, .84f);
        public static readonly Color Text = new Color(.9f, .96f, .94f);
        public static readonly Color Muted = new Color(.52f, .64f, .62f);
        public static readonly Color Line = new Color(.7f, .86f, .82f, .75f);   // UI frame linework (deliberately not the С cyan)
        public static Color Alpha(Color c, float a) { c.a = a; return c; }
    }

    // Procedural sprites: no external textures, crisp at any resolution, identical in Editor and on device.
    public static class NeonArt
    {
        private static Sprite rounded, roundedSmall, ring, dot, softDot, white, frameLine, frameLineSmall, ticks;
        // Sci-fi HUD language (refs: angular frames, segmented gauges): chamfered panels instead of rounded ones.
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeChamfer(96, 24, 0, false);
        public static Sprite RoundedSmall => roundedSmall != null ? roundedSmall : roundedSmall = MakeChamfer(48, 10, 0, false);
        public static Sprite FrameLine => frameLine != null ? frameLine : frameLine = MakeChamfer(96, 24, 3, true);
        public static Sprite FrameLineSmall => frameLineSmall != null ? frameLineSmall : frameLineSmall = MakeChamfer(48, 10, 2, true);
        public static Sprite Ring => ring != null ? ring : ring = MakeSegmentedRing(256, .8f, 24, .1f);
        public static Sprite Ticks => ticks != null ? ticks : ticks = MakeSegmentedRing(256, .9f, 60, .45f);
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

        // Chamfered panel (top-left + bottom-right corners cut). outline > 0: line only, with an accent notch
        // thickened along the top-left chamfer (the "bracket" of the reference HUD kits).
        private static Sprite MakeChamfer(int size, int cut, int outline, bool lineOnly)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + .5f, fy = y + .5f;
                    // signed distance to the chamfered box (positive inside)
                    float d = Mathf.Min(Mathf.Min(fx, size - fx), Mathf.Min(fy, size - fy));
                    float dTL = (fx + (size - fy) - cut) / 1.41421f;           // top-left cut
                    float dBR = ((size - fx) + fy - cut) / 1.41421f;           // bottom-right cut
                    d = Mathf.Min(d, Mathf.Min(dTL, dBR));
                    float inside = Mathf.Clamp01(d + .5f);
                    float a;
                    if (!lineOnly) a = inside;
                    else
                    {
                        float w = outline;
                        if (dTL < w * 2.4f && fy > size * .55f) w *= 2.2f;      // accent along the top-left chamfer
                        if (fy > size - w - .5f && fx > cut && fx < cut + size * .18f) w = outline * 2.2f;   // short tab on the top edge
                        a = inside * Mathf.Clamp01(w + .5f - d);
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            float b = cut + 4;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        // Ring split into segments (gauge look); gap = fraction of each segment left empty.
        private static Sprite MakeSegmentedRing(int size, float inner, int segments, float gap)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - half, dy = y + .5f - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / half;
                    float a = Mathf.Clamp01((1 - r) * half) * Mathf.Clamp01((r - inner) * half);
                    float ang = (Mathf.Atan2(dy, dx) / (2 * Mathf.PI) + 1) % 1f * segments;
                    float f = ang - Mathf.Floor(ang);
                    float edge = Mathf.Min(f - gap * .5f, 1 - gap * .5f - f) * (2 * Mathf.PI * r * half / segments);
                    a *= Mathf.Clamp01(edge + .5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
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
            if (sprite == null || sprite == NeonArt.RoundedSmall)
            {   // frame linework on top of the fill (HUD-kit look)
                var line = Node("Frame", img.transform).gameObject.AddComponent<Image>();
                line.sprite = sprite == NeonArt.RoundedSmall ? NeonArt.FrameLineSmall : NeonArt.FrameLine;
                line.type = Image.Type.Sliced; line.raycastTarget = false;
                line.color = NeonPalette.Alpha(NeonPalette.Line, NeonPalette.Line.a * Mathf.Clamp01(color.a * 1.6f));
                line.rectTransform.Stretch();
            }
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
