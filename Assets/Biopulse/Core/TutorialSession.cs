using System;
using Biopulse.Emg;

namespace Biopulse.Gameplay
{
    public enum TutorialStep { Rest, WeakJump, StrongJump, NearGrab, RestInGrab, Release, FarGrab, ReleaseFar, Lock, Complete }
    public readonly struct TutorialObservation
    {
        public readonly double Time;
        public readonly EmgIntentKind Input;
        public readonly int Jumps;
        public readonly float JumpAmplitude;
        public readonly bool Grounded, Grabbed, LockOpen, AtLock;
        public readonly string TargetId;
        public TutorialObservation(double time, EmgIntentKind input, int jumps, float jumpAmplitude,
            bool grounded, bool grabbed, string targetId, bool lockOpen, bool atLock)
        { Time = time; Input = input; Jumps = jumps; JumpAmplitude = jumpAmplitude; Grounded = grounded;
          Grabbed = grabbed; TargetId = targetId; LockOpen = lockOpen; AtLock = atLock; }
    }
    public sealed class TutorialSession
    {
        private double restSince = double.NaN, lastTime = double.NegativeInfinity;
        private int seenJumps;
        private float weakAmplitude;
        public TutorialStep Step { get; private set; }
        public string Feedback { get; private set; }
        public void Pause() => restSince = double.NaN;
        public void Advance(TutorialObservation o)
        {
            if (!Numbers.Finite(o.Time) || o.Time < lastTime || o.Input == EmgIntentKind.Invalid) { Pause(); return; }
            if (o.Time - lastTime > .25) Pause();
            lastTime = o.Time;
            switch (Step)
            {
                case TutorialStep.Rest:
                    if (RestLongEnough(o, .5)) Next(TutorialStep.WeakJump, o); break;
                case TutorialStep.WeakJump:
                case TutorialStep.StrongJump:
                    if (o.Jumps <= seenJumps || !o.Grounded) break;
                    seenJumps = o.Jumps;
                    if (Step == TutorialStep.WeakJump)
                    {
                        if (o.JumpAmplitude > .65f) { Feedback = "Для первого прыжка уменьшите амплитуду Р."; break; }
                        weakAmplitude = o.JumpAmplitude; Next(TutorialStep.StrongJump, o);
                    }
                    else if (o.JumpAmplitude >= weakAmplitude + .15f) Next(TutorialStep.NearGrab, o);
                    else Feedback = "Сделайте второй прыжок сильнее первого.";
                    break;
                case TutorialStep.NearGrab:
                    if (o.Grabbed && o.TargetId == "tutorial-near") Next(TutorialStep.RestInGrab, o); break;
                case TutorialStep.RestInGrab:
                    if (!o.Grabbed) { Next(TutorialStep.NearGrab, o); Feedback = "Снова захватите цель и расслабьтесь."; }
                    else if (RestLongEnough(o, .75)) Next(TutorialStep.Release, o);
                    break;
                case TutorialStep.Release:
                    if (!o.Grabbed && o.Jumps == seenJumps) Next(TutorialStep.FarGrab, o); break;
                case TutorialStep.FarGrab:
                    if (o.Grabbed && o.TargetId == "tutorial-far") Next(TutorialStep.ReleaseFar, o); break;
                case TutorialStep.ReleaseFar:
                    if (!o.Grabbed && o.Jumps == seenJumps) Next(TutorialStep.Lock, o); break;
                case TutorialStep.Lock:
                    if (o.LockOpen && !o.AtLock && o.Input == EmgIntentKind.Rest) Next(TutorialStep.Complete, o); break;
            }
        }
        private bool RestLongEnough(TutorialObservation o, double seconds)
        {
            if (o.Input != EmgIntentKind.Rest) { restSince = double.NaN; return false; }
            if (double.IsNaN(restSince)) restSince = o.Time;
            return o.Time - restSince + 1e-9 >= seconds;
        }
        private void Next(TutorialStep step, TutorialObservation o)
        { Step = step; restSince = double.NaN; seenJumps = o.Jumps; Feedback = null; }
    }
}
