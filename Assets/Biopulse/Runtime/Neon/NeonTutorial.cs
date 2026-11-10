using System;
using System.Collections.Generic;
using Biopulse.Emg;
using UnityEngine;

namespace Biopulse.Neon
{
    public enum NeonLessonKind { LowJump, MidJump, HighJump, Gap, Grab, RestInGrab, Release, Lock }

    // Only the world clock stops. The source and recognizer keep using real timestamps, so keyboard,
    // touch and calibrated EMG all dismiss a lesson through the same gameplay command path.
    [DefaultExecutionOrder(-30)]
    public sealed class NeonTutorial : MonoBehaviour
    {
        [Serializable]
        public sealed class Lesson
        {
            public NeonLessonKind Kind;
            public float TriggerX, TargetX;
        }

        public NeonLevelSession Session;
        public Lesson[] Lessons = Array.Empty<Lesson>();
        public int LessonIndex { get; private set; }
        public bool IsPaused { get; private set; }
        public bool RestArmed { get; private set; }
        public bool Complete => LessonIndex >= Lessons.Length;
        public NeonLessonKind CurrentKind => Complete ? NeonLessonKind.Lock : Lessons[LessonIndex].Kind;
        public float RestProgress => Mathf.Clamp01(restSeconds / .75f);
        private float restSeconds;
        private bool recovering;
        private float feedbackUntil;
        private Input.EmgInputDriver subscribedInput;

        public void Configure(NeonTimingZone[] zones)
        {
            Array.Sort(zones, (a, b) => a.TargetX.CompareTo(b.TargetX));
            var result = new List<Lesson>();
            var seen = new HashSet<NeonChallenge>();
            foreach (var zone in zones)
            {
                if (!seen.Add(zone.Challenge)) continue;
                NeonLessonKind kind;
                float lead;
                switch (zone.Challenge)
                {
                    case NeonChallenge.LowJump: kind = NeonLessonKind.LowJump; lead = 1.8f; break;
                    case NeonChallenge.MidJump: kind = NeonLessonKind.MidJump; lead = 2.25f; break;
                    case NeonChallenge.HighJump: kind = NeonLessonKind.HighJump; lead = 2.75f; break;
                    case NeonChallenge.Gap: kind = NeonLessonKind.Gap; lead = .9f; break;
                    case NeonChallenge.Grab: kind = NeonLessonKind.Grab; lead = 2.6f; break;
                    case NeonChallenge.Lock: kind = NeonLessonKind.Lock; lead = 0; break;
                    default: continue;
                }
                result.Add(new Lesson { Kind = kind, TriggerX = zone.TargetX - lead, TargetX = zone.TargetX });
                if (kind != NeonLessonKind.Grab) continue;
                result.Add(new Lesson { Kind = NeonLessonKind.RestInGrab, TriggerX = zone.TargetX - lead, TargetX = zone.TargetX });
                result.Add(new Lesson { Kind = NeonLessonKind.Release, TriggerX = zone.TargetX - lead, TargetX = zone.TargetX });
            }
            Lessons = result.ToArray();
        }

        private void Start() => Subscribe();
        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            if (subscribedInput != null || Session == null || Session.Input == null) return;
            subscribedInput = Session.Input;
            subscribedInput.IntentRecognized += OnIntent;
        }
        private void OnDisable()
        {
            if (subscribedInput != null) subscribedInput.IntentRecognized -= OnIntent;
            subscribedInput = null;
            if (IsPaused)
            {
                IsPaused = false; Time.timeScale = 1;
                if (Session != null && Session.State == NeonSessionState.Playing && Session.Router != null)
                    Session.Router.enabled = true;
            }
            Session?.Hud?.HideLesson();
        }

        private bool Ready
        {
            get
            {
                var p = Session != null && Session.Input != null ? Session.Input.Pipeline : null;
                return p != null && p.Frame.IsValid && !p.WaitingForRest && !p.ResumeRequired;
            }
        }

        private void Update()
        {
            if (Session == null || Session.State != NeonSessionState.Playing || Complete) return;
            var runner = Session.Runner;
            if (!IsPaused)
            {
                if (!Ready || runner.IsSuspended) return;
                var lesson = Lessons[LessonIndex];
                bool trigger;
                switch (lesson.Kind)
                {
                    case NeonLessonKind.RestInGrab: trigger = runner.IsGrabbed; break;
                    case NeonLessonKind.Release:
                        // Preserve the real forward/upward swing, exactly as in the main levels.
                        trigger = runner.IsGrabbed && runner.Body.position.x > lesson.TargetX + .3f &&
                                  runner.Body.linearVelocity.x > 2 && runner.Body.linearVelocity.y > .5f;
                        break;
                    case NeonLessonKind.Lock: trigger = Session.Locks != null && Session.Locks.IsEngaged; break;
                    default: trigger = runner.IsGrounded && runner.Body.position.x >= lesson.TriggerX; break;
                }
                if (trigger) Freeze();
                return;
            }

            var pipeline = Session.Input.Pipeline;
            if (!Ready)
            {
                RestArmed = false; restSeconds = 0; recovering = true;
                if (CurrentKind == NeonLessonKind.Lock) Session.Locks.Current?.Challenge.Pause();
                return;
            }
            // A technical-pause recovery gesture can never also complete a tutorial action.
            if (recovering)
            {
                if (pipeline.State != EmgIntentKind.Rest) return;
                recovering = false;
            }
            if (pipeline.State == EmgIntentKind.Rest) RestArmed = true;
            if (CurrentKind == NeonLessonKind.RestInGrab)
            {
                restSeconds = pipeline.State == EmgIntentKind.Rest ? restSeconds + Time.unscaledDeltaTime : 0;
                Session.Hud?.SetLessonStatus($"Покой: {RestProgress:P0}");
                if (restSeconds >= .75f) Advance();
            }
            else if (CurrentKind == NeonLessonKind.Lock)
            {
                Session.Locks.Step(pipeline.State, pipeline.Frame.TimeSeconds, true);
                var terminal = Session.Locks.Current;
                if (terminal == null) { Advance(); return; }
                Session.Hud?.SetLessonStatus(terminal.IsOpen ? "Открыто! Отпустите оба канала, чтобы продолжить." :
                    terminal.Challenge.State == Gameplay.LockState.WaitingForRest ? "Сначала отпустите оба канала." :
                    $"Удерживайте С + Р: {terminal.Challenge.Progress:P0}");
            }
            else if (Time.unscaledTime >= feedbackUntil)
                Session.Hud?.SetLessonStatus(RestArmed ? "Выполните действие — игра продолжится." : "Сначала отпустите оба канала.");
        }

        private void Freeze()
        {
            Session.ClearHitStop();
            Session.Router.enabled = false;
            IsPaused = true; RestArmed = false; restSeconds = 0; recovering = false; feedbackUntil = 0;
            Time.timeScale = 0;
            Session.Hud?.ShowLesson(CurrentKind, LessonIndex + 1, Lessons.Length);
        }

        private void OnIntent(EmgIntent intent)
        {
            if (!IsPaused || !Ready || !RestArmed || recovering || Complete) return;
            bool correct;
            switch (CurrentKind)
            {
                case NeonLessonKind.LowJump: correct = intent.Kind == EmgIntentKind.Extend && intent.Extensor <= .46f; break;
                case NeonLessonKind.MidJump: correct = intent.Kind == EmgIntentKind.Extend && intent.Extensor >= .48f && intent.Extensor <= .8f; break;
                case NeonLessonKind.HighJump: correct = intent.Kind == EmgIntentKind.Extend && intent.Extensor >= .82f; break;
                case NeonLessonKind.Gap: correct = intent.Kind == EmgIntentKind.Extend && intent.Extensor >= .48f; break;
                case NeonLessonKind.Grab: correct = intent.Kind == EmgIntentKind.Flex && intent.Flexor >= .85f; break;
                case NeonLessonKind.Release: correct = intent.Kind == EmgIntentKind.Extend; break;
                default: return;
            }
            if (!correct)
            {
                Session.Hud?.SetLessonStatus("Попробуйте по подсказке. Отпустите оба канала перед новой попыткой.");
                feedbackUntil = Time.unscaledTime + 1.5f;
                return;
            }
            // Re-enable routing before replaying this very intent; no second press is required.
            Advance();
            Session.Router.TryRoute(intent);
        }

        private void Advance()
        {
            LessonIndex++;
            IsPaused = false; RestArmed = false;
            Session.ClearHitStop();
            Session.Hud?.HideLesson();
            Session.Router.enabled = true;
        }

        public void RetryFrom(float x)
        {
            // Re-teach an obstacle if the attempt failed before the next checkpoint.
            for (int i = 0; i < LessonIndex; i++)
                if (Lessons[i].TriggerX >= x) { LessonIndex = i; break; }
            IsPaused = false; RestArmed = false;
            Session.Hud?.HideLesson();
        }
    }
}
