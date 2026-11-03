using System;

namespace Biopulse.Emg
{
    [Serializable]
    public sealed class EmgSettings
    {
        public float OnThreshold = .25f;
        public float OffThreshold = .15f;
        public float DeadZone = .03f;
        public double ConfirmationSeconds = .02;
        public double ArbitrationSeconds = .02;
        public double StaleSeconds = .25;
        public double SmoothingSeconds = .008;
        // Normalized emulator units per second. Tune again for another source.
        public float MaxSlewPerSecond = 40f;

        public void Validate()
        {
            if (!Numbers.Finite(OnThreshold) || !Numbers.Finite(OffThreshold) ||
                !Numbers.Finite(DeadZone) || OffThreshold < 0 || OnThreshold > 1 ||
                OnThreshold <= OffThreshold || DeadZone < 0 || DeadZone > OffThreshold ||
                !Numbers.Finite(ConfirmationSeconds) || ConfirmationSeconds < 0 ||
                !Numbers.Finite(ArbitrationSeconds) || ArbitrationSeconds < 0 ||
                !Numbers.Finite(StaleSeconds) || StaleSeconds <= 0 ||
                !Numbers.Finite(SmoothingSeconds) || SmoothingSeconds < 0 ||
                !Numbers.Finite(MaxSlewPerSecond) || MaxSlewPerSecond <= 0)
                throw new ArgumentException("Invalid EMG processing settings.");
        }
    }

    public sealed class EmgCalibration
    {
        public string SourceId { get; }
        public int Version { get; }
        public float FlexBaseline { get; }
        public float FlexReference { get; }
        public float ExtendBaseline { get; }
        public float ExtendReference { get; }

        public EmgCalibration(string sourceId, float flexBaseline = 0, float flexReference = 1,
            float extendBaseline = 0, float extendReference = 1, int version = 1)
        {
            if (string.IsNullOrEmpty(sourceId) || version < 1 ||
                !Numbers.Finite(flexBaseline) || !Numbers.Finite(flexReference) ||
                !Numbers.Finite(extendBaseline) || !Numbers.Finite(extendReference) ||
                flexReference - flexBaseline < .001f || extendReference - extendBaseline < .001f)
                throw new ArgumentException("Calibration needs finite, distinct baseline/reference values.");
            SourceId = sourceId; Version = version;
            FlexBaseline = flexBaseline; FlexReference = flexReference;
            ExtendBaseline = extendBaseline; ExtendReference = extendReference;
        }
    }

    internal static class Numbers
    {
        internal static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        internal static float Clamp01(float v) => Math.Max(0, Math.Min(1, v));
    }
}
