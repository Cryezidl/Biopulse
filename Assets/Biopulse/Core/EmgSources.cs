using System;
using System.Collections.Generic;

namespace Biopulse.Emg
{
    // Main-thread source. Publish samples using the same monotonic clock as the consumer.
    public sealed class EmulatorEmgSource : IEmgSource
    {
        public const string SourceId = "emulator-envelope-v1";
        private readonly Queue<EmgSample> queue;
        private readonly int capacity;
        private long sequence;
        public long DroppedSamples { get; private set; }
        public EmgSourceInfo Info => new EmgSourceInfo(SourceId, EmgRepresentation.Envelope, "arbitrary");
        public EmgSourceStatus Status { get; private set; }
        public EmulatorEmgSource(int capacity = 128)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity; queue = new Queue<EmgSample>(capacity);
        }
        public void Start() { queue.Clear(); Status = EmgSourceStatus.Streaming; }
        public void Stop() { queue.Clear(); Status = EmgSourceStatus.Stopped; }
        public void Publish(double time, float flexor, float extensor, EmgSampleQuality quality = EmgSampleQuality.Valid)
        {
            if (Status != EmgSourceStatus.Streaming) return;
            if (queue.Count == capacity) { queue.Dequeue(); DroppedSamples++; }
            queue.Enqueue(new EmgSample(sequence++, time, flexor, extensor, quality));
        }
        public bool TryRead(out EmgSample sample)
        {
            sample = default;
            if (Status != EmgSourceStatus.Streaming || queue.Count == 0) return false;
            sample = queue.Dequeue(); return true;
        }
        public void Dispose() => Stop();
    }

    // Relative timestamps are mapped onto the application's clock at Start(). Synthetic by default.
    public sealed class ReplayEmgSource : IEmgSource
    {
        private readonly EmgSample[] samples;
        private readonly Func<double> clock;
        private double startedAt;
        private int index;
        public EmgSourceInfo Info { get; }
        public EmgSourceStatus Status { get; private set; }
        public ReplayEmgSource(IEnumerable<EmgSample> samples, Func<double> clock,
            string sourceId = "synthetic-replay-v1")
        {
            if (samples == null || clock == null) throw new ArgumentNullException();
            this.samples = new List<EmgSample>(samples).ToArray(); this.clock = clock;
            double previous = -1;
            foreach (var sample in this.samples)
            {
                if (!Numbers.Finite(sample.TimeSeconds) || sample.TimeSeconds < 0 || sample.TimeSeconds <= previous)
                    throw new ArgumentException("Replay timestamps must increase and be nonnegative.");
                previous = sample.TimeSeconds;
            }
            Info = new EmgSourceInfo(sourceId, EmgRepresentation.Envelope, "arbitrary");
        }
        public void Start() { index = 0; startedAt = clock(); Status = EmgSourceStatus.Streaming; }
        public void Stop() { index = 0; Status = EmgSourceStatus.Stopped; }
        public bool TryRead(out EmgSample sample)
        {
            sample = default;
            if (Status != EmgSourceStatus.Streaming || index == samples.Length ||
                startedAt + samples[index].TimeSeconds > clock()) return false;
            var next = samples[index++];
            sample = new EmgSample(index, startedAt + next.TimeSeconds, next.Flexor, next.Extensor, next.Quality);
            return true;
        }
        public void Dispose() => Stop();
    }
}
