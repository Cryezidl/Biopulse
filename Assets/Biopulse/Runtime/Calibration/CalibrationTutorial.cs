using Biopulse.Emg;
using Biopulse.Gameplay;
using Biopulse.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Biopulse.Calibration
{
    public enum SetupStage { Welcome, Recording, VerifyRest, VerifyFlex, RestAfterFlex, VerifyExtend, RestAfterExtend, VerifyBoth, FinalRest, Review, Tutorial, Complete, Error }

    [DefaultExecutionOrder(100)]
    public sealed class CalibrationTutorial : MonoBehaviour
    {
        [SerializeField] private EmgInputDriver input;
        [SerializeField] private RunnerMotor2D runner;
        [SerializeField] private RunnerCommandRouter router;
        [SerializeField] private RunnerGrabController grab;
        [SerializeField] private RunnerLockController lockController;
        [SerializeField] private GrabTarget nearTarget, farTarget;
        [SerializeField] private LockTerminal terminal;
        private CalibrationSession recording;
        private CalibrationProfile candidate, saved;
        private TutorialSession tutorial;
        private int choice;
        private bool pendingConfirm, recoveryArmed, recoveryRequested;
        private double restSince = double.NaN;
        private double cancelSince = double.NaN;
        private string error, sourceId;
        private string sourceSignature;
        private string Signature => input.Source.Info.Id + "|" + input.Source.Info.Representation + "|" + input.Source.Info.Units + "|" + input.SettingsSignature;
        public SetupStage Stage { get; private set; }
        public TutorialSession Tutorial => tutorial;
        public string TutorialScene = "Neon_Tutorial";
        public EmgInputDriver Input => input;
        public CalibrationSession Recording => recording;
        public double RestDuration => double.IsNaN(restSince) ? 0 : input.Pipeline.Frame.TimeSeconds - restSince;

        public void Configure(EmgInputDriver source, RunnerMotor2D motor, RunnerCommandRouter commandRouter,
            RunnerGrabController grabController, RunnerLockController locks, GrabTarget near, GrabTarget far, LockTerminal lockTerminal)
        { input = source; runner = motor; router = commandRouter; grab = grabController; lockController = locks;
          nearTarget = near; farTarget = far; terminal = lockTerminal; }
        private void Start()
        {
            sourceId = input.Source.Info.Id;
            sourceSignature = Signature;
            ProfileStore.TryLoad(input.Source.Info, input.SettingsSignature, out saved);
            input.ShowDiagnostics = false; input.ShowTouchControls = true;
            input.SampleReceived += OnSample; input.IntentRecognized += OnIntent;
            SetGameplay(false); nearTarget.gameObject.SetActive(false); farTarget.gameObject.SetActive(false); terminal.gameObject.SetActive(false);
        }
        private void OnDestroy()
        {
            if (input != null) { input.SampleReceived -= OnSample; input.IntentRecognized -= OnIntent; }
        }
        private bool Ready => input.Pipeline.Frame.IsValid && !input.Pipeline.WaitingForRest && !input.Pipeline.ResumeRequired;
        private void SetGameplay(bool value)
        {
            router.enabled = value;
            if (!value) { grab.CancelReach(); runner.ClearPendingJump(); runner.SetSuspended(true); }
        }
        private void OnSample(EmgSample sample)
        {
            if (Stage != SetupStage.Recording || recording == null) return;
            recording.Push(sample);
            if (recording.Stage == CalibrationStage.Failed) { Fail(recording.Error); return; }
            if (recording.Stage != CalibrationStage.Complete) return;
            candidate = CalibrationProfile.Create(input.Source.Info, input.SettingsSignature, recording.Result);
            candidate.version = saved == null ? 1 : saved.version + 1;
            input.ApplyCalibration(candidate.ToCalibration()); Stage = SetupStage.VerifyRest;
        }
        private void OnIntent(EmgIntent intent)
        {
            if (Stage == SetupStage.VerifyFlex && intent.Kind == EmgIntentKind.Flex) Stage = SetupStage.RestAfterFlex;
            else if (Stage == SetupStage.VerifyExtend && intent.Kind == EmgIntentKind.Extend) Stage = SetupStage.RestAfterExtend;
            else if (Stage == SetupStage.VerifyBoth && intent.Kind == EmgIntentKind.Coactivate) Stage = SetupStage.FinalRest;
            else if (Interactive && !pendingConfirm)
            {
                if (intent.Kind == EmgIntentKind.Flex) choice = (choice + 1) % OptionCount;
                if (intent.Kind == EmgIntentKind.Extend) pendingConfirm = true;
            }
        }
        private bool Interactive => Stage == SetupStage.Welcome || Stage == SetupStage.Review || Stage == SetupStage.Complete || Stage == SetupStage.Error;
        private int OptionCount => Stage == SetupStage.Welcome ? (saved == null ? 2 : 3) : Stage == SetupStage.Review ? 2 : 1;
        private void Update()
        {
            if (input == null || input.Source == null) return;
            if (Signature != sourceSignature)
            {
                sourceId = input.Source.Info.Id; saved = candidate = null;
                sourceSignature = Signature;
                ProfileStore.TryLoad(input.Source.Info, input.SettingsSignature, out saved);
                SetGameplay(false); Fail("Источник изменён. Настройте или загрузите профиль этого источника.");
            }
            var p = input.Pipeline;
            if (p.ResumeRequired)
            {
                restSince = double.NaN;
                if (Stage == SetupStage.Recording) Fail("Запись прервана. После восстановления повторите настройку.");
                tutorial?.Pause();
                // Explicit two-channel recovery: rest → Р → rest. No gameplay is routed here.
                if (p.Frame.IsValid)
                {
                    bool rest = p.Frame.Flexor <= .15f && p.Frame.Extensor <= .15f;
                    if (rest && !recoveryRequested) recoveryArmed = true;
                    if (recoveryArmed && p.Frame.Extensor >= .25f && p.Frame.Flexor <= .15f) recoveryRequested = true;
                    if (rest && recoveryRequested && p.Resume()) recoveryArmed = recoveryRequested = false;
                }
                return;
            }
            if (!Ready) { restSince = double.NaN; tutorial?.Pause(); return; }
            if (Stage == SetupStage.Recording && p.State == EmgIntentKind.Coactivate)
            {
                if (double.IsNaN(cancelSince)) cancelSince = p.Frame.TimeSeconds;
                if (p.Frame.TimeSeconds - cancelSince >= 1)
                { recording = null; Stage = SetupStage.Welcome; choice = 0; pendingConfirm = false; }
            }
            else cancelSince = double.NaN;
            if (pendingConfirm && p.State == EmgIntentKind.Rest) { pendingConfirm = false; Confirm(); }
            bool restNow = p.State == EmgIntentKind.Rest;
            if (!restNow) restSince = double.NaN;
            else if (double.IsNaN(restSince)) restSince = p.Frame.TimeSeconds;
            if (restNow)
            {
                if (Stage == SetupStage.VerifyRest && p.Frame.TimeSeconds - restSince >= .5) Stage = SetupStage.VerifyFlex;
                else if (Stage == SetupStage.RestAfterFlex) Stage = SetupStage.VerifyExtend;
                else if (Stage == SetupStage.RestAfterExtend) Stage = SetupStage.VerifyBoth;
                else if (Stage == SetupStage.FinalRest) { candidate.verified = true; Stage = SetupStage.Review; choice = 0; }
            }
            if (Stage != SetupStage.Tutorial) return;
            var previous = tutorial.Step;
            tutorial.Advance(new TutorialObservation(p.Frame.TimeSeconds, p.State, runner.JumpCount,
                runner.LastJumpAmplitude, runner.IsGrounded, runner.IsGrabbed,
                grab.Target == null ? null : grab.Target.StableId, terminal.IsOpen, lockController.IsEngaged));
            if (previous != tutorial.Step) EnterTutorialStep(tutorial.Step);
        }
        public void StartCalibration()
        {
            SetGameplay(false); candidate = null; choice = 0; pendingConfirm = false;
            recording = new CalibrationSession(input.Source.Info.Id); Stage = SetupStage.Recording; cancelSince = restSince = double.NaN;
        }
        private void Confirm()
        {
            if (Stage == SetupStage.Welcome)
            {
                if (choice == 0) StartCalibration();
                else if (saved != null && choice == 1)
                { candidate = saved; input.ApplyCalibration(candidate.ToCalibration()); Stage = SetupStage.VerifyRest; restSince = double.NaN; }
                else
                {
                    ProfileStore.Delete(sourceId); saved = null;
                    if (sourceId == EmulatorEmgSource.SourceId) input.ApplyCalibration(new EmgCalibration(sourceId));
                    choice = 0;
                }
            }
            else if (Stage == SetupStage.Review)
            {
                if (choice == 1) { StartCalibration(); return; }
                ProfileStore.Save(candidate); saved = candidate; StartTutorial();
            }
            else if (Stage == SetupStage.Error) StartCalibration();
            else if (Stage == SetupStage.Complete) SceneManager.LoadScene("Neon_L1_Underground");
        }
        private void StartTutorial()
        {
            if (!string.IsNullOrEmpty(TutorialScene))
            {
                SceneManager.LoadScene(TutorialScene);
                return;
            }
            tutorial = new TutorialSession(); Stage = SetupStage.Tutorial;
            runner.ResetTo(new Vector2(0,.82f)); runner.RunSpeed = 0; SetGameplay(true);
            EnterTutorialStep(tutorial.Step);
        }
        private void EnterTutorialStep(TutorialStep step)
        {
            nearTarget.gameObject.SetActive(step == TutorialStep.NearGrab || step == TutorialStep.RestInGrab || step == TutorialStep.Release);
            farTarget.gameObject.SetActive(step == TutorialStep.FarGrab || step == TutorialStep.ReleaseFar);
            terminal.gameObject.SetActive(step == TutorialStep.Lock);
            if (step == TutorialStep.FarGrab || step == TutorialStep.Lock) runner.ResetTo(new Vector2(0,.82f));
            if (step == TutorialStep.Complete) { Stage = SetupStage.Complete; SetGameplay(false); choice = 0; }
        }
        private void Fail(string message) { Stage = SetupStage.Error; error = message; pendingConfirm = false; choice = 0; restSince = double.NaN; SetGameplay(false); }
        private string Instruction()
        {
            switch (Stage)
            {
                case SetupStage.Welcome: return "С (F) — следующий пункт, Р (R) — подтвердить. После действия отпускайте оба канала.";
                case SetupStage.Recording:
                    if (recording.Stage == CalibrationStage.Rest) return $"Покой: не нажимайте F/R 3 секунды. {recording.RestProgress:P0}";
                    return $"Канал {(recording.Stage == CalibrationStage.Flex ? "С / F" : "Р / R")}: три комфортных нажатия по 0.3–1 с с отпусканием. Готово {recording.Repetitions}/3. Второй канал в покое.";
                case SetupStage.VerifyRest: return "Проверка профиля: отпустите оба канала на полсекунды.";
                case SetupStage.VerifyFlex: return "Проверка: нажмите только С / F, затем отпустите.";
                case SetupStage.RestAfterFlex: case SetupStage.RestAfterExtend: return "Отпустите оба канала.";
                case SetupStage.VerifyExtend: return "Проверка: нажмите только Р / R, затем отпустите.";
                case SetupStage.VerifyBoth: return "Проверка: нажмите оба канала С+Р / F+R, затем отпустите.";
                case SetupStage.FinalRest: return "Отпустите оба канала перед сохранением.";
                case SetupStage.Review: return "Проверка пройдена. С — выбрать, Р — подтвердить и отпустить.";
                case SetupStage.Error: return error + " Р и покой — повторить.";
                case SetupStage.Complete: return "Обучение завершено! Р и покой — перейти на стенд механик.";
                case SetupStage.Tutorial:
                    switch (tutorial.Step)
                    {
                        case TutorialStep.Rest: return "Шаг 1: расслабьте оба канала на полсекунды.";
                        case TutorialStep.WeakJump: return "Шаг 2: уменьшите Р ползунком примерно до половины опорного значения. Нажмите R для низкого прыжка и дождитесь посадки.";
                        case TutorialStep.StrongJump: return "Шаг 3: увеличьте Р до комфортного опорного значения. Прыгните сильнее и дождитесь посадки.";
                        case TutorialStep.NearGrab: return "Шаг 4: нажмите F для ближнего захвата. Дальность зависит от амплитуды С.";
                        case TutorialStep.RestInGrab: return "Шаг 5: отпустите F/R. Захват сохраняется без напряжения — подождите 0.75 с.";
                        case TutorialStep.Release: return "Шаг 6: нажмите R для отпускания. В захвате Р не прыгает.";
                        case TutorialStep.FarGrab: return "Шаг 7: отпустите каналы, увеличьте С до опорного значения и нажмите F для дальней цели.";
                        case TutorialStep.ReleaseFar: return "Расслабьтесь, затем нажмите R, чтобы отпустить дальнюю цель.";
                        case TutorialStep.Lock: return "Шаг 8: у синего замка сначала покой, затем F+R 0.5 с. После открытия отпустите оба канала.";
                    }
                    break;
            }
            return "Подготовка";
        }
        private void OnGUI()
        {
            if (input == null || input.Pipeline == null) return;
            Rect safe = Screen.safeArea;
            GUILayout.BeginArea(new Rect(safe.x + 16, Screen.height - safe.yMax + 16, Mathf.Min(540, safe.width * .4f), Mathf.Min(500, safe.height * .6f)), GUI.skin.box);
            GUILayout.Label("BIOPULSE — настройка и обучение");
            GUILayout.Label($"Источник: {input.Source.Info.Id} / {input.Source.Status}");
            GUILayout.Label($"С: {input.Pipeline.Frame.Flexor:F2}  |  Р: {input.Pipeline.Frame.Extensor:F2}  |  {Stage}");
            var wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label(Instruction(), wrap);
            if (Stage == SetupStage.Recording) GUILayout.Label("Отмена настройки: удерживайте F+R одну секунду.", wrap);
            if (input.Pipeline.ResumeRequired) GUILayout.Label("Пауза ввода: покой → Р → покой для продолжения.", wrap);
            if (Stage == SetupStage.Welcome)
            {
                GUILayout.Label((choice == 0 ? "► " : "   ") + "Новая калибровка");
                if (saved != null) GUILayout.Label((choice == 1 ? "► " : "   ") + "Загрузить сохранённый профиль (с повторной проверкой)");
                GUILayout.Label((choice == OptionCount - 1 ? "► " : "   ") + "Удалить профиль / восстановить эмулятор 0..1");
            }
            if (candidate != null) GUILayout.Label($"Профиль v{candidate.version}: С {candidate.flexBaseline:F2}…{candidate.flexReference:F2}, Р {candidate.extendBaseline:F2}…{candidate.extendReference:F2}");
            if (Stage == SetupStage.Review) GUILayout.Label(choice == 0 ? "► Сохранить и начать обучение     Повторить" : "Сохранить и начать обучение     ► Повторить");
            if (tutorial != null && tutorial.Feedback != null) GUILayout.Label(tutorial.Feedback, wrap);
            GUILayout.Label($"Амплитуда эмулятора С: {input.FlexAmplitude:F2}"); input.FlexAmplitude = GUILayout.HorizontalSlider(input.FlexAmplitude,0,1);
            GUILayout.Label($"Амплитуда эмулятора Р: {input.ExtendAmplitude:F2}"); input.ExtendAmplitude = GUILayout.HorizontalSlider(input.ExtendAmplitude,0,1);
            GUILayout.Label("Клавиатура: F / R. Touch: две нижние зоны, высота пальца задаёт амплитуду.", wrap);
            GUILayout.EndArea();
        }
    }
}
