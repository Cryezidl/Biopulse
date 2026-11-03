using Biopulse.Emg;
using Biopulse.Input;
using UnityEngine;

namespace Biopulse.Gameplay
{
    // Context routing grows here when grab/lock states arrive. The motor sees only GameCommand.
    [DefaultExecutionOrder(-50)]
    public sealed class RunnerCommandRouter : MonoBehaviour
    {
        [SerializeField] private EmgInputDriver input;
        [SerializeField] private RunnerMotor2D runner;
        [SerializeField] private RunnerGrabController grab;
        [SerializeField] private RunnerLockController terminal;
        public RunnerLockController Terminal { get => terminal; set => terminal = value; }
        public RunnerGrabController Grab { get => grab; set => grab = value; }
        private EmgInputDriver subscribedInput;
        private EmgPipeline observedPipeline;
        private long lastIntentId, nextCommandId;
        public RunnerMotor2D Runner { get => runner; set => runner = value; }
        public EmgInputDriver Input
        {
            get => input;
            set { Unsubscribe(); input = value; observedPipeline = null; if (isActiveAndEnabled) Subscribe(); }
        }
        private void OnEnable() => Subscribe();
        private void OnDisable() { Unsubscribe(); runner?.ClearPendingJump(); grab?.CancelReach(); terminal?.Current?.Challenge.Pause(); }
        private void Subscribe()
        {
            if (input == null || subscribedInput == input) return;
            subscribedInput = input; subscribedInput.IntentRecognized += OnIntent;
        }
        private void Unsubscribe()
        {
            if (subscribedInput != null) subscribedInput.IntentRecognized -= OnIntent;
            subscribedInput = null;
        }
        private void Update()
        {
            TrackPipeline();
            if (!InputReady()) runner?.ClearPendingJump();
        }
        private void FixedUpdate()
        {
            TrackPipeline();
            // Check before the motor's physics step, including focus loss between Update and FixedUpdate.
            bool ready = InputReady();
            if (!ready) runner?.ClearPendingJump();
            runner?.SetSuspended(!ready);
            terminal?.Step(ready ? input.Pipeline.State : EmgIntentKind.Invalid,
                input != null && input.Pipeline != null ? input.Pipeline.Frame.TimeSeconds : 0, ready);
            if (terminal != null && terminal.IsEngaged) { runner?.ClearPendingJump(); grab?.CancelReach(); return; }
            if (ready && input.Pipeline.State == EmgIntentKind.Coactivate) grab?.CancelReach();
            grab?.Step(ready ? input.Pipeline.Frame.Flexor : 0, ready, Time.fixedDeltaTime);
        }
        private void TrackPipeline()
        {
            var pipeline = input == null ? null : input.Pipeline;
            if (ReferenceEquals(observedPipeline, pipeline)) return;
            observedPipeline = pipeline; lastIntentId = 0; runner?.ClearPendingJump(); grab?.CancelReach();
            terminal?.Current?.Challenge.Pause();
        }
        private bool InputReady() => input != null && input.isActiveAndEnabled && input.Source != null &&
            input.Source.Status == EmgSourceStatus.Streaming && input.Pipeline != null &&
            input.Pipeline.Frame.IsValid && !input.Pipeline.WaitingForRest && !input.Pipeline.ResumeRequired;
        private void OnIntent(EmgIntent intent) => TryRoute(intent);

        public bool TryRoute(EmgIntent intent)
        {
            TrackPipeline();
            if (!isActiveAndEnabled || runner == null || !InputReady() || intent.Id <= lastIntentId) return false;
            lastIntentId = intent.Id;
            terminal?.RefreshContext();
            if (terminal != null && terminal.IsEngaged) return false;
            if (intent.Kind == EmgIntentKind.Flex) return grab != null && grab.BeginReach(intent.Flexor);
            if (intent.Kind == EmgIntentKind.Extend && runner.IsGrabbed)
            { grab?.Release(); return true; }
            if (intent.Kind != EmgIntentKind.Extend) return false;
            grab?.CancelReach();
            return runner.QueueCommand(new GameCommand(++nextCommandId, GameCommandKind.Jump,
                intent.Extensor, intent.InputTimeSeconds, intent.RecognizedTimeSeconds));
        }
    }
}
