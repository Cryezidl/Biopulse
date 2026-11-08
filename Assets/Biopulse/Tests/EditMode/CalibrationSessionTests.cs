using Biopulse.Emg;
using NUnit.Framework;

namespace Biopulse.Tests
{
    public sealed class CalibrationSessionTests
    {
        private CalibrationSession session;
        private double time;
        private long sequence;
        [SetUp]
        public void Setup() { session = new CalibrationSession("test-envelope"); time = 0; sequence = 0; }
        private void Feed(double seconds, float flex, float extend)
        { for (int i = 0; i < seconds * 100; i++) { session.Push(new EmgSample(sequence++,time,flex,extend)); time += .01; } }
        private void Pulse(float flex, float extend) { Feed(.1,0,0); Feed(.35,flex,extend); Feed(.1,0,0); }
        [Test]
        public void ThreeComfortablePulsesProduceIndependentRobustReferences()
        {
            Feed(3.1,0,0);
            Pulse(.6f,0); Pulse(.65f,0); Pulse(.95f,0); // One overly high repetition cannot become the reference.
            Assert.That(session.Stage,Is.EqualTo(CalibrationStage.Extend));
            Pulse(0,.4f); Pulse(0,.45f); Pulse(0,.5f);
            Assert.That(session.Stage,Is.EqualTo(CalibrationStage.Complete));
            Assert.That(session.Result.FlexReference,Is.EqualTo(.65f).Within(.001));
            Assert.That(session.Result.ExtendReference,Is.EqualTo(.45f).Within(.001));
            Assert.That(session.Result.FlexBaseline,Is.Zero);
        }
        [Test]
        public void ShortPulsesAndOtherChannelDoNotCount()
        {
            Feed(3.1,0,0); Feed(.1,0,0); Feed(.1,.7f,0); Feed(.1,0,0);
            Feed(.4,.7f,.7f); Feed(.2,0,0);
            Assert.That(session.Repetitions,Is.Zero); Assert.That(session.Stage,Is.EqualTo(CalibrationStage.Flex));
        }
        [Test]
        public void MissingAndNonfiniteSamplesCannotCreateProfile()
        {
            Feed(.5,0,0); time += .5; Feed(.1,0,0);
            Assert.That(session.Stage,Is.EqualTo(CalibrationStage.Failed)); Assert.That(session.Result,Is.Null);
            session = new CalibrationSession("test-envelope"); session.Push(new EmgSample(0,0,float.NaN,0));
            Assert.That(session.Stage,Is.EqualTo(CalibrationStage.Failed));
        }
        [Test]
        public void InsufficientActivationNeverCompletesCalibration()
        {
            Feed(3.1,0,0); for(int i=0;i<4;i++) Pulse(.03f,0);
            Assert.That(session.Stage,Is.EqualTo(CalibrationStage.Flex)); Assert.That(session.Result,Is.Null);
        }
        [Test]
        public void ContinuousActivationTimesOutInsteadOfRewardingEffort()
        {
            Feed(3.1,0,0); Feed(.1,0,0); Feed(5.5,.8f,0);
            Assert.That(session.Stage,Is.EqualTo(CalibrationStage.Failed));
        }
    }
}
