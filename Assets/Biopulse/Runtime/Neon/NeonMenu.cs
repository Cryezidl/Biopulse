using Biopulse.Emg;
using Biopulse.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Biopulse.Neon
{
    // Main menu operated with the two channels only (spec §18.1): С moves focus, Р confirms.
    public sealed class NeonMenu : MonoBehaviour
    {
        [System.Serializable]
        public struct Item { public string Title, Subtitle, Scene; public int Level; }

        public EmgInputDriver Input;
        public Font Font;
        public Item[] Items;
        public Transform[] Bobbing;
        private int focus;
        private RectTransform root;
        private NeonChannelPanels channels;
        private Image[] rows; private Text[] titles, subs; private RectTransform[] rowRects;
        private Text logo, tagline;
        private float t, confirmAt = -1;
        private string pending;
        private Vector3[] bobBase;
        private CanvasGroup fade;

        private void Awake()
        {
            var go = new GameObject("Menu Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            root = (RectTransform)go.transform;

            logo = NeonUi.Label("Logo", root, Font, 150, Color.white, TextAnchor.MiddleLeft,
                "BIO<color=#FF38B8>PULSE</color>");
            logo.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -70), new Vector2(1200, 170));
            logo.Glow(NeonPalette.Alpha(NeonPalette.Flex, .7f), 6);
            tagline = NeonUi.Label("Tagline", root, Font, 30, NeonPalette.Muted, TextAnchor.MiddleLeft,
                "побег из лаборатории · двухканальное ЭМГ-управление · бионическая рука");
            tagline.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(118, -240), new Vector2(1400, 40));

            int n = Items.Length;
            rows = new Image[n]; titles = new Text[n]; subs = new Text[n]; rowRects = new RectTransform[n];
            for (int i = 0; i < n; i++)
            {
                rows[i] = NeonUi.Panel("Row " + i, root, NeonPalette.Panel);
                rowRects[i] = rows[i].rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -320 - i * 112), new Vector2(760, 96));
                titles[i] = NeonUi.Label("Title", rows[i].transform, Font, 38, Color.white, TextAnchor.MiddleLeft, Items[i].Title);
                titles[i].rectTransform.Place(new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(34, 14), new Vector2(700, 50));
                subs[i] = NeonUi.Label("Sub", rows[i].transform, Font, 22, NeonPalette.Muted, TextAnchor.MiddleLeft);
                subs[i].rectTransform.Place(new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(36, -24), new Vector2(700, 30));
            }
            var help = NeonUi.Label("Help", root, Font, 26, NeonPalette.Text, TextAnchor.MiddleLeft,
                "<color=#26F2FF>С</color> — следующий пункт     <color=#FF38B8>Р</color> — выбрать");
            help.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(116, -320 - n * 112 - 10), new Vector2(900, 40));

            channels = new NeonChannelPanels(root, Font);
            channels.HideKeyboardHelp();
            if (!Application.isMobilePlatform)
                help.text += "     <size=20><color=#8C99B8>(клавиатура: W/E = С, S/Пробел = Р)</color></size>";
            var f = NeonUi.Panel("Fade", root, Color.black, NeonArt.White); f.type = Image.Type.Simple; f.rectTransform.Stretch();
            fade = f.gameObject.AddComponent<CanvasGroup>(); fade.alpha = 1;

            bobBase = new Vector3[Bobbing != null ? Bobbing.Length : 0];
            for (int i = 0; i < bobBase.Length; i++) bobBase[i] = Bobbing[i].localPosition;
        }

        private void OnEnable() { if (Input != null) Input.IntentRecognized += OnIntent; }
        private void OnDisable() { if (Input != null) Input.IntentRecognized -= OnIntent; }
        private void Start() { if (Input != null) { Input.ShowDiagnostics = false; Input.ShowTouchControls = false; } }

        private void OnIntent(EmgIntent intent)
        {
            if (pending != null) return;
            if (intent.Kind == EmgIntentKind.Flex) { focus = (focus + 1) % Items.Length; channels.Flash(true); NeonAudio.Play(Sfx.UiMove); }
            else if (intent.Kind == EmgIntentKind.Extend) { pending = Items[focus].Scene; confirmAt = t; channels.Flash(false); NeonAudio.Play(Sfx.UiConfirm); }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime; t += dt;
            // Recover from load hitches automatically: nothing is in progress on the menu.
            var p = Input != null ? Input.Pipeline : null;
            if (p != null && p.ResumeRequired && !p.WaitingForRest && p.Frame.IsValid) p.Resume();

            for (int i = 0; i < rows.Length; i++)
            {
                bool f = i == focus;
                rows[i].color = f ? NeonPalette.Alpha(NeonPalette.Extend, .85f) : NeonPalette.Alpha(NeonPalette.Panel, .7f);
                titles[i].color = f ? Color.white : NeonPalette.Alpha(NeonPalette.Text, .8f);
                float target = f ? 140 : 110;
                var pos = rowRects[i].anchoredPosition;
                rowRects[i].anchoredPosition = new Vector2(Mathf.Lerp(pos.x, target, 1 - Mathf.Exp(-dt * 14)), pos.y);
                int lvl = Items[i].Level;
                string best = lvl > 0 && NeonProgress.Best(lvl) > 0 ? $"   ·   рекорд {NeonProgress.Best(lvl)}" : "";
                subs[i].text = Items[i].Subtitle + best;
                subs[i].color = f ? new Color(1, 1, 1, .8f) : NeonPalette.Muted;
            }
            logo.rectTransform.localScale = Vector3.one * (1 + Mathf.Sin(t * 2.2f) * .012f);
            for (int i = 0; i < bobBase.Length; i++)
                Bobbing[i].localPosition = bobBase[i] + new Vector3(0, Mathf.Sin(t * (1.6f + i * .7f) + i) * .1f, 0);

            channels.Update(Input, GetScale(), "ВЫБОР", "ОК", dt);
            fade.alpha = pending != null ? Mathf.Clamp01((t - confirmAt) / .35f) : Mathf.Max(0, fade.alpha - dt * 2.5f);
            // Unattended verification (Player.exe -autopilot): skip the menu straight into level 1.
            if (NeonAutopilot.CommandLine && pending == null && t > .5f && Items != null && Items.Length > 0) { pending = Items[0].Scene; confirmAt = t; }
            if (pending != null && t - confirmAt > .4f) { var s = pending; pending = null; SceneManager.LoadScene(s); }
            // Dev: Player.exe -menuShot <file.png> captures the menu and exits (used for documentation screenshots).
            var args = System.Environment.GetCommandLineArgs();
            int shotArg = System.Array.IndexOf(args, "-menuShot");
            if (shotArg >= 0 && shotArg + 1 < args.Length)
            {
                if (t > 2.5f && t - dt <= 2.5f) { focus = 1; ScreenCapture.CaptureScreenshot(args[shotArg + 1]); }
                if (t > 3.5f) Application.Quit();
            }
        }

        private float GetScale() { var c = root.GetComponent<Canvas>(); return c != null && c.scaleFactor > 0 ? c.scaleFactor : 1; }
    }
}
