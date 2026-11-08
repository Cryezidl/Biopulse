using Biopulse.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Biopulse.Neon
{
    // The two bottom channel panels (С left, Р right). They sit exactly over the emulator's touch zones,
    // so on a phone the panel IS the input: finger height = amplitude, fill = processed signal.
    public sealed class NeonChannelPanels
    {
        private sealed class Panel
        {
            public RectTransform Root, Fill, Threshold;
            public Image Border, FillImage;
            public Text Letter, Action, Amount;
            public Color Color; public float Flash;
        }

        private readonly Panel flex, extend;
        private readonly Text keys;

        public NeonChannelPanels(RectTransform root, Font font)
        {
            flex = Make(root, font, "Flex zone", NeonPalette.Flex, "С", "СГИБАНИЕ", true);
            extend = Make(root, font, "Extend zone", NeonPalette.Extend, "Р", "РАЗГИБАНИЕ", false);
            keys = NeonUi.Label("Keyboard help", root, font, 19, NeonPalette.Alpha(NeonPalette.Muted, .85f));
            keys.rectTransform.Place(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -112), new Vector2(1400, 28));
            keys.gameObject.SetActive(!Application.isMobilePlatform);
        }

        public void Flash(bool isFlex) => (isFlex ? flex : extend).Flash = 1;
        public void HideKeyboardHelp() => keys.gameObject.SetActive(false);

        public void Update(EmgInputDriver input, float canvasScale, string flexAction, string extendAction, float dt)
        {
            if (input == null) return;
            var p = input.Pipeline;
            UpdatePanel(flex, input.TouchZone(true), canvasScale, p != null ? p.Frame.Flexor : 0, input.FlexHeld, flexAction, dt);
            UpdatePanel(extend, input.TouchZone(false), canvasScale, p != null ? p.Frame.Extensor : 0, input.ExtendHeld, extendAction, dt);
            keys.text = "<color=#FF38B8>Р</color>: A слабо · S/Пробел средне · D сильно     " +
                "<color=#26F2FF>С</color>: Q слабо · W средне · E сильно     <color=#B873FF>С+Р</color>: B     F2 автопилот · F1 диагностика";
        }

        private static Panel Make(RectTransform root, Font font, string name, Color color, string letter, string channelName, bool left)
        {
            var p = new Panel { Color = color };
            p.Root = NeonUi.Node(name, root).Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(800, 200));
            p.Border = NeonUi.Panel("Border", p.Root, NeonPalette.Alpha(color, .35f)); p.Border.rectTransform.Stretch(-3);
            NeonUi.Panel("Back", p.Root, new Color(.035f, .04f, .07f, .93f)).rectTransform.Stretch();
            p.FillImage = NeonUi.Panel("Fill", p.Root, NeonPalette.Alpha(color, .28f));
            p.Fill = p.FillImage.rectTransform;
            p.Fill.anchorMin = new Vector2(0, 0); p.Fill.anchorMax = new Vector2(1, 0); p.Fill.pivot = new Vector2(.5f, 0);
            p.Fill.offsetMin = new Vector2(4, 4); p.Fill.offsetMax = new Vector2(-4, 4);
            var th = NeonUi.Panel("Threshold", p.Root, new Color(1, 1, 1, .35f), NeonArt.White); th.type = Image.Type.Simple;
            p.Threshold = th.rectTransform;
            p.Threshold.anchorMin = new Vector2(0, 0); p.Threshold.anchorMax = new Vector2(1, 0); p.Threshold.pivot = new Vector2(.5f, .5f);
            p.Threshold.sizeDelta = new Vector2(-16, 2);
            var anchor = left ? new Vector2(0, .5f) : new Vector2(1, .5f);
            float dir = left ? 1 : -1;
            p.Letter = NeonUi.Label("Letter", p.Root, font, 110, color, TextAnchor.MiddleCenter, letter);
            p.Letter.rectTransform.Place(anchor, new Vector2(.5f, .5f), new Vector2(dir * 80, 0), new Vector2(140, 140));
            p.Letter.Glow(NeonPalette.Alpha(color, .5f), 4);
            var textAnchor = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            var pivot = left ? new Vector2(0, .5f) : new Vector2(1, .5f);
            var nameText = NeonUi.Label("Name", p.Root, font, 22, NeonPalette.Muted, textAnchor, channelName);
            nameText.rectTransform.Place(anchor, pivot, new Vector2(dir * 160, 30), new Vector2(420, 30));
            p.Action = NeonUi.Label("Action", p.Root, font, 46, Color.white, textAnchor);
            p.Action.rectTransform.Place(anchor, pivot, new Vector2(dir * 160, -14), new Vector2(520, 60));
            p.Amount = NeonUi.Label("Amount", p.Root, font, 30, color, left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            p.Amount.rectTransform.Place(left ? new Vector2(1, .5f) : new Vector2(0, .5f), left ? new Vector2(1, .5f) : new Vector2(0, .5f),
                new Vector2(-dir * 28, 0), new Vector2(160, 40));
            return p;
        }

        private static void UpdatePanel(Panel p, Rect zone, float k, float amount, bool held, string action, float dt)
        {
            p.Root.anchoredPosition = zone.position / k;
            p.Root.sizeDelta = zone.size / k;
            float h = p.Root.sizeDelta.y - 8;
            p.Fill.sizeDelta = new Vector2(-8, Mathf.Lerp(p.Fill.sizeDelta.y, h * Mathf.Clamp01(amount), 1 - Mathf.Exp(-dt * 30)));
            p.Threshold.anchoredPosition = new Vector2(0, 4 + h * .25f);
            p.Flash = Mathf.Max(0, p.Flash - dt * 3);
            float glow = Mathf.Max(held ? .8f : .3f, p.Flash);
            p.Border.color = NeonPalette.Alpha(p.Color, glow);
            p.FillImage.color = NeonPalette.Alpha(Color.Lerp(p.Color, Color.white, p.Flash * .5f), .25f + p.Flash * .3f);
            p.Action.text = action;
            p.Amount.text = held || amount > .02f ? $"{amount * 100:0}%" : "";
            p.Letter.rectTransform.localScale = Vector3.one * (1 + p.Flash * .25f + (held ? .06f : 0));
        }
    }
}
