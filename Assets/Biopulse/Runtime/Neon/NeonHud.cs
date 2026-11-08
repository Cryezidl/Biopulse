using Biopulse.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Biopulse.Neon
{
    // Player-facing HUD, built in code. The two bottom panels are drawn exactly over the emulator's
    // touch zones, so on a phone the "buttons" ARE the live signal meters (finger height = amplitude).
    public sealed class NeonHud : MonoBehaviour
    {
        public Font Font;
        public NeonLevelSession Session;
        private Canvas canvas;
        private RectTransform root;

        private NeonChannelPanels channels;
        private Image hintPanel; private Text hintText; private string lastHint; private float hintPop;
        private Text levelLabel, scoreText, comboText;
        private RectTransform progressTrack, progressRunner, progressChaser;
        private RectTransform lockRoot; private Image lockFill; private Text lockText;
        private Image flash; private float flashAlpha;
        private RectTransform titleRoot; private CanvasGroup titleGroup; private Text titleSmall, titleBig, titleSub; private float titleTime = -1;
        private Text toast; private float toastTime;
        private RectTransform lostRoot; private Text lostText;
        private RectTransform resultsRoot; private Text resultsStats; private Image[] resultButtons; private Text[] resultLabels;
        private readonly Bubble[] bubbles = new Bubble[6];
        private readonly PopText[] pops = new PopText[10];
        private int nextBubble, nextPop;

        private sealed class Bubble { public RectTransform Root; public Image Back; public Text Text; public Transform Target; public Vector2 Offset; public float Age, Life; }
        private sealed class PopText { public Text Text; public Vector2 World; public float Age; }

        private void Awake()
        {
            var go = new GameObject("HUD Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            root = (RectTransform)go.transform;
            Build();
        }

        private void Build()
        {
            channels = new NeonChannelPanels(root, Font);

            // Top-left: level and progress.
            var tl = NeonUi.Panel("Level panel", root, NeonPalette.Alpha(NeonPalette.Panel, .6f));
            tl.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(620, 92));
            levelLabel = NeonUi.Label("Level", tl.transform, Font, 24, NeonPalette.Text, TextAnchor.UpperLeft);
            levelLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -14), new Vector2(580, 30));
            var track = NeonUi.Panel("Progress track", tl.transform, new Color(1, 1, 1, .1f), NeonArt.RoundedSmall);
            progressTrack = track.rectTransform.Place(new Vector2(0, 0), new Vector2(0, .5f), new Vector2(22, 26), new Vector2(576, 10));
            progressChaser = NeonUi.Panel("Chaser dot", track.transform, NeonPalette.Gold, NeonArt.Dot).rectTransform
                .Place(new Vector2(0, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(20, 20));
            progressRunner = NeonUi.Panel("Runner dot", track.transform, NeonPalette.Flex, NeonArt.Dot).rectTransform
                .Place(new Vector2(0, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(26, 26));

            // Top-right: score & combo.
            scoreText = NeonUi.Label("Score", root, Font, 58, NeonPalette.Text, TextAnchor.UpperRight, "0");
            scoreText.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -18), new Vector2(500, 70));
            scoreText.Glow(NeonPalette.Alpha(NeonPalette.Flex, .5f), 3);
            comboText = NeonUi.Label("Combo", root, Font, 28, NeonPalette.Gold, TextAnchor.UpperRight);
            comboText.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, -92), new Vector2(500, 40));

            // Top-centre: contextual hint chip.
            hintPanel = NeonUi.Panel("Hint", root, NeonPalette.Alpha(NeonPalette.Panel, .85f));
            hintPanel.rectTransform.Place(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -30), new Vector2(500, 70));
            hintText = NeonUi.Label("Hint text", hintPanel.transform, Font, 32, NeonPalette.Text);
            hintText.rectTransform.Stretch();

            // Lock ring (world-follow).
            lockRoot = NeonUi.Node("Lock ring", root).Place(Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(170, 170));
            var lockBack = NeonUi.Panel("Back", lockRoot, new Color(1, 1, 1, .12f), NeonArt.Ring); lockBack.type = Image.Type.Simple; lockBack.rectTransform.Stretch();
            lockFill = NeonUi.Panel("Fill", lockRoot, NeonPalette.Combo, NeonArt.Ring); lockFill.rectTransform.Stretch();
            lockFill.type = Image.Type.Filled; lockFill.fillMethod = Image.FillMethod.Radial360; lockFill.fillOrigin = (int)Image.Origin360.Top;
            lockText = NeonUi.Label("Label", lockRoot, Font, 34, NeonPalette.Text, TextAnchor.MiddleCenter, "С+Р");
            lockText.rectTransform.Stretch();
            lockRoot.gameObject.SetActive(false);

            for (int i = 0; i < bubbles.Length; i++)
            {
                var b = new Bubble();
                b.Back = NeonUi.Panel("Bubble " + i, root, NeonPalette.Alpha(NeonPalette.Ink, .9f));
                b.Root = b.Back.rectTransform.Place(Vector2.zero, new Vector2(.5f, 0), Vector2.zero, new Vector2(200, 56));
                b.Text = NeonUi.Label("Text", b.Root, Font, 26, Color.white); b.Text.rectTransform.Stretch();
                var tail = NeonUi.Panel("Tail", b.Root, NeonPalette.Alpha(NeonPalette.Ink, .9f), NeonArt.White);
                tail.rectTransform.Place(new Vector2(.5f, 0), new Vector2(.5f, .5f), Vector2.zero, new Vector2(16, 16));
                tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                b.Root.gameObject.SetActive(false);
                bubbles[i] = b;
            }
            for (int i = 0; i < pops.Length; i++)
            {
                var t = NeonUi.Label("Pop " + i, root, Font, 40, Color.white);
                t.rectTransform.Place(Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(400, 60));
                t.Glow(new Color(0, 0, 0, .6f), 3);
                t.gameObject.SetActive(false);
                pops[i] = new PopText { Text = t };
            }

            toast = NeonUi.Label("Toast", root, Font, 46, NeonPalette.Success);
            toast.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 230), new Vector2(1200, 70));
            toast.Glow(new Color(0, 0, 0, .7f), 4);
            toast.gameObject.SetActive(false);

            flash = NeonUi.Panel("Flash", root, Color.clear, NeonArt.White); flash.type = Image.Type.Simple; flash.rectTransform.Stretch();

            // Title card.
            titleRoot = NeonUi.Node("Title card", root).Stretch();
            titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
            var band = NeonUi.Panel("Band", titleRoot, NeonPalette.Alpha(NeonPalette.Ink, .78f), NeonArt.White); band.type = Image.Type.Simple;
            band.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 40), new Vector2(4000, 300));
            var line1 = NeonUi.Panel("Line", titleRoot, NeonPalette.Flex, NeonArt.White); line1.type = Image.Type.Simple;
            line1.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 190), new Vector2(4000, 4));
            var line2 = NeonUi.Panel("Line", titleRoot, NeonPalette.Extend, NeonArt.White); line2.type = Image.Type.Simple;
            line2.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -110), new Vector2(4000, 4));
            titleSmall = NeonUi.Label("Small", titleRoot, Font, 34, NeonPalette.Flex);
            titleSmall.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 140), new Vector2(1600, 50));
            titleBig = NeonUi.Label("Big", titleRoot, Font, 92, Color.white);
            titleBig.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 50), new Vector2(1800, 120));
            titleBig.Glow(NeonPalette.Alpha(NeonPalette.Extend, .8f), 5);
            titleSub = NeonUi.Label("Sub", titleRoot, Font, 30, NeonPalette.Muted);
            titleSub.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -40), new Vector2(1600, 50));
            titleRoot.gameObject.SetActive(false);

            // Technical pause.
            lostRoot = NeonUi.Node("Input lost", root).Stretch();
            var dim = NeonUi.Panel("Dim", lostRoot, new Color(0, 0, 0, .72f), NeonArt.White); dim.type = Image.Type.Simple; dim.rectTransform.Stretch();
            var lostPanel = NeonUi.Panel("Panel", lostRoot, NeonPalette.Panel);
            lostPanel.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 80), new Vector2(1100, 260));
            lostText = NeonUi.Label("Text", lostPanel.transform, Font, 34, NeonPalette.Text);
            lostText.rectTransform.Stretch(20);
            lostRoot.gameObject.SetActive(false);

            // Results.
            resultsRoot = NeonUi.Node("Results", root).Stretch();
            var rdim = NeonUi.Panel("Dim", resultsRoot, new Color(0, 0, 0, .6f), NeonArt.White); rdim.type = Image.Type.Simple; rdim.rectTransform.Stretch();
            var rp = NeonUi.Panel("Panel", resultsRoot, NeonPalette.Panel);
            rp.rectTransform.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 90), new Vector2(980, 620));
            var rTitle = NeonUi.Label("Title", rp.transform, Font, 64, NeonPalette.Gold, TextAnchor.MiddleCenter, "ПОБЕГ УДАЛСЯ!");
            rTitle.rectTransform.Place(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -30), new Vector2(900, 90));
            rTitle.Glow(NeonPalette.Alpha(NeonPalette.Extend, .7f), 4);
            resultsStats = NeonUi.Label("Stats", rp.transform, Font, 32, NeonPalette.Text, TextAnchor.UpperLeft);
            resultsStats.rectTransform.Place(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -140), new Vector2(760, 300));
            resultsStats.lineSpacing = 1.2f;
            resultButtons = new Image[2]; resultLabels = new Text[2];
            string[] labels = { "ДАЛЕЕ", "ЗАНОВО" };
            for (int i = 0; i < 2; i++)
            {
                resultButtons[i] = NeonUi.Panel("Button " + i, rp.transform, NeonPalette.Panel);
                resultButtons[i].rectTransform.Place(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(i == 0 ? -210 : 210, 70), new Vector2(360, 96));
                resultLabels[i] = NeonUi.Label("Label", resultButtons[i].transform, Font, 40, Color.white, TextAnchor.MiddleCenter, labels[i]);
                resultLabels[i].rectTransform.Stretch();
            }
            var rHelp = NeonUi.Label("Help", rp.transform, Font, 24, NeonPalette.Muted, TextAnchor.MiddleCenter,
                "<color=#26F2FF>С</color> — выбрать   ·   <color=#FF38B8>Р</color> — подтвердить");
            rHelp.rectTransform.Place(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 22), new Vector2(900, 36));
            resultsRoot.gameObject.SetActive(false);
        }

        // ---------- API used by the session ----------
        public void Say(Transform target, string text, Color color, Vector2 offset, float seconds)
        {
            Bubble b = null;
            foreach (var x in bubbles) if (x.Root.gameObject.activeSelf && x.Target == target) { b = x; break; }
            if (b == null) { b = bubbles[nextBubble]; nextBubble = (nextBubble + 1) % bubbles.Length; }
            b.Target = target; b.Offset = offset; b.Age = 0; b.Life = seconds;
            b.Text.text = text; b.Text.color = color == Color.white ? Color.white : Color.Lerp(color, Color.white, .35f);
            b.Back.color = NeonPalette.Alpha(NeonPalette.Ink, .92f);
            b.Root.sizeDelta = new Vector2(b.Text.preferredWidth + 44, 58);
            b.Root.gameObject.SetActive(true);
        }

        public void Pop(Vector2 world, string text, Color color)
        {
            var p = pops[nextPop]; nextPop = (nextPop + 1) % pops.Length;
            p.World = world; p.Age = 0; p.Text.text = text; p.Text.color = color; p.Text.gameObject.SetActive(true);
        }

        public void Toast(string text, Color color) { toast.text = text; toast.color = color; toastTime = 1.6f; toast.gameObject.SetActive(true); }
        public void Flash(Color color) { flash.color = NeonPalette.Alpha(color, .45f); flashAlpha = .45f; }
        public void FlashChannel(bool flex) => channels.Flash(flex);

        public void ShowTitle(string small, string big, string sub)
        {
            titleSmall.text = small; titleBig.text = big; titleSub.text = sub;
            titleTime = 0; titleRoot.gameObject.SetActive(true);
        }

        public void SetInputLost(bool lost, bool resting, string fault)
        {
            bool show = lost && Session != null && Session.State != NeonSessionState.Intro;
            if (lostRoot.gameObject.activeSelf != show) lostRoot.gameObject.SetActive(show);
            if (!show) return;
            lostText.text = resting
                ? "<size=44><color=#FFC72E>ТЕХНИЧЕСКАЯ ПАУЗА</color></size>\n\nСигнал в покое. Коротко активируйте <color=#FF38B8>Р</color>, чтобы продолжить."
                : "<size=44><color=#FFC72E>ТЕХНИЧЕСКАЯ ПАУЗА</color></size>\n\nРасслабьте обе мышцы и дождитесь стабильного сигнала.\n<size=22><color=#8C99B8>" + (fault ?? "") + "</color></size>";
        }

        public void ShowResults(NeonLevelSession s)
        {
            resultsRoot.gameObject.SetActive(true);
            int best = NeonProgress.Best(s.LevelNumber);
            int minutes = (int)(s.ElapsedSeconds / 60), seconds = (int)(s.ElapsedSeconds % 60);
            resultsStats.text =
                $"Время            <color=#FFFFFF>{minutes}:{seconds:00}</color>\n" +
                $"Очки             <color=#FFC72E>{s.Score}</color>   <size=24><color=#8C99B8>рекорд {best}</color></size>\n" +
                $"Повторы          <color=#FFFFFF>{s.Deaths}</color>\n" +
                $"ЭМГ-тайминг      <color=#26F2FF>{s.TimingShare * 100:0}%</color> <size=22><color=#8C99B8>активного времени</color></size>";
            resultLabels[0].text = string.IsNullOrEmpty(s.NextScene) ? "В НАЧАЛО" : "ДАЛЕЕ";
            RefreshResults();
        }

        public void RefreshResults()
        {
            if (Session == null) return;
            for (int i = 0; i < 2; i++)
            {
                bool f = Session.ResultsFocus == i;
                resultButtons[i].color = f ? NeonPalette.Alpha(NeonPalette.Extend, .9f) : NeonPalette.Alpha(Color.white, .08f);
                resultLabels[i].color = f ? Color.white : NeonPalette.Muted;
                resultButtons[i].rectTransform.localScale = Vector3.one * (f ? 1.06f : 1f);
            }
        }

        // ---------- per-frame ----------
        private void LateUpdate()
        {
            if (Session == null) return;
            float dt = Time.unscaledDeltaTime;
            float k = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1;
            var input = Session.Input;
            var pipeline = input != null ? input.Pipeline : null;
            var runner = Session.Runner;
            bool atLock = Session.Locks != null && Session.Locks.IsEngaged;
            bool grabbed = runner != null && runner.IsGrabbed;

            // Channel panels follow the emulator's exact touch rectangles.
            channels.Update(input, k, atLock ? "ВЗЛОМ" : grabbed ? "ДЕРЖИМ" : "ЗАХВАТ", atLock ? "ВЗЛОМ" : grabbed ? "ОТПУСТИТЬ" : "ПРЫЖОК", dt);
            if (input != null && Session.Cam != null) Session.Cam.BottomReserve = input.TouchZone(true).yMax / Mathf.Max(1, Screen.height);

            levelLabel.text = $"<color=#26F2FF>УРОВЕНЬ {Session.LevelNumber}</color>  ·  {Session.Title}";
            float w = progressTrack.rect.width;
            progressRunner.anchoredPosition = new Vector2(Session.Progress * w, 0);
            if (Session.Chaser != null)
                progressChaser.anchoredPosition = new Vector2(Mathf.Clamp01(Mathf.InverseLerp(Session.StartX, Session.FinishX, Session.Chaser.transform.position.x)) * w, 0);

            scoreText.text = Session.Score.ToString("N0").Replace(',', ' ');
            comboText.text = Session.Combo >= 2 ? $"КОМБО x{Session.Combo}" : "";

            UpdateHint(atLock, grabbed, dt);
            UpdateLockRing(k);
            UpdateBubbles(k, dt);
            UpdatePops(k, dt);

            if (toastTime > 0)
            {
                toastTime -= dt;
                toast.color = NeonPalette.Alpha(toast.color, Mathf.Clamp01(toastTime * 2));
                toast.rectTransform.localScale = Vector3.one * (1 + Mathf.Max(0, toastTime - 1.4f) * 2);
                if (toastTime <= 0) toast.gameObject.SetActive(false);
            }
            if (flashAlpha > 0) { flashAlpha = Mathf.Max(0, flashAlpha - dt * 1.4f); flash.color = NeonPalette.Alpha(flash.color, flashAlpha); }
            if (titleTime >= 0)
            {
                titleTime += dt;
                float a = titleTime < .25f ? titleTime / .25f : Mathf.Clamp01((2.6f - titleTime) / .45f);
                titleGroup.alpha = a;
                titleBig.rectTransform.anchoredPosition = new Vector2((1 - Mathf.Clamp01(titleTime * 3)) * -120, 50);
                if (titleTime > 2.6f) { titleTime = -1; titleRoot.gameObject.SetActive(false); }
            }
            if (input != null && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)
                input.ShowDiagnostics = !input.ShowDiagnostics;
        }

        private void UpdateHint(bool atLock, bool grabbed, float dt)
        {
            string hint = "";
            var s = Session;
            if (s.State == NeonSessionState.Playing)
            {
                if (atLock)
                {
                    var st = s.Locks.Current.Challenge.State;
                    hint = st == LockState.WaitingForRest ? "РАССЛАБЬТЕ ОБЕ РУКИ" :
                        st == LockState.Opened ? "ОТКРЫТО! ОТПУСТИТЕ ОБЕ" :
                        "<color=#26F2FF>С</color> + <color=#FF38B8>Р</color>  ДЕРЖИТЕ ВМЕСТЕ";
                }
                else if (grabbed) hint = "ПОКОЙ — ВИСИМ  ·  <color=#FF38B8>Р</color> — ОТПУСТИТЬ";
                else if (s.ActiveZone != null && !string.IsNullOrEmpty(s.ActiveZone.Hint))
                    hint = (s.ActiveZone.HintIsFlex ? "<color=#26F2FF>С</color>  " : "<color=#FF38B8>Р</color>  ") + s.ActiveZone.Hint;
            }
            if (hint != lastHint) { lastHint = hint; hintPop = 1; hintText.text = hint; }
            hintPanel.gameObject.SetActive(hint.Length > 0);
            if (hint.Length == 0) return;
            hintPop = Mathf.Max(0, hintPop - dt * 5);
            hintPanel.rectTransform.sizeDelta = new Vector2(hintText.preferredWidth + 70, 70);
            hintPanel.rectTransform.localScale = Vector3.one * (1 + hintPop * .15f);
        }

        private void UpdateLockRing(float k)
        {
            var locks = Session.Locks;
            bool show = locks != null && locks.Current != null && Session.State == NeonSessionState.Playing;
            lockRoot.gameObject.SetActive(show);
            if (!show) return;
            var cam = Camera.main;
            Vector2 screen = cam.WorldToScreenPoint(locks.Current.transform.position + new Vector3(0, 3.2f, 0));
            lockRoot.anchoredPosition = screen / k;
            float progress = locks.Current.Challenge.Progress;
            lockFill.fillAmount = progress;
            lockFill.color = locks.Current.IsOpen ? NeonPalette.Success : Color.Lerp(NeonPalette.Combo, Color.white, progress * .5f);
            lockText.text = locks.Current.IsOpen ? "ОК" : locks.Current.Challenge.State == LockState.WaitingForRest ? "ПОКОЙ" : "С+Р";
            lockText.fontSize = lockText.text.Length > 3 ? 26 : 34;
            lockRoot.localScale = Vector3.one * (1 + progress * .15f);
        }

        private void UpdateBubbles(float k, float dt)
        {
            var cam = Camera.main;
            foreach (var b in bubbles)
            {
                if (!b.Root.gameObject.activeSelf) continue;
                b.Age += dt;
                if (b.Target == null || b.Age > b.Life) { b.Root.gameObject.SetActive(false); continue; }
                Vector2 screen = cam.WorldToScreenPoint(b.Target.position + (Vector3)b.Offset);
                b.Root.anchoredPosition = screen / k;
                float pop = b.Age < .12f ? b.Age / .12f : 1;
                float fade = Mathf.Clamp01((b.Life - b.Age) / .25f);
                b.Root.localScale = Vector3.one * (pop < 1 ? Mathf.Lerp(.4f, 1.1f, pop) : 1);
                b.Text.color = NeonPalette.Alpha(b.Text.color, fade);
                b.Back.color = NeonPalette.Alpha(b.Back.color, .92f * fade);
            }
        }

        private void UpdatePops(float k, float dt)
        {
            var cam = Camera.main;
            foreach (var p in pops)
            {
                if (!p.Text.gameObject.activeSelf) continue;
                p.Age += dt;
                if (p.Age > 1f) { p.Text.gameObject.SetActive(false); continue; }
                Vector2 screen = cam.WorldToScreenPoint(p.World + new Vector2(0, p.Age * 1.4f));
                p.Text.rectTransform.anchoredPosition = screen / k;
                p.Text.rectTransform.localScale = Vector3.one * (p.Age < .1f ? Mathf.Lerp(.5f, 1.3f, p.Age / .1f) : Mathf.Lerp(1.3f, 1f, (p.Age - .1f) * 4));
                p.Text.color = NeonPalette.Alpha(p.Text.color, Mathf.Clamp01((1 - p.Age) * 3));
            }
        }
    }
}
