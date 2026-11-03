using Biopulse.Emg;
using Biopulse.Gameplay;
using NUnit.Framework;

namespace Biopulse.Tests
{
    public sealed class LockChallengeTests
    {
        [Test]
        public void LeadingChannelDoesNotInvalidateReadyState(
            [Values(true, false)] bool flexFirst, [Values(.01, .05, .2, .5)] double gap)
        {
            var c = new LockChallenge(); c.Advance(EmgIntentKind.Rest, 0);
            var leading = flexFirst ? EmgIntentKind.Flex : EmgIntentKind.Extend;
            c.Advance(leading, .1); c.Advance(leading, .1 + gap);
            Assert.That(c.State, Is.EqualTo(LockState.Ready)); Assert.That(c.Progress, Is.Zero);
            c.Advance(EmgIntentKind.Coactivate, .11 + gap);
            c.Advance(EmgIntentKind.Coactivate, .51 + gap);
            Assert.That(c.State, Is.EqualTo(LockState.Holding));
            c.Advance(EmgIntentKind.Coactivate, .61 + gap);
            Assert.That(c.State, Is.EqualTo(LockState.Opened));
        }
        [TestCase(EmgIntentKind.Flex)]
        [TestCase(EmgIntentKind.Extend)]
        public void LeadingChannelOnEntryDoesNotBypassInitialRest(EmgIntentKind leading)
        {
            var c = new LockChallenge(); c.Advance(leading, 0);
            c.Advance(EmgIntentKind.Coactivate, 1); c.Advance(EmgIntentKind.Coactivate, 2);
            Assert.That(c.State, Is.EqualTo(LockState.WaitingForRest));
        }
        [Test]
        public void HeldCombinationOnEntryNeedsRestBeforeOpening()
        {
            var c = new LockChallenge(); c.Advance(EmgIntentKind.Coactivate, 0); c.Advance(EmgIntentKind.Coactivate, 2);
            Assert.That(c.State, Is.EqualTo(LockState.WaitingForRest));
            c.Advance(EmgIntentKind.Rest, 3); c.Advance(EmgIntentKind.Coactivate, 3.1);
            c.Advance(EmgIntentKind.Coactivate, 3.599); Assert.That(c.State, Is.EqualTo(LockState.Holding));
            c.Advance(EmgIntentKind.Coactivate, 3.6); Assert.That(c.State, Is.EqualTo(LockState.Opened));
        }
        [Test]
        public void InterruptionRequiresAnotherRestAndClearsProgress()
        {
            var c = new LockChallenge(); c.Advance(EmgIntentKind.Rest, 0); c.Advance(EmgIntentKind.Coactivate, .1);
            c.Advance(EmgIntentKind.Coactivate, .4); Assert.That(c.Progress, Is.GreaterThan(.5));
            c.Advance(EmgIntentKind.Extend, .45); c.Advance(EmgIntentKind.Coactivate, 2);
            Assert.That(c.Progress, Is.Zero); Assert.That(c.State, Is.EqualTo(LockState.WaitingForRest));
            c.Advance(EmgIntentKind.Rest, 3); c.Advance(EmgIntentKind.Coactivate, 4); c.Advance(EmgIntentKind.Coactivate, 4.5);
            Assert.That(c.State, Is.EqualTo(LockState.Opened));
        }
        [Test]
        public void PauseFreezesProgressAndRecoveryCannotCountOfflineTime()
        {
            var c = new LockChallenge(); c.Advance(EmgIntentKind.Rest, 0); c.Advance(EmgIntentKind.Coactivate, .1);
            c.Advance(EmgIntentKind.Coactivate, .3); float before = c.Progress; c.Pause();
            Assert.That(c.Progress, Is.EqualTo(before)); c.Advance(EmgIntentKind.Coactivate, 10);
            Assert.That(c.State, Is.EqualTo(LockState.WaitingForRest)); Assert.That(c.Progress, Is.Zero);
        }
        [Test]
        public void RepeatedRenderingOfSameSampleDoesNotAdvanceTimer()
        {
            var c = new LockChallenge(); c.Advance(EmgIntentKind.Rest, 0);
            for (int i = 0; i < 100; i++) c.Advance(EmgIntentKind.Coactivate, .1);
            Assert.That(c.Progress, Is.Zero); c.Advance(EmgIntentKind.Coactivate, .6);
            Assert.That(c.State, Is.EqualTo(LockState.Opened));
        }
        [Test]
        public void ResetRelocksAnOpenedChallenge()
        {
            var c = new LockChallenge(); c.Advance(EmgIntentKind.Rest, 0); c.Advance(EmgIntentKind.Coactivate, 1);
            c.Advance(EmgIntentKind.Coactivate, 2); c.Reset();
            Assert.That(c.State, Is.EqualTo(LockState.WaitingForRest)); Assert.That(c.Progress, Is.Zero);
        }
    }
}
