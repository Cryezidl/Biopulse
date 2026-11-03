using Biopulse.Input;
using UnityEngine;

namespace Biopulse.Gameplay
{
    // Developer-only scene composition and readout. User-facing session/menu comes later.
    public sealed class RunnerSandbox : MonoBehaviour
    {
        [SerializeField] private RunnerMotor2D runner;
        [SerializeField] private EmgInputDriver input;
        [SerializeField] private float resetBelowY = -8;
        private Vector2 spawn;
        public RunnerMotor2D Runner { get => runner; set => runner = value; }
        public EmgInputDriver Input { get => input; set => input = value; }

        private void Start()
        {
            if (runner == null || input == null)
            { Debug.LogError("RunnerSandbox needs a runner and an input driver.", this); enabled = false; return; }
            spawn = runner.Body.position;
        }
        private void LateUpdate()
        {
            if (runner == null || input == null) return;
            var p = input.Pipeline;
            bool unavailable = p == null || !input.isActiveAndEnabled || !p.Frame.IsValid || p.WaitingForRest || p.ResumeRequired;
            runner.SetSuspended(unavailable);
            if (!unavailable && runner.Body.position.y < resetBelowY) runner.ResetTo(spawn);
        }
        private void OnGUI()
        {
            if (runner == null || input == null || input.Pipeline == null) return;
            var safe = Screen.safeArea;
            if (input.ShowDiagnostics)
            {
                if (GUI.Button(new Rect(safe.xMax - 160, Screen.height - safe.yMax + 18, 140, 26), "Close input panel"))
                    input.ShowDiagnostics = false;
                return;
            }
            GUILayout.BeginArea(new Rect(safe.x + 16, Screen.height - safe.yMax + 16, Mathf.Min(430, safe.width - 32), 430), GUI.skin.box);
            GUILayout.Label("BIOPULSE / MOVEMENT TEST");
            GUILayout.Label("Auto-run + Р jump / hurdles: 0.8, 1.6, 2.4 units");
            GUILayout.Label($"Motion: {runner.State} | Ground: {runner.IsGrounded} | Wall: {runner.IsBlocked}");
            Vector2 v = runner.Body.linearVelocity;
            GUILayout.Label($"Velocity: {v.x:F2}, {v.y:F2} | Run speed: {runner.RunSpeed:F1}");
            var p = input.Pipeline;
            GUILayout.Label($"EMG: {p.State} | С {p.Frame.Flexor:F2} / Р {p.Frame.Extensor:F2}");
            GUILayout.Label("F = С reach; R = jump/release; F+R = blue terminal.");
            var terminal = runner.GetComponent<RunnerLockController>();
            if (terminal != null) GUILayout.Label($"Lock: {(terminal.Current == null ? 0 : terminal.Current.Challenge.Progress) * 100:F0}% | {terminal.Feedback}");
            var grab = runner.GetComponent<RunnerGrabController>();
            if (grab != null) GUILayout.Label($"Reach: {grab.CurrentReach:F2} | {grab.Feedback}");
            GUILayout.Label($"Jump #{runner.JumpCount}: amplitude {runner.LastJumpAmplitude:F2}, impulse {runner.LastJumpImpulse:F2}");
            GUILayout.Label($"Peak height: {runner.LastJumpHeight:F2} | Buffer: {runner.LastJumpUsedBuffer} | Coyote: {runner.LastJumpUsedCoyote}");
            GUILayout.Label($"С amplitude: {input.FlexAmplitude:F2}");
            input.FlexAmplitude = GUILayout.HorizontalSlider(input.FlexAmplitude, 0, 1);
            GUILayout.Label($"Р amplitude: {input.ExtendAmplitude:F2}");
            input.ExtendAmplitude = GUILayout.HorizontalSlider(input.ExtendAmplitude, 0, 1);
            if (p.ResumeRequired)
            {
                GUILayout.Label("Input paused: release F/R and resume.");
                if (GUILayout.Button("Resume input")) p.Resume();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset position")) runner.ResetTo(spawn);
            if (GUILayout.Button(input.ShowDiagnostics ? "Hide input panel" : "Full input panel")) input.ShowDiagnostics = !input.ShowDiagnostics;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
