using System.Collections.Generic;
using UnityEngine;

namespace Biopulse.Neon
{
    [DefaultExecutionOrder(-30)]
    public sealed class NeonEndlessRooftops : MonoBehaviour
    {
        public NeonLevelSession Session;
        public NeonRooftopChunk[] Templates;
        public float Ahead = 110, Behind = 55;
        public int Seed;
        public int Revision { get; private set; }
        public int ActiveChunkCount => active.Count;
        public int CreatedChunkCount { get; private set; }
        public float GeneratedThrough => nextX;

        // Flat joins preserve landing/reaction room; vertical mechanics appear from the first cycle.
        public readonly struct Pattern
        {
            public readonly string Layout;
            public readonly int Tier;
            public readonly NeonRooftopKind Kind;
            public Pattern(NeonRooftopKind kind, int tier, string layout) { Kind = kind; Tier = tier; Layout = layout; }
        }

        public static readonly Pattern[] Patterns =
        {
            new Pattern(NeonRooftopKind.Rest, 0, "run 8"),
            new Pattern(NeonRooftopKind.Jump, 0, "run 2 low low mid run 3"),
            new Pattern(NeonRooftopKind.Jump, 0, "run 2 mid low run 3"),
            new Pattern(NeonRooftopKind.Jump, 0, "run 2 pit 2.6 low run 3"),
            new Pattern(NeonRooftopKind.Jump, 0, "run 2 low mid low run 3"),
            new Pattern(NeonRooftopKind.Jump, 1, "run 2 high mid run 3"),
            new Pattern(NeonRooftopKind.Jump, 0, "run 2 pit 3 mid run 3"),
            new Pattern(NeonRooftopKind.Jump, 1, "run 2 mid high low run 3"),
            new Pattern(NeonRooftopKind.Jump, 2, "run 2 high pit 3.6 low run 3"),
            new Pattern(NeonRooftopKind.Laser, 0, "run 2 laser low run 3"),
            new Pattern(NeonRooftopKind.Laser, 0, "run 2 low laser run 3"),
            new Pattern(NeonRooftopKind.Laser, 0, "run 2 ceiling run 3"),
            new Pattern(NeonRooftopKind.Laser, 0, "run 2 ceiling3 run 3"),
            new Pattern(NeonRooftopKind.Laser, 0, "run 2 laser-mid low run 3"),
            new Pattern(NeonRooftopKind.Laser, 0, "run 2 ceiling-mixed run 3"),
            new Pattern(NeonRooftopKind.Laser, 1, "run 2 ceiling-mid run 3"),
            new Pattern(NeonRooftopKind.Laser, 1, "run 2 laser-mid laser run 3"),
            new Pattern(NeonRooftopKind.Laser, 1, "run 2 laser low laser-mid run 3"),
            new Pattern(NeonRooftopKind.Laser, 2, "run 2 laser-high mid run 3"),
            new Pattern(NeonRooftopKind.Laser, 1, "run 2 laser pit 3 laser run 3"),
            new Pattern(NeonRooftopKind.Laser, 1, "run 2 ceiling run 4 grip 9 run 6"),
            new Pattern(NeonRooftopKind.Laser, 2, "run 2 laser-mid run 3 grip 14 run 7"),
            new Pattern(NeonRooftopKind.Grab, 0, "run 2 grip 6 run 6"),
            new Pattern(NeonRooftopKind.Grab, 0, "run 2 grip 9 run 7"),
            new Pattern(NeonRooftopKind.Grab, 0, "run 2 grip 14 run 7"),
            new Pattern(NeonRooftopKind.Grab, 0, "run 2 grip-wave 14 run 7"),
            new Pattern(NeonRooftopKind.Grab, 0, "run 2 overhead run 4"),
            new Pattern(NeonRooftopKind.Grab, 1, "run 2 grip 10 run 4 low run 4"),
            new Pattern(NeonRooftopKind.Grab, 0, "run 2 low mid run 3 grip 14 run 7"),
            new Pattern(NeonRooftopKind.Grab, 1, "run 2 grip 14 run 7 laser run 3"),
            new Pattern(NeonRooftopKind.Grab, 1, "run 2 grip-wave 15 run 7 mid run 4"),
            new Pattern(NeonRooftopKind.Grab, 1, "run 2 grip 6 run 4 grip 10 run 7"),
            new Pattern(NeonRooftopKind.Grab, 2, "run 2 high run 3 grip 15 run 7"),
            new Pattern(NeonRooftopKind.Grab, 1, "run 2 overhead run 3 laser-mid run 3"),
            new Pattern(NeonRooftopKind.Grab, 2, "run 2 grip 17 run 7"),
            new Pattern(NeonRooftopKind.Grab, 2, "run 2 grip-wave 14 run 6 grip 9 run 7"),
            new Pattern(NeonRooftopKind.Grab, 2, "run 2 grip 9 run 7 ceiling3 run 3"),
            new Pattern(NeonRooftopKind.Laser, 2, "run 2 ceiling-mid run 4 grip-wave 14 run 7")
        };
        private const int MaxCopiesPerTemplate = 4;
        private readonly List<NeonRooftopChunk> active = new List<NeonRooftopChunk>();
        private Stack<NeonRooftopChunk>[] pools;
        private int[] created;
        private int[] lastUsed;
        private System.Random random;
        private float nextX;
        private int previous = -1, sequence;
        private readonly NeonRooftopKind[] cycle = new NeonRooftopKind[5];

        public static int TierAt(float distance) => distance < 180 ? 0 : distance < 500 ? 1 : 2;

        private void Start()
        {
            if (Session == null || !Session.IsEndless || Templates == null || Templates.Length == 0)
            { enabled = false; return; }
            random = new System.Random(Seed == 0 ? System.Environment.TickCount : Seed);
            pools = new Stack<NeonRooftopChunk>[Templates.Length];
            created = new int[Templates.Length];
            lastUsed = new int[Templates.Length];
            for (int i = 0; i < lastUsed.Length; i++) lastUsed[i] = -100;
            for (int i = 0; i < pools.Length; i++) pools[i] = new Stack<NeonRooftopChunk>();
            // One short opening runway, then five challenges per recovery roof.
            Append(0);
            Maintain();
        }

        private void Update()
        {
            if (Session.State != NeonSessionState.Playing) return;
            Maintain();
            // Rebase on the ground so interpolation/physics joints never jump during a swing.
            if (Session.Runner.Body.position.x > 2000 && Session.Runner.IsGrounded && !Session.Runner.IsGrabbed)
                Rebase(1500);
        }

        private void Maintain()
        {
            float x = Session.Runner.Body.position.x;
            while (active.Count > 0 && active[0].EndX < x - Behind)
            {
                var chunk = active[0]; active.RemoveAt(0);
                chunk.gameObject.SetActive(false);
                pools[chunkTemplate[chunk]].Push(chunk);
                Revision++;
            }
            while (nextX < x + Ahead) Append(Choose());
        }

        private readonly Dictionary<NeonRooftopChunk, int> chunkTemplate = new Dictionary<NeonRooftopChunk, int>();

        private int Choose()
        {
            int phase = sequence % 6;
            if (phase == 0 && Available(0)) return 0;
            if (phase == 1)
            {
                cycle[0] = NeonRooftopKind.Laser; cycle[1] = NeonRooftopKind.Grab;
                cycle[2] = NeonRooftopKind.Jump; cycle[3] = NeonRooftopKind.Laser; cycle[4] = NeonRooftopKind.Grab;
                // Keep the first laser/grab readable, then vary the order of every following cycle.
                for (int i = 4; i > (sequence == 1 ? 2 : 0); i--)
                {
                    int j = random.Next(sequence == 1 ? 2 : 0, i + 1);
                    var swap = cycle[i]; cycle[i] = cycle[j]; cycle[j] = swap;
                }
            }
            var kind = cycle[phase - 1];
            int tier = TierAt(Session.Distance);
            int selected = -1, count = 0, oldest = int.MaxValue;
            // Randomize among the least recently used eligible patterns. A new variant is shown
            // before repeating an old one, even while the runner is still in the opening tier.
            for (int i = 1; i < Templates.Length; i++)
            {
                if (i == previous || Templates[i].Kind != kind || Templates[i].MinimumTier > tier || !Available(i)) continue;
                if (lastUsed[i] < oldest) { oldest = lastUsed[i]; count = 0; selected = -1; }
                if (lastUsed[i] == oldest && random.Next(++count) == 0) selected = i;
            }
            if (selected >= 0) return selected;
            for (int i = 0; i < Templates.Length; i++) if (Available(i)) return i;
            throw new System.InvalidOperationException("Rooftop pool exhausted; reduce the generation window.");
        }

        private bool Available(int i) => pools[i].Count > 0 || created[i] < MaxCopiesPerTemplate;

        private void Append(int index)
        {
            NeonRooftopChunk chunk;
            if (pools[index].Count > 0) chunk = pools[index].Pop();
            else
            {
                chunk = Instantiate(Templates[index], transform);
                chunkTemplate.Add(chunk, index);
                created[index]++; CreatedChunkCount++;
            }
            chunk.Place(nextX, sequence++);
            if (chunk.Coins != null) Session.GetComponent<NeonCoinDirector>()?.Populate(chunk.Coins);
            nextX += chunk.Length;
            chunk.gameObject.SetActive(true);
            active.Add(chunk);
            previous = index;
            lastUsed[index] = sequence - 1;
            Revision++;
        }

        private void Rebase(float amount)
        {
            Session.AddDistanceOffset(amount);
            Session.GetComponent<NeonCoinDirector>()?.ShiftOrigin(amount);
            foreach (var chunk in active) chunk.ShiftOrigin(amount);
            nextX -= amount;
            var body = Session.Runner.Body;
            body.position -= Vector2.right * amount;
            Session.Runner.transform.position = new Vector3(body.position.x, body.position.y, 0);
            Session.Cam.ShiftOrigin(amount);
            if (Session.Chaser != null) Session.Chaser.transform.position -= Vector3.right * amount;
            foreach (var layer in FindObjectsByType<NeonParallax>(FindObjectsSortMode.None)) layer.ShiftOrigin(amount);
            Physics2D.SyncTransforms();
            Revision++;
        }
    }
}
