using Biopulse.Gameplay;
using UnityEngine;

namespace Biopulse.Neon
{
    public enum NeonRooftopKind { Rest, Jump, Laser, Grab }
    // Built with the same pieces/art as the authored rooftops. Templates stay inactive in the scene.
    public sealed class NeonRooftopChunk : MonoBehaviour
    {
        public float Length;
        public int MinimumTier;
        public NeonRooftopKind Kind;
        public NeonTimingZone[] Zones;
        public GrabTarget[] Targets;
        public NeonCoinField Coins;
        public int Sequence { get; private set; }
        public float EndX => transform.position.x + Length;

        public void Place(float x, int sequence)
        {
            Sequence = sequence;
            float delta = x - transform.position.x;
            transform.position = new Vector3(x, 0, 0);
            for (int i = 0; i < Zones.Length; i++)
            {
                Zones[i].TargetX += delta;
                Zones[i].Id = $"endless/{sequence}/{i}";
            }
            for (int i = 0; i < Targets.Length; i++)
                Targets[i].StableId = $"endless/{sequence}/hook/{i}";
        }

        public void ShiftOrigin(float distance)
        {
            transform.position -= Vector3.right * distance;
            foreach (var zone in Zones) zone.TargetX -= distance;
        }
    }
}
