using Biopulse.Gameplay;
using UnityEngine;

namespace Biopulse.Neon
{
    // Shared types for level pieces. NOTE: every MonoBehaviour lives in its own file named after the class,
    // otherwise Unity cannot serialize it into scenes.
    internal static class RunnerLookup
    {
        public static RunnerMotor2D From(Collider2D other) =>
            other != null && other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<RunnerMotor2D>() : null;
    }

    public enum NeonDeath { Fall, Laser, Toxic, Mopped, Shock }

    public enum NeonChallenge { LowJump, MidJump, HighJump, Gap, Grab, Lock, Precision }
}
