using Biopulse.Emg;
using Biopulse.Gameplay;
using NUnit.Framework;

namespace Biopulse.Tests
{
    public sealed class TutorialSessionTests
    {
        private TutorialSession session;
        private double time;
        private void Observe(EmgIntentKind input = EmgIntentKind.Rest, int jumps = 0, float amplitude = 0,
            bool grabbed = false, string target = null, bool open = false, bool atLock = false)
        {
            time += .1;
            session.Advance(new TutorialObservation(time,input,jumps,amplitude,true,grabbed,target,open,atLock));
        }
        [SetUp]
        public void Setup() { session = new TutorialSession(); time = 0; for(int i=0;i<7;i++) Observe(); }
        [Test]
        public void SuccessfulRouteRequiresRealActionsInTheRightContext()
        {
            Assert.That(session.Step,Is.EqualTo(TutorialStep.WeakJump));
            Observe(jumps:1,amplitude:.4f); Observe(jumps:2,amplitude:.8f);
            Assert.That(session.Step,Is.EqualTo(TutorialStep.NearGrab));
            Observe(jumps:2,grabbed:true,target:"tutorial-near");
            for(int i=0;i<10;i++) Observe(jumps:2,grabbed:true,target:"tutorial-near");
            Assert.That(session.Step,Is.EqualTo(TutorialStep.Release));
            Observe(jumps:2); Assert.That(session.Step,Is.EqualTo(TutorialStep.FarGrab));
            Observe(jumps:0,grabbed:true,target:"tutorial-far"); Observe(jumps:0);
            Assert.That(session.Step,Is.EqualTo(TutorialStep.Lock));
            Observe(open:true,atLock:true); Assert.That(session.Step,Is.EqualTo(TutorialStep.Lock));
            Observe(open:true); Assert.That(session.Step,Is.EqualTo(TutorialStep.Complete));
        }
        [Test]
        public void EqualJumpAmplitudesDoNotPassProportionalityLesson()
        {
            Observe(jumps:1,amplitude:.4f); Observe(jumps:2,amplitude:.45f);
            Assert.That(session.Step,Is.EqualTo(TutorialStep.StrongJump));
        }
        [Test]
        public void RestLessonCannotBePassedByHoldingFlex()
        {
            Observe(jumps:1,amplitude:.4f); Observe(jumps:2,amplitude:.8f);
            Observe(jumps:2,grabbed:true,target:"tutorial-near");
            for(int i=0;i<15;i++) Observe(EmgIntentKind.Flex,2,grabbed:true,target:"tutorial-near");
            Assert.That(session.Step,Is.EqualTo(TutorialStep.RestInGrab));
        }
        [Test]
        public void TimeWithoutSamplesCannotCompleteRestLesson()
        {
            Observe(jumps:1,amplitude:.4f); Observe(jumps:2,amplitude:.8f);
            Observe(jumps:2,grabbed:true,target:"tutorial-near"); Observe(jumps:2,grabbed:true,target:"tutorial-near");
            time += 10; Observe(jumps:2,grabbed:true,target:"tutorial-near");
            Assert.That(session.Step,Is.EqualTo(TutorialStep.RestInGrab));
        }
    }
}
