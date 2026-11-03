using Biopulse.Emg;
using UnityEngine;

namespace Biopulse.Gameplay
{
    public sealed class RunnerLockController : MonoBehaviour
    {
        [SerializeField] private RunnerMotor2D runner;
        [SerializeField] private LayerMask terminalLayers = ~0;
        private int resetVersion;
        public RunnerMotor2D Runner { get => runner; set { runner = value; resetVersion = value == null ? 0 : value.ResetVersion; } }
        public LayerMask TerminalLayers { get => terminalLayers; set => terminalLayers = value; }
        public LockTerminal Current { get; private set; }
        public bool IsEngaged => Current != null;
        public string Feedback => Current == null ? "Blue terminal: rest, then hold F+R" :
            (Current.IsOpen ? "Open! Release both to continue" : (Current.Challenge.State == LockState.WaitingForRest ? "Release both channels first" : "Hold both 0.5 s (either key can start first)"));
        public void RefreshContext()
        {
            if (runner == null) return;
            if (runner.ResetVersion != resetVersion)
            {
                resetVersion = runner.ResetVersion; Current = null; runner.SetAtLock(false);
                foreach (var terminal in LockTerminal.Active)
                    if (terminal != null && (terminalLayers.value & (1 << terminal.gameObject.layer)) != 0) terminal.ResetTerminal();
            }
            if (Current != null && !Current.isActiveAndEnabled) Current = null;
            if (Current == null && runner.IsAtLock) runner.SetAtLock(false);
            if (Current != null || runner.IsSuspended || runner.IsGrabbed || !runner.IsGrounded) return;
            foreach (var terminal in LockTerminal.Active)
            {
                if (terminal == null || !terminal.isActiveAndEnabled || terminal.IsOpen ||
                    (terminalLayers.value & (1 << terminal.gameObject.layer)) == 0 || !terminal.Contains(runner.Body.position)) continue;
                Current = terminal; terminal.Challenge.Reset(); runner.SetAtLock(true); return;
            }
        }
        public void Step(EmgIntentKind kind, double sampleTime, bool ready)
        {
            RefreshContext();
            if (Current == null) return;
            if (!ready) { Current.Challenge.Pause(); return; }
            bool wasOpen = Current.IsOpen;
            Current.Challenge.Advance(kind, sampleTime); Current.ApplyVisuals();
            // The opening combination and its trailing single channel must never escape into gameplay.
            if (wasOpen && kind == EmgIntentKind.Rest)
            { Current = null; runner.SetAtLock(false); }
        }
        private void OnDisable() { Current = null; runner?.SetAtLock(false); }
    }
}
