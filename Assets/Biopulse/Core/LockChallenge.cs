using System;
using Biopulse.Emg;

namespace Biopulse.Gameplay
{
    public enum LockState { WaitingForRest, Ready, Holding, Opened }
    public sealed class LockChallenge
    {
        private readonly double holdSeconds;
        private double started, lastTime = double.NegativeInfinity;
        private bool paused;
        public LockState State { get; private set; }
        public float Progress { get; private set; }
        public LockChallenge(double holdSeconds = .5)
        {
            if (double.IsNaN(holdSeconds) || double.IsInfinity(holdSeconds) || holdSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(holdSeconds));
            this.holdSeconds = holdSeconds;
        }
        public void Reset() { State = LockState.WaitingForRest; Progress = 0; lastTime = double.NegativeInfinity; paused = false; }
        public void Pause() => paused = true;
        public void Advance(EmgIntentKind kind, double time)
        {
            if (State == LockState.Opened) return;
            if (double.IsNaN(time) || double.IsInfinity(time) || time < lastTime || kind == EmgIntentKind.Invalid) { Pause(); return; }
            if (paused) { State = LockState.WaitingForRest; Progress = 0; paused = false; }
            lastTime = time;
            if (kind == EmgIntentKind.Rest) { State = LockState.Ready; Progress = 0; return; }
            if (State == LockState.WaitingForRest) return;
            if (kind != EmgIntentKind.Coactivate)
            {
                // Before overlap starts, a leading channel is preparation, not a failed attempt.
                // Only interruption of an actual two-channel hold requires another rest.
                if (State == LockState.Holding) State = LockState.WaitingForRest;
                Progress = 0; return;
            }
            if (State == LockState.Ready) { State = LockState.Holding; started = time; }
            double elapsed = time - started;
            Progress = (float)Math.Min(1, elapsed / holdSeconds);
            if (elapsed + 1e-9 >= holdSeconds) { Progress = 1; State = LockState.Opened; }
        }
    }
}
