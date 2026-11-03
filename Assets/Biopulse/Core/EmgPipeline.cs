using System;

namespace Biopulse.Emg
{
    // No Unity APIs: sample-time processing is deterministic across render frame rates.
    public sealed class EmgPipeline
    {
        private readonly EmgSettings settings;
        private readonly EmgCalibration calibration;
        private bool haveSample, initialized, flexActive, extendActive, latched, candidate;
        private double lastTime, gestureTime, candidateTime;
        private long lastSequence = -1, nextIntentId;
        private float slewFlex, slewExtend, smoothFlex, smoothExtend;
        private EmgIntentKind candidateKind;

        public NormalizedEmgFrame Frame { get; private set; }
        public EmgSample Raw { get; private set; }
        public EmgIntentKind State { get; private set; } = EmgIntentKind.Invalid;
        public bool WaitingForRest { get; private set; } = true;
        public bool ResumeRequired { get; private set; }
        public string Fault { get; private set; } = "Waiting for source";
        public long RejectedSamples { get; private set; }
        public long MissingSamples { get; private set; }
        public event Action<EmgIntent> IntentRecognized;

        public EmgPipeline(EmgSourceInfo info, EmgSettings settings, EmgCalibration calibration)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.calibration = calibration ?? throw new ArgumentNullException(nameof(calibration));
            settings.Validate();
            if (info.Representation != EmgRepresentation.Envelope)
                throw new NotSupportedException("Raw EMG needs a device-specific envelope processor.");
            if (calibration.SourceId != info.Id) throw new ArgumentException("Calibration belongs to another source.");
        }

        public void Suspend(string reason)
        {
            Fault = reason; State = EmgIntentKind.Invalid; WaitingForRest = true; ResumeRequired = true;
            initialized = false; flexActive = extendActive = latched = candidate = false;
            Frame = new NormalizedEmgFrame(Frame.TimeSeconds, 0, 0, false, calibration.Version);
        }

        // Session/UI must explicitly resume after a fault. Held input still cannot act before rest.
        public bool Resume()
        {
            if (!Frame.IsValid || WaitingForRest || Frame.Flexor > settings.OffThreshold || Frame.Extensor > settings.OffThreshold ||
                Normalize(Raw.Flexor, calibration.FlexBaseline, calibration.FlexReference) > settings.OffThreshold ||
                Normalize(Raw.Extensor, calibration.ExtendBaseline, calibration.ExtendReference) > settings.OffThreshold) return false;
            ResumeRequired = false; Fault = null; return true;
        }

        public void Tick(double now, EmgSourceStatus sourceStatus)
        {
            if (sourceStatus != EmgSourceStatus.Streaming) { Suspend("Source: " + sourceStatus); return; }
            if (!Numbers.Finite(now) || !haveSample || now - lastTime > settings.StaleSeconds)
                Suspend("No fresh valid data");
        }

        public bool Push(EmgSample sample, double now)
        {
            if (sample.Sequence < 0 || !Numbers.Finite(now) || !Numbers.Finite(sample.TimeSeconds) ||
                !Numbers.Finite(sample.Flexor) || !Numbers.Finite(sample.Extensor) ||
                sample.Quality != EmgSampleQuality.Valid || sample.TimeSeconds > now + .000001 ||
                now - sample.TimeSeconds > settings.StaleSeconds ||
                (haveSample && (sample.TimeSeconds <= lastTime || sample.Sequence <= lastSequence)))
            { RejectedSamples++; Suspend("Invalid, stale or out-of-order sample"); return false; }

            if (haveSample && sample.TimeSeconds - lastTime > settings.StaleSeconds)
                Suspend("Gap in input");
            if (haveSample && sample.Sequence > lastSequence + 1) MissingSamples += sample.Sequence - lastSequence - 1;
            double dt = haveSample ? sample.TimeSeconds - lastTime : 0;
            lastTime = sample.TimeSeconds; lastSequence = sample.Sequence; haveSample = true; Raw = sample;

            // Limiter and EMA operate in source units; emulator's calibrated span is 0..1.
            if (!initialized)
            {
                slewFlex = smoothFlex = calibration.FlexBaseline;
                slewExtend = smoothExtend = calibration.ExtendBaseline;
                initialized = true;
            }
            float limit = (float)(settings.MaxSlewPerSecond * dt);
            slewFlex = Approach(slewFlex, sample.Flexor, limit);
            slewExtend = Approach(slewExtend, sample.Extensor, limit);
            float alpha = settings.SmoothingSeconds == 0 ? 1 : (float)(1 - Math.Exp(-dt / settings.SmoothingSeconds));
            smoothFlex += alpha * (slewFlex - smoothFlex);
            smoothExtend += alpha * (slewExtend - smoothExtend);
            float flex = Normalize(smoothFlex, calibration.FlexBaseline, calibration.FlexReference);
            float extend = Normalize(smoothExtend, calibration.ExtendBaseline, calibration.ExtendReference);
            Frame = new NormalizedEmgFrame(lastTime, flex, extend, true, calibration.Version);

            // Recovery requires RAW normalized rest too, so filter reset cannot invent a rest.
            bool physicalRest = Normalize(sample.Flexor, calibration.FlexBaseline, calibration.FlexReference) <= settings.OffThreshold &&
                Normalize(sample.Extensor, calibration.ExtendBaseline, calibration.ExtendReference) <= settings.OffThreshold;
            if (WaitingForRest)
            {
                if (!physicalRest || flex > settings.OffThreshold || extend > settings.OffThreshold) return true;
                WaitingForRest = false; State = EmgIntentKind.Rest;
                if (!ResumeRequired) Fault = null;
                return true;
            }
            if (ResumeRequired) return true;
            Fault = null;
            bool wasRest = !flexActive && !extendActive;
            flexActive = flexActive ? flex > settings.OffThreshold : flex >= settings.OnThreshold;
            extendActive = extendActive ? extend > settings.OffThreshold : extend >= settings.OnThreshold;
            State = flexActive ? (extendActive ? EmgIntentKind.Coactivate : EmgIntentKind.Flex) :
                (extendActive ? EmgIntentKind.Extend : EmgIntentKind.Rest);
            if (State == EmgIntentKind.Rest) { candidate = latched = false; return true; }
            if (latched) return true;
            if (wasRest) gestureTime = lastTime;
            if (!candidate || candidateKind != State)
            { candidate = true; candidateKind = State; candidateTime = lastTime; }
            // Confirmation and arbitration overlap; a newly arrived second channel must also be stable.
            if (lastTime - candidateTime + 1e-9 < settings.ConfirmationSeconds ||
                lastTime - gestureTime + 1e-9 < settings.ArbitrationSeconds) return true;
            latched = true;
            IntentRecognized?.Invoke(new EmgIntent(++nextIntentId, State, flex, extend, gestureTime, lastTime));
            return true;
        }

        private float Normalize(float value, float baseline, float reference)
        {
            float result = Numbers.Clamp01((value - baseline) / (reference - baseline));
            return result < settings.DeadZone ? 0 : result;
        }
        private static float Approach(float current, float target, float step) =>
            current + Math.Max(-step, Math.Min(step, target - current));
    }
}
