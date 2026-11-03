using System;
using System.Collections.Generic;
using Biopulse.Emg;
using NUnit.Framework;

namespace Biopulse.Tests
{
    public sealed class EmgPipelineTests
    {
        private sealed class Bench
        {
            public readonly List<EmgIntent> Intents = new List<EmgIntent>();
            public readonly EmgPipeline Pipeline;
            private long sequence;
            public Bench(EmgSettings settings = null, EmgCalibration calibration = null)
            {
                Pipeline = new EmgPipeline(new EmulatorEmgSource().Info, settings ?? FastSettings(),
                    calibration ?? new EmgCalibration(EmulatorEmgSource.SourceId));
                Pipeline.IntentRecognized += Intents.Add;
                Push(0, 0, 0);
            }
            public void Push(double time, float flex, float extend) => Pipeline.Push(new EmgSample(sequence++, time, flex, extend), time);
            public void Hold(double from, double to, float flex, float extend)
            {
                for (double t = from; t < to + .000001; t += .005) Push(t, flex, extend);
            }
        }
        private static EmgSettings FastSettings() => new EmgSettings { SmoothingSeconds = 0, MaxSlewPerSecond = 100000 };

        [Test]
        public void HoldingChannelEmitsOnceAndRestRearms()
        {
            var b = new Bench(); b.Hold(.01, .3, 0, .6f);
            Assert.That(b.Intents.Count, Is.EqualTo(1));
            Assert.That(b.Intents[0].Kind, Is.EqualTo(EmgIntentKind.Extend));
            b.Push(.31, 0, 0); b.Hold(.32, .4, 0, .8f);
            Assert.That(b.Intents.Count, Is.EqualTo(2));
            Assert.That(b.Intents[1].Id, Is.GreaterThan(b.Intents[0].Id));
        }

        [TestCase(true)] [TestCase(false)]
        public void NearSimultaneousChannelsEmitOnlyCombination(bool flexFirst)
        {
            var b = new Bench(); b.Push(.01, flexFirst ? .7f : 0, flexFirst ? 0 : .7f);
            b.Hold(.02, .2, .7f, .7f);
            Assert.That(b.Intents.Count, Is.EqualTo(1));
            Assert.That(b.Intents[0].Kind, Is.EqualTo(EmgIntentKind.Coactivate));
            b.Hold(.21, .3, 0, .7f); // Releasing one channel cannot turn the same gesture into Extend.
            Assert.That(b.Intents.Count, Is.EqualTo(1));
        }

        [Test]
        public void ArbitrationAndConfirmationOverlap()
        {
            var b = new Bench(); b.Hold(.01, .03, .7f, 0);
            Assert.That(b.Intents.Count, Is.EqualTo(1));
            Assert.That(b.Intents[0].RecognizedTimeSeconds - b.Intents[0].InputTimeSeconds, Is.EqualTo(.02).Within(.000001));
        }

        [Test]
        public void SecondChannelAtWindowBoundaryWinsBeforeSingleDispatch()
        {
            var b = new Bench(); b.Push(.01, .7f, 0); b.Push(.02, .7f, 0);
            b.Hold(.03, .08, .7f, .7f);
            Assert.That(b.Intents.Count, Is.EqualTo(1));
            Assert.That(b.Intents[0].Kind, Is.EqualTo(EmgIntentKind.Coactivate));
        }

        [Test]
        public void ShortSpikesAndThresholdNoiseDoNotEmit()
        {
            var b = new Bench();
            for (int i = 1; i <= 20; i++) b.Push(i * .005, i % 2 == 1 ? .26f : .14f, 0);
            Assert.That(b.Intents, Is.Empty);
        }

        [Test]
        public void HysteresisKeepsOneGestureUntilOffThreshold()
        {
            var b = new Bench(); b.Hold(.01, .05, .3f, 0); b.Hold(.06, .1, .2f, 0);
            Assert.That(b.Pipeline.State, Is.EqualTo(EmgIntentKind.Flex));
            b.Push(.11, .15f, 0);
            Assert.That(b.Pipeline.State, Is.EqualTo(EmgIntentKind.Rest));
            Assert.That(b.Intents.Count, Is.EqualTo(1));
        }

        [TestCase(.35f)] [TestCase(.6f)] [TestCase(.9f)]
        public void AmplitudeSurvivesProcessing(float amplitude)
        {
            var b = new Bench(); b.Hold(.01, .06, 0, amplitude);
            Assert.That(b.Intents[0].Extensor, Is.EqualTo(amplitude).Within(.001));
        }

        [Test]
        public void DefaultFiltersPreserveThreeDifferentStepAmplitudes()
        {
            float previous = 0;
            foreach (float amplitude in new[] { .35f, .6f, .9f })
            {
                var b = new Bench(new EmgSettings()); b.Hold(.005, .15, 0, amplitude);
                Assert.That(b.Intents.Count, Is.EqualTo(1));
                Assert.That(b.Intents[0].Extensor, Is.GreaterThan(previous));
                // Core-only synthetic timing. Does NOT verify emulator-to-screen latency.
                Assert.That(b.Intents[0].RecognizedTimeSeconds, Is.LessThan(.1));
                previous = b.Intents[0].Extensor;
            }
        }

        [Test]
        public void CalibrationNormalizesChannelsIndependentlyAndClamps()
        {
            var calibration = new EmgCalibration(EmulatorEmgSource.SourceId, 1, 3, 10, 14);
            var b = new Bench(calibration: calibration); b.Push(.01, 2, 11);
            Assert.That(b.Pipeline.Frame.Flexor, Is.EqualTo(.5f));
            Assert.That(b.Pipeline.Frame.Extensor, Is.EqualTo(.25f));
            b.Push(.02, -5, 40);
            Assert.That(b.Pipeline.Frame.Flexor, Is.Zero);
            Assert.That(b.Pipeline.Frame.Extensor, Is.EqualTo(1));
        }

        [Test]
        public void InvalidCalibrationAndSourceMismatchAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new EmgCalibration("test", 1, 1));
            Assert.Throws<ArgumentException>(() => new EmgCalibration("test", float.NaN));
            Assert.Throws<ArgumentException>(() => new EmgPipeline(new EmulatorEmgSource().Info, FastSettings(), new EmgCalibration("other")));
            Assert.Throws<NotSupportedException>(() => new EmgPipeline(new EmgSourceInfo("raw", EmgRepresentation.Raw, "unknown"), FastSettings(), new EmgCalibration("raw")));
        }

        [Test]
        public void SlewLimitUsesTimeAndFiltersSingleSpike()
        {
            var b = new Bench(new EmgSettings { SmoothingSeconds = 0, MaxSlewPerSecond = 10 });
            b.Push(.01, 1, 0);
            Assert.That(b.Pipeline.Frame.Flexor, Is.EqualTo(.1f).Within(.0001));
            b.Push(.02, 0, 0);
            Assert.That(b.Intents, Is.Empty);
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void NonfiniteSignalIsInvalidNotRest(float value)
        {
            var b = new Bench(); b.Push(.01, value, 0);
            Assert.That(b.Pipeline.State, Is.EqualTo(EmgIntentKind.Invalid));
            Assert.That(b.Pipeline.Frame.IsValid, Is.False);
            Assert.That(b.Pipeline.ResumeRequired, Is.True);
        }

        [Test]
        public void StaleDataNeedsRestAndExplicitResume()
        {
            var b = new Bench(); b.Pipeline.Tick(.3, EmgSourceStatus.Streaming);
            Assert.That(b.Pipeline.State, Is.EqualTo(EmgIntentKind.Invalid));
            b.Hold(.31, .4, 0, .8f);
            Assert.That(b.Intents, Is.Empty);
            Assert.That(b.Pipeline.Resume(), Is.False);
            b.Push(.41, 0, 0);
            Assert.That(b.Pipeline.Resume(), Is.True);
            b.Hold(.42, .5, 0, .8f);
            Assert.That(b.Intents.Count, Is.EqualTo(1));
        }

        [Test]
        public void ResumeCannotAcceptHeldInputAfterRestWasSeen()
        {
            var b = new Bench(); b.Pipeline.Suspend("test"); b.Push(.01, 0, 0); b.Push(.02, 0, .8f);
            Assert.That(b.Pipeline.Resume(), Is.False);
            Assert.That(b.Intents, Is.Empty);
        }

        [Test]
        public void StartupHeldInputNeedsRealRestBeforeArming()
        {
            var p = new EmgPipeline(new EmulatorEmgSource().Info, FastSettings(), new EmgCalibration(EmulatorEmgSource.SourceId));
            int count = 0; p.IntentRecognized += _ => count++;
            for (int i = 0; i < 30; i++) p.Push(new EmgSample(i, i * .005, 0, 1), i * .005);
            Assert.That(p.WaitingForRest, Is.True);
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void InvalidQualityAndOutOfOrderSamplesAreRejected()
        {
            var b = new Bench();
            Assert.That(b.Pipeline.Push(new EmgSample(0, .01, 0, 0), .01), Is.False);
            Assert.That(b.Pipeline.Push(new EmgSample(1, .02, 0, 0, EmgSampleQuality.Unknown), .02), Is.False);
            Assert.That(b.Pipeline.Push(new EmgSample(1, 1, 0, 0), .02), Is.False);
            Assert.That(b.Pipeline.Push(new EmgSample(1, .01, 0, 0), 1), Is.False);
            Assert.That(b.Pipeline.RejectedSamples, Is.EqualTo(4));
        }

        [Test]
        public void BoundedEmulatorDropsOldestSamplesAndClearsOnStop()
        {
            using (var source = new EmulatorEmgSource(2))
            {
                source.Start(); source.Publish(0, 0, 0); source.Publish(.01, .5f, 0); source.Publish(.02, 1, 0);
                Assert.That(source.DroppedSamples, Is.EqualTo(1));
                Assert.That(source.TryRead(out var sample), Is.True);
                Assert.That(sample.TimeSeconds, Is.EqualTo(.01));
                source.Stop(); Assert.That(source.TryRead(out _), Is.False);
            }
        }

        [Test]
        public void ReplayProducesIdenticalIntentionsAt30And144Fps()
        {
            var slow = Replay(30); var fast = Replay(144);
            Assert.That(slow.Count, Is.EqualTo(3)); Assert.That(fast.Count, Is.EqualTo(slow.Count));
            for (int i = 0; i < slow.Count; i++)
            {
                Assert.That(fast[i].Kind, Is.EqualTo(slow[i].Kind));
                Assert.That(fast[i].RecognizedTimeSeconds, Is.EqualTo(slow[i].RecognizedTimeSeconds));
                Assert.That(fast[i].Flexor, Is.EqualTo(slow[i].Flexor));
                Assert.That(fast[i].Extensor, Is.EqualTo(slow[i].Extensor));
            }
        }
        private static List<EmgIntent> Replay(int fps)
        {
            var samples = new List<EmgSample>();
            for (int i = 0; i <= 120; i++)
            {
                float flex = (i >= 10 && i < 25) || (i >= 90 && i < 110) ? .7f : 0;
                float extend = (i >= 50 && i < 65) || (i >= 90 && i < 110) ? .8f : 0;
                samples.Add(new EmgSample(i, i * .005, flex, extend));
            }
            double now = 0;
            using (var source = new ReplayEmgSource(samples, () => now))
            {
                var p = new EmgPipeline(source.Info, new EmgSettings(), new EmgCalibration(source.Info.Id));
                var result = new List<EmgIntent>(); p.IntentRecognized += result.Add; source.Start();
                for (int i = 0; i <= fps * .7; i++)
                {
                    now = i / (double)fps;
                    while (source.TryRead(out var sample)) p.Push(sample, now);
                    p.Tick(now, source.Status);
                }
                return result;
            }
        }
    }
}
