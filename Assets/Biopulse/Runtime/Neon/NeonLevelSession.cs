using System.Collections.Generic;
using Biopulse.Emg;
using Biopulse.Gameplay;
using Biopulse.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Biopulse.Neon
{
    public enum NeonSessionState { Intro, Playing, Dying, Results }

    // Level flow and presentation glue. Gameplay control still goes Source → Pipeline → Router → Motor;
    // this class only starts/stops the router, respawns, scores and narrates.
    [DefaultExecutionOrder(-40)]
    public sealed class NeonLevelSession : MonoBehaviour
    {
        public static NeonLevelSession Current { get; private set; }

        [Header("Wiring")]
        public RunnerMotor2D Runner;
        public RunnerGrabController Grab;
        public RunnerLockController Locks;
        public RunnerCommandRouter Router;
        public EmgInputDriver Input;
        public NeonCamera Cam;
        public NeonHud Hud;
        public NeonFx Fx;
        public NeonChaser Chaser;
        public NeonRunnerVisuals Visuals;

        [Header("Level")]
        public int LevelNumber = 1;
        public string Title = "ПОДЗЕМНАЯ ЛАБОРАТОРИЯ";
        public string Subtitle = "Побег из испытательного блока";
        public string NextScene = "";
        public float StartX, FinishX = 100;
        public float KillY = -5;
        public List<float> CheckpointXs = new List<float>();
        public bool Endless;
        public bool IsTutorial;
        public NeonTutorial Tutorial;
        public bool TutorialPaused => Tutorial != null && Tutorial.IsPaused;
        public bool IsEndless => Endless && LevelNumber == 3;
        public float Distance { get; private set; }
        private float distanceOffset;
        public void AddDistanceOffset(float amount) { distanceOffset += amount; }

        public NeonSessionState State { get; private set; } = NeonSessionState.Intro;
        public int Score { get; private set; }
        public int CoinsCollected { get; private set; }
        public int Combo { get; private set; }
        public int Deaths { get; private set; }
        public float ActiveSeconds { get; private set; }
        public float TimingSeconds { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public NeonTimingZone ActiveZone => zones.Count > 0 ? zones[zones.Count - 1] : null;
        public float Progress => Mathf.InverseLerp(StartX, FinishX, Runner != null ? Runner.Body.position.x : 0);
        public int ResultsFocus { get; private set; }
        public float DyingFor => stateTime;

        private readonly List<NeonTimingZone> zones = new List<NeonTimingZone>();
        public readonly List<string> DeathLog = new List<string>();
        private Vector2 respawn;
        private float stateTime, quipCooldown, restingGrabbed, minAirVy;
        private int lastJumpCount;
        private bool wasGrounded = true, wasGrabbed, resumeArmed, saidHangHint;
        private NeonDeath lastDeath;

        private static readonly string[] BigJump = { "ВЖУХ!", "ВОТ ЭТО ТОЛЧОК!", "ЛЕЧУ-У-У!", "ГРАВИТАЦИЯ? НЕ СЛЫШАЛ." };
        private static readonly string[] SmallJump = { "Аккуратненько.", "Хоп!", "Мини-прыжок!", "Ювелирно." };
        private static readonly string[] GrabLines = { "ПОЙМАЛ!", "ДЕРЖУ!", "ЦЕПКО!", "Есть контакт!" };
        private static readonly string[] HangLines = { "Мышцы в покой — хват держится.", "Р — отпустить. Робот не ждёт!", "Держусь! Но долго не висим." };
        private static readonly string[] ReleaseLines = { "ОТПУСКАЮ!", "ПОЛЕТЕЛИ!", "Йе-е-ей!" };
        private static readonly string[] Deaths_ = { "Ой. Ещё разок?", "Я в порядке. А ты?", "Это была разведка.", "Засчитаем как тренировку." };
        private static readonly string[] Mopped = { "НАС ОТПОЛИРОВАЛИ!", "Меня протёрли… насухо.", "Он слишком чистоплотный!" };
        private static readonly string[] Clears = { "Чисто!", "Красиво!", "Так держать!", "Идеально!" };

        private void Awake()
        {
            Current = this;
            if (Router != null) Router.enabled = false;
        }
        private void OnDestroy() { if (Current == this) Current = null; if (Input != null) Input.IntentRecognized -= OnIntent; }

        private void Start()
        {
            respawn = Runner.Body.position;
            StartX = respawn.x;
            Runner.SetSuspended(true);
            if (Input != null) { Input.IntentRecognized += OnIntent; Input.ShowDiagnostics = false; Input.ShowTouchControls = false; }
            if (Input != null && IsTutorial) Input.NormalizeKeyboardAmplitudes = true;
            Cam?.Snap();
            Chaser?.ResetBehind();
            Hud?.ShowTitle(IsTutorial ? "ОБУЧЕНИЕ" : $"УРОВЕНЬ {LevelNumber}", Title, Subtitle);
            lastJumpCount = Runner.JumpCount;
        }

        private void SetState(NeonSessionState s) { State = s; stateTime = 0; }

        // Hit-stop: a few real-time milliseconds of near-freeze that sells the moment the hook bites.
        private float hitstopUntil = -1;
        public void HitStop(float seconds, float scale = .12f) { if (TutorialPaused) return; hitstopUntil = Time.unscaledTime + seconds; Time.timeScale = scale; }
        public void ClearHitStop() { hitstopUntil = -1; Time.timeScale = 1; }
        private void OnDisable() { Time.timeScale = 1; }

        private void UpdateDynamics()
        {
            if (TutorialPaused) return;
            if (hitstopUntil > 0 && Time.unscaledTime >= hitstopUntil) { hitstopUntil = -1; Time.timeScale = 1; }
            if (Cam == null || Runner == null || Runner.Body == null) return;
            Vector2 v = Runner.Body.linearVelocity;
            float over = Mathf.Clamp01((v.magnitude - Runner.RunSpeed) / 9f);
            bool swinging = Runner.IsGrabbed;
            Cam.ExtraZoom = State == NeonSessionState.Playing ? over * 1.6f + (swinging ? .9f : 0) + (Grab != null && Grab.IsReaching ? .3f : 0) : 0;
            Cam.ExtraLookAhead = State == NeonSessionState.Playing ? Mathf.Clamp((v.x - Runner.RunSpeed) * .45f, -1.5f, 3.5f) + (swinging ? 1.2f : 0) : 0;
            if (State == NeonSessionState.Playing && v.magnitude > Runner.RunSpeed + 3.5f && !Runner.IsGrounded) Fx?.Streaks(Runner.Body.position, v);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            quipCooldown -= dt;
            HandleInputLoss();
            if (TutorialPaused) { NeonAudio.Hum(0, 0); return; }
            UpdateDynamics();
            if (State != NeonSessionState.Playing) NeonAudio.Hum(0, 0);
            switch (State)
            {
                case NeonSessionState.Intro:
                    if (stateTime > 2.4f && InputReady()) { SetState(NeonSessionState.Playing); Runner.SetSuspended(false); Router.enabled = true; }
                    break;
                case NeonSessionState.Playing:
                    if (IsEndless) Distance = Mathf.Max(Distance, distanceOffset + Runner.Body.position.x - StartX);
                    ElapsedSeconds += dt;
                    if (!Runner.IsSuspended)
                    {
                        ActiveSeconds += dt;
                        bool lockActive = Locks != null && Locks.Current != null && Locks.Current.Challenge.State == LockState.Holding;
                        bool inLock = Locks != null && Locks.IsEngaged;
                        if ((zones.Count > 0 && !inLock && !(Runner.IsGrabbed && restingGrabbed > 1.5f)) || lockActive) TimingSeconds += dt;
                    }
                    TrackEvents(dt);
                    var lockNow = Locks != null ? Locks.Current : null;
                    NeonAudio.Hum(lockNow != null && lockNow.Challenge.State == LockState.Holding ? 1 : 0, lockNow != null ? lockNow.Challenge.Progress : 0);
                    if (Runner.Body.position.y < KillY) Kill(NeonDeath.Fall);
                    break;
                case NeonSessionState.Dying:
                    if (stateTime > 1.15f)
                    {
                        if (IsEndless) EndRun();
                        else Respawn();
                    }
                    break;
            }
        }

        private bool InputReady()
        {
            var p = Input != null ? Input.Pipeline : null;
            return p != null && p.Frame.IsValid && !p.WaitingForRest && !p.ResumeRequired;
        }

        // Technical pause: rest both channels, then a single Р continues (spec §7, §18.1 — no extra touch needed).
        private void HandleInputLoss()
        {
            var p = Input != null ? Input.Pipeline : null;
            if (p == null) return;
            bool lost = p.ResumeRequired || !p.Frame.IsValid;
            if (Hud != null) Hud.SetInputLost(lost, p.Frame.IsValid && !p.WaitingForRest, p.Fault);
            if (!p.ResumeRequired) { resumeArmed = false; return; }
            if (p.WaitingForRest || !p.Frame.IsValid) return;
            // Loading hitches before play starts interrupt nothing: resume as soon as the signal rests.
            if (State == NeonSessionState.Intro) { p.Resume(); return; }
            if (p.Frame.Extensor >= .25f) resumeArmed = true;
            else if (resumeArmed && p.Frame.Extensor <= .15f && p.Frame.Flexor <= .15f) { resumeArmed = false; p.Resume(); }
        }

        private void TrackEvents(float dt)
        {
            Vector2 pos = Runner.Body.position;
            Vector2 feet = pos + Vector2.down * .8f;
            if (Runner.JumpCount != lastJumpCount)
            {
                lastJumpCount = Runner.JumpCount;
                float a = Runner.LastJumpAmplitude;
                Visuals?.OnJump(a);
                NeonAudio.Play(Sfx.Jump, .75f + a * .75f);
                Fx?.Dust(feet, a, NeonPalette.Extend);
                Fx?.Ring(feet, NeonPalette.Alpha(NeonPalette.Extend, .9f), 1.2f + a * 2.2f);
                Fx?.Sparks(feet, NeonPalette.Extend, 6 + (int)(a * 16), 4 + a * 5);
                Cam?.Shake(.12f + a * .3f);
                Cam?.Punch(-.15f - a * .25f);
                Hud?.FlashChannel(false);
                if (quipCooldown <= 0 && Random.value < .45f)
                    Quip(a > .72f ? Pick(BigJump) : a < .38f ? Pick(SmallJump) : null, NeonPalette.Extend);
            }
            bool grounded = Runner.IsGrounded && !Runner.IsGrabbed;
            if (!grounded) minAirVy = Mathf.Min(minAirVy, Runner.Body.linearVelocity.y);
            if (grounded && !wasGrounded)
            {
                float impact = Mathf.Clamp01(-minAirVy / 18f);
                if (impact > .15f)
                {
                    Visuals?.OnLand(impact);
                    NeonAudio.Play(Sfx.Land, 1, .4f + impact * .6f);
                    Fx?.Dust(feet, impact * .8f, NeonPalette.Flex);
                    Cam?.Shake(impact * .25f);
                }
                minAirVy = 0;
            }
            wasGrounded = grounded;

            bool grabbed = Runner.IsGrabbed;
            if (grabbed && !wasGrabbed)
            {
                Visuals?.OnGrab();
                NeonAudio.Play(Sfx.Grab);
                HitStop(.07f);
                Cam?.Punch(.35f);
                Vector2 at = Grab.Target != null ? (Vector2)Grab.Target.transform.position : pos;
                Fx?.Sparks(at, NeonPalette.Flex, 16, 5);
                Fx?.Ring(at, NeonPalette.Flex, 1.6f);
                Cam?.Shake(.18f);
                Hud?.FlashChannel(true);
                Quip(Pick(GrabLines), NeonPalette.Flex, true);
                restingGrabbed = 0; saidHangHint = false;
            }
            if (!grabbed && wasGrabbed && State == NeonSessionState.Playing)
            {
                Fx?.Sparks(pos, NeonPalette.Extend, 10, 4);
                NeonAudio.Play(Sfx.Release);
                if (quipCooldown <= 0) Quip(Pick(ReleaseLines), NeonPalette.Extend);
            }
            if (grabbed)
            {
                var p = Input.Pipeline;
                restingGrabbed = p != null && p.State == EmgIntentKind.Rest ? restingGrabbed + dt : 0;
                if (restingGrabbed > 1.1f && !saidHangHint) { saidHangHint = true; Quip(IsTutorial ? "Захват держится сам. Р — отпустить." : Pick(HangLines), NeonPalette.Flex, true); }
            }
            wasGrabbed = grabbed;
        }

        private void OnIntent(EmgIntent intent)
        {
            if (State == NeonSessionState.Results)
            {
                if (intent.Kind == EmgIntentKind.Flex) { ResultsFocus = (ResultsFocus + 1) % 2; Hud?.RefreshResults(); NeonAudio.Play(Sfx.UiMove); }
                else if (intent.Kind == EmgIntentKind.Extend) { NeonAudio.Play(Sfx.UiConfirm); ConfirmResults(); }
                return;
            }
            if (!IsEndless && State == NeonSessionState.Playing && intent.Kind == EmgIntentKind.Coactivate && (Locks == null || !Locks.IsEngaged))
                Quip("Обе сразу? Это для терминалов!", NeonPalette.Combo, true);
            if (State == NeonSessionState.Playing && intent.Kind == EmgIntentKind.Flex && !Runner.IsGrabbed && Grab != null && Grab.AimTarget == null && quipCooldown <= 0)
                Quip("Тут не за что схватиться…", NeonPalette.Flex);
        }

        public void Quip(string text, Color color, bool force = false)
        {
            if (string.IsNullOrEmpty(text) || Hud == null || (!force && quipCooldown > 0)) return;
            Hud.Say(Runner.transform, text, color, new Vector2(.2f, 2.1f), 1.6f);
            NeonAudio.Play(Sfx.Chirp, Random.Range(.9f, 1.2f), .5f);
            quipCooldown = 2.2f;
        }
        private static string Pick(string[] lines) => lines[Random.Range(0, lines.Length)];

        public void EnterZone(NeonTimingZone zone) { if (!zones.Contains(zone)) zones.Add(zone); }
        public void ForgetZone(NeonTimingZone zone) { zones.Remove(zone); }
        public void ExitZone(NeonTimingZone zone, bool cleared)
        {
            zones.Remove(zone);
            if (!cleared || State != NeonSessionState.Playing) return;
            Combo++;
            int points = Mathf.RoundToInt(zone.Points * (1 + (Combo - 1) * .25f));
            Score += points;
            NeonAudio.Play(Sfx.Score, Mathf.Min(2f, 1 + Combo * .04f), .6f);
            Hud?.Pop(Runner.Body.position + new Vector2(0, 1.4f), $"+{points}", Combo > 2 ? NeonPalette.Gold : NeonPalette.Success);
            if (Combo >= 3 && Combo % 3 == 0) Quip($"КОМБО x{Combo}! " + Pick(Clears), NeonPalette.Gold, true);
        }

        public void ReachCheckpoint(NeonCheckpoint checkpoint)
        {
            if (IsEndless) return;
            respawn = checkpoint.SpawnPoint;
            NeonAudio.Play(Sfx.Checkpoint);
            Fx?.Ring(checkpoint.transform.position + Vector3.up, NeonPalette.Success, 3.5f, .6f);
            Fx?.Sparks((Vector2)checkpoint.transform.position + Vector2.up * 1.5f, NeonPalette.Success, 24, 6);
            Hud?.Toast("КОНТРОЛЬНАЯ ТОЧКА", NeonPalette.Success);
        }

        public void CollectCoin(Vector2 position)
        {
            if (State != NeonSessionState.Playing) return;
            CoinsCollected++; Score += 25;
            NeonProgress.AddCoin();
            NeonAudio.Play(Sfx.Score, 1.7f, .7f);
            Fx?.Sparks(position, NeonPalette.Gold, 10, 3);
            Hud?.Pop(position, "+1", NeonPalette.Gold);
        }

        public void TerminalOpened(NeonTerminalVisual terminal)
        {
            Score += 250;
            NeonAudio.Play(Sfx.Unlock);
            Fx?.Sparks(terminal.transform.position + Vector3.up, NeonPalette.Combo, 40, 8);
            Fx?.Ring(terminal.transform.position + Vector3.up, NeonPalette.Combo, 5, .7f);
            Cam?.Shake(.45f); Cam?.Punch(.6f);
            Hud?.Pop(terminal.transform.position + Vector3.up * 2.5f, "+250 ВЗЛОМ!", NeonPalette.Combo);
            Quip("КУЛАК-ВЗЛОМ! Отпусти обе.", NeonPalette.Combo, true);
        }

        public void Kill(NeonDeath kind)
        {
            if (State != NeonSessionState.Playing) return;
            lastDeath = kind;
            DeathLog.Add($"{kind}@{Runner.Body.position.x:F1}");
            SetState(NeonSessionState.Dying);
            Deaths++; Combo = 0; zones.Clear();
            Router.enabled = false;
            Grab?.CancelReach();
            Runner.SetSuspended(true);
            Visuals?.PlayDeath(kind == NeonDeath.Mopped);
            NeonAudio.Play(kind == NeonDeath.Mopped ? Sfx.Mopped : Sfx.Death);
            Vector2 at = Runner.Body.position;
            Color c = kind == NeonDeath.Toxic ? NeonPalette.Success : kind == NeonDeath.Mopped ? NeonPalette.Gold :
                kind == NeonDeath.Shock ? NeonPalette.Flex : kind == NeonDeath.Fall ? NeonPalette.Combo : NeonPalette.Danger;
            Fx?.Sparks(at, c, 40, 9, 1.5f, .18f, .7f);
            Fx?.Ring(at, c, 3.5f, .5f);
            Cam?.Shake(.7f); Cam?.Punch(.5f);
            Hud?.Flash(c);
            Hud?.Say(Runner.transform, kind == NeonDeath.Mopped ? Pick(Mopped) : Pick(Deaths_), Color.white, new Vector2(.2f, 2.1f), 1.3f);
            quipCooldown = 1.5f;
        }

        private void Respawn()
        {
            Runner.ResetTo(respawn);
            Runner.SetSuspended(false);
            lastJumpCount = Runner.JumpCount;
            wasGrounded = true; wasGrabbed = false; minAirVy = 0;
            Visuals?.Respawn();
            Chaser?.ResetBehind();
            Cam?.Snap();
            Fx?.Ring(respawn, NeonPalette.Flex, 2.5f, .45f);
            SetState(NeonSessionState.Playing);
            Router.enabled = true;
            Tutorial?.RetryFrom(respawn.x);
        }

        public void Finish()
        {
            if (IsEndless || State != NeonSessionState.Playing) return;
            SetState(NeonSessionState.Results);
            Router.enabled = false;
            Runner.SetSuspended(true);
            Fx?.Sparks(Runner.Body.position, NeonPalette.Gold, 60, 10);
            NeonAudio.Play(Sfx.Unlock, 1.25f);
            Cam?.Punch(.8f);
            ResultsFocus = 0;
            if (!IsTutorial) NeonProgress.Record(LevelNumber, Score, Deaths, ElapsedSeconds, TimingShare);
            Hud?.ShowResults(this);
        }

        private void EndRun()
        {
            SetState(NeonSessionState.Results);
            ResultsFocus = 0;
            NeonProgress.RecordEndless(Score, Distance, ElapsedSeconds);
            Hud?.ShowResults(this);
        }

        // Spec §11 operational definition: D = T_timing / T_active.
        public float TimingShare => ActiveSeconds > .01f ? TimingSeconds / ActiveSeconds : 0;

        private void ConfirmResults()
        {
            if (IsEndless)
            {
                SceneManager.LoadScene(ResultsFocus == 0 ? SceneManager.GetActiveScene().name : "Neon_Menu");
                return;
            }
            string target = ResultsFocus == 0 && !string.IsNullOrEmpty(NextScene) ? NextScene : SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(target);
        }
    }

    // Local progress only; no network, no personal data (spec §9).
    public static class NeonProgress
    {
        public const string CoinsKey = "biopulse.coins.total";
        public static int TotalCoins => PlayerPrefs.GetInt(CoinsKey, 0);
        public static void AddCoin()
        {
            PlayerPrefs.SetInt(CoinsKey, TotalCoins + 1);
            PlayerPrefs.Save();
        }
        private const string EndlessKey = "biopulse.rooftops.endless";
        public static float BestEndlessDistance => PlayerPrefs.GetFloat(EndlessKey + ".distance", 0);
        public static int BestEndlessScore => PlayerPrefs.GetInt(EndlessKey + ".score", 0);
        public static void RecordEndless(int score, float distance, float seconds)
        {
            PlayerPrefs.SetFloat(EndlessKey + ".distance", Mathf.Max(distance, BestEndlessDistance));
            PlayerPrefs.SetInt(EndlessKey + ".score", Mathf.Max(score, BestEndlessScore));
            PlayerPrefs.SetFloat(EndlessKey + ".lastSeconds", seconds);
            PlayerPrefs.Save();
        }
        public static void Record(int level, int score, int deaths, float seconds, float timingShare)
        {
            string k = "biopulse.level" + level;
            if (score > PlayerPrefs.GetInt(k + ".best", 0)) PlayerPrefs.SetInt(k + ".best", score);
            PlayerPrefs.SetInt(k + ".done", 1);
            PlayerPrefs.SetFloat(k + ".lastTiming", timingShare);
            PlayerPrefs.SetFloat(k + ".lastSeconds", seconds);
            PlayerPrefs.SetInt(k + ".lastDeaths", deaths);
            PlayerPrefs.Save();
        }
        public static int Best(int level) => PlayerPrefs.GetInt("biopulse.level" + level + ".best", 0);
    }
}
