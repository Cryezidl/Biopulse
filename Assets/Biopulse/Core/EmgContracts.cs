using System;

namespace Biopulse.Emg
{
    public enum EmgRepresentation { Raw, Envelope }
    public enum EmgSampleQuality { Unknown, Valid, Invalid }
    public enum EmgSourceStatus { Stopped, Starting, Streaming, Disconnected, Error }
    public enum EmgIntentKind { Invalid, Rest, Flex, Extend, Coactivate }

    public readonly struct EmgSourceInfo
    {
        public readonly string Id;
        public readonly EmgRepresentation Representation;
        public readonly string Units;
        public readonly double? NominalRateHz;
        public EmgSourceInfo(string id, EmgRepresentation representation, string units, double? rate = null)
        { Id = id; Representation = representation; Units = units; NominalRateHz = rate; }
    }

    public readonly struct EmgSample
    {
        public readonly long Sequence;
        public readonly double TimeSeconds;
        public readonly float Flexor, Extensor;
        public readonly EmgSampleQuality Quality;
        public EmgSample(long sequence, double timeSeconds, float flexor, float extensor,
            EmgSampleQuality quality = EmgSampleQuality.Valid)
        { Sequence = sequence; TimeSeconds = timeSeconds; Flexor = flexor; Extensor = extensor; Quality = quality; }
    }

    public interface IEmgSource : IDisposable
    {
        EmgSourceInfo Info { get; }
        EmgSourceStatus Status { get; }
        void Start();
        void Stop();
        bool TryRead(out EmgSample sample);
    }

    public readonly struct NormalizedEmgFrame
    {
        public readonly double TimeSeconds;
        public readonly float Flexor, Extensor;
        public readonly bool IsValid;
        public readonly int CalibrationVersion;
        public NormalizedEmgFrame(double time, float flexor, float extensor, bool valid, int version)
        { TimeSeconds = time; Flexor = flexor; Extensor = extensor; IsValid = valid; CalibrationVersion = version; }
    }

    // An intention is not yet a jump/grab: the next stage routes it using gameplay context.
    public readonly struct EmgIntent
    {
        public readonly long Id;
        public readonly EmgIntentKind Kind;
        public readonly float Flexor, Extensor;
        public readonly double InputTimeSeconds, RecognizedTimeSeconds;
        public EmgIntent(long id, EmgIntentKind kind, float flexor, float extensor, double inputTime, double recognizedTime)
        { Id = id; Kind = kind; Flexor = flexor; Extensor = extensor; InputTimeSeconds = inputTime; RecognizedTimeSeconds = recognizedTime; }
    }
}
