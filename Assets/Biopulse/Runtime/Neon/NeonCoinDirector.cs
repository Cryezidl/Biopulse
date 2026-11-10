using UnityEngine;

namespace Biopulse.Neon
{
    [DefaultExecutionOrder(-35)]
    public sealed class NeonCoinDirector : MonoBehaviour
    {
        public NeonCoinField LevelField;
        public int Seed;
        public float MinimumGap = 16, MaximumGap = 28;
        public float NextGroupX { get; private set; }
        private System.Random random;

        private void EnsureRandom()
        {
            if (random != null) return;
            random = new System.Random(Seed == 0 ? System.Environment.TickCount : Seed);
            NextGroupX = 8 + (float)random.NextDouble() * 6;
        }

        private void Start()
        {
            EnsureRandom();
            if (LevelField != null) Populate(LevelField);
        }

        public void Populate(NeonCoinField field)
        {
            EnsureRandom();
            field.BeginPopulation();
            if (field.CoinPrefab == null) return;
            var routes = field.Routes;
            for (int i = 0; i < routes.Length; i++)
            {
                if (routes[i].Points == null || routes[i].Points.Length == 0) continue;
                float firstX = field.transform.TransformPoint(routes[i].Points[0]).x;
                if (firstX < NextGroupX) continue;
                // Nearby alternatives make the reward alternate between floor, jump arc and grapple.
                int selected = i, choices = 1;
                for (int j = i + 1; j < routes.Length; j++)
                {
                    if (routes[j].Points == null || routes[j].Points.Length == 0) continue;
                    float x = field.transform.TransformPoint(routes[j].Points[0]).x;
                    if (x > firstX + 5) break;
                    if (random.Next(++choices) == 0) selected = j;
                }
                var points = routes[selected].Points;
                int count = random.Next(1, Mathf.Min(3, points.Length) + 1);
                int offset = random.Next(points.Length - count + 1);
                for (int k = 0; k < count; k++) field.Spawn(points[offset + k]);
                float end = field.transform.TransformPoint(points[offset + count - 1]).x;
                NextGroupX = end + Mathf.Lerp(MinimumGap, MaximumGap, (float)random.NextDouble());
                i = selected;
            }
        }

        public void ShiftOrigin(float amount) { NextGroupX -= amount; }
    }
}
