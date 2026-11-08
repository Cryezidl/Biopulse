using System.Collections.Generic;
using Biopulse.Gameplay;
using UnityEngine;

namespace Biopulse.Neon
{
    // Developer/demo autopilot (F2). It "plays" by moving the emulator channels — the same path a finger
    // or a real EMG source takes — choosing a jump amplitude per obstacle. Used to prove levels are beatable
    // and to record the screencast's proportionality demo. Not part of the player-facing control scheme.
    public sealed class NeonAutopilot : MonoBehaviour
    {
        public NeonLevelSession Session;
        public bool Active;
        public float Gravity = 29.43f;
        public float AmplitudeBoost = 1.08f;   // pipeline smoothing captures slightly less than the target
        private readonly List<NeonTimingZone> zones = new List<NeonTimingZone>();
        private readonly HashSet<NeonTimingZone> handled = new HashSet<NeonTimingZone>();
        private GrabTarget[] targets = new GrabTarget[0];
        private float flex, extend, pulseUntil, grabbedFor, restUntil;
        private int lastDeaths;

        public string LastAction { get; private set; } = "";

        // Unattended verification: Player.exe -autopilot [-autopilotLog path] plays every level and writes a report.
        public static bool CommandLine => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autopilot") >= 0;
        private float reportedAt = -1;

        private void Awake()
        {
            if (!CommandLine) return;
            Active = true; Application.runInBackground = true;
            if (Session != null && Session.Input != null) Session.Input.AllowUnfocusedScriptedInput = true;
        }

        private void LateUpdate()
        {
            if (!CommandLine || Session == null) return;
            if (Time.time >= nextStatus)
            {
                nextStatus = Time.time + 3;
                var r = Session.Runner; var p = Session.Input.Pipeline;
                Write($"  t={Time.time:F0} state={Session.State} x={r.Body.position.x:F1} y={r.Body.position.y:F1} motion={r.State} " +
                      $"pipe={p?.State} resume={p?.ResumeRequired} last={LastAction} deaths={Session.Deaths}");
                string shots = ArgValue("-autopilotShots");
                if (shots != null) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, $"L{Session.LevelNumber}_{shot++:00}.png"));
            }
            // Swing snapshots: mid-swing and mid-fling, for checking the grapple feel.
            string swingShots = ArgValue("-autopilotShots");
            if (swingShots != null)
            {
                bool g = Session.Runner.IsGrabbed;
                if (g && !shotGrabbed) swingShotAt = Time.time + .3f;
                if (!g && shotGrabbed) swingShotAt = Time.time + .35f;
                shotGrabbed = g;
                if (swingShotAt > 0 && Time.time >= swingShotAt)
                {
                    swingShotAt = -1;
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(swingShots, $"L{Session.LevelNumber}_swing{swingShot++:00}.png"));
                }
            }
            bool done = Session.State == NeonSessionState.Results;
            bool timeout = Session.ElapsedSeconds > 150;
            if ((done || timeout) && reportedAt < 0)
            {
                reportedAt = Time.time;
                string line = $"level={Session.LevelNumber} finished={done} time={Session.ElapsedSeconds:F1}s active={Session.ActiveSeconds:F1}s " +
                    $"timing={Session.TimingSeconds:F1}s share={Session.TimingShare:P0} score={Session.Score} deaths={Session.Deaths} [{string.Join(", ", Session.DeathLog)}]";
                Write(line);
                if (!done || string.IsNullOrEmpty(Session.NextScene) || !Application.CanStreamedLevelBeLoaded(Session.NextScene) ||
                    Session.NextScene == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
                    Application.Quit();
            }
            // Advance from the results screen with a single Р, like a player.
            if (done && reportedAt >= 0 && Time.time - reportedAt > 1 && Time.time >= pulseUntil) Pulse(0, .5f, .12f, "next");
        }

        private float nextStatus, swingShotAt = -1; private int shot, swingShot; private bool shotGrabbed;
        private static void Write(string line)
        {
            Debug.Log("AUTOPILOT " + line);
            string path = ArgValue("-autopilotLog") ?? System.IO.Path.Combine(Application.persistentDataPath, "autopilot.txt");
            System.IO.File.AppendAllText(path, line + System.Environment.NewLine);
        }

        private static string ArgValue(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private void Start()
        {
            zones.AddRange(FindObjectsByType<NeonTimingZone>(FindObjectsSortMode.None));
            targets = FindObjectsByType<GrabTarget>(FindObjectsSortMode.None);
            zones.Sort((a, b) => a.TargetX.CompareTo(b.TargetX));
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame) Active = !Active;
            if (Session == null || Session.Input == null) return;
            var input = Session.Input;
            if (!Active) { if (input.ScriptedFlex >= 0 || input.ScriptedExtend >= 0) input.ScriptedFlex = input.ScriptedExtend = -1; return; }

            if (Session.Deaths != lastDeaths) { lastDeaths = Session.Deaths; handled.Clear(); }
            if (Time.time >= pulseUntil) { flex = extend = 0; if (Time.time >= restUntil) Decide(); }
            input.ScriptedFlex = flex; input.ScriptedExtend = extend;
        }

        private void Pulse(float f, float e, float seconds, string what)
        {
            flex = f; extend = e; pulseUntil = Time.time + seconds; restUntil = pulseUntil + .08f; LastAction = what;
        }

        private void Decide()
        {
            if (Session.State == NeonSessionState.Results || Session.State == NeonSessionState.Dying) return;
            var pipeline = Session.Input.Pipeline;
            // Technical pause: behave like a player — rest, then one short Р to continue.
            if (pipeline != null && pipeline.ResumeRequired)
            { if (!pipeline.WaitingForRest && pipeline.Frame.IsValid) Pulse(0, .5f, .12f, "resume"); return; }
            var runner = Session.Runner;
            if (runner == null || runner.IsSuspended) return;
            float x = runner.Body.position.x;
            float speed = runner.RunSpeed;

            if (runner.IsAtLock && Session.Locks.Current != null)
            {
                if (Session.Locks.Current.Challenge.State == LockState.Ready) Pulse(.7f, .7f, .75f, "lock C+P");
                return;
            }
            if (runner.IsGrabbed)
            {
                grabbedFor += Time.deltaTime;
                var target = Session.Grab.Target;
                Vector2 v = runner.Body.linearVelocity;
                if (target != null && ((x > target.transform.position.x + .3f && v.x > 2f && v.y > .5f) || grabbedFor > 3))
                    Pulse(0, .5f, .12f, "release");
                return;
            }
            grabbedFor = 0;

            NeonTimingZone next = null;
            foreach (var z in zones)
                if (!handled.Contains(z) && (z.TargetX > x - .5f || (z.Challenge == NeonChallenge.Grab && z.RightEdge - 1 > x)))
                { next = z; break; }
            if (next == null) return;

            switch (next.Challenge)
            {
                case NeonChallenge.LowJump: TryJump(next, x, speed, .36f); break;
                case NeonChallenge.MidJump: TryJump(next, x, speed, .64f); break;
                case NeonChallenge.HighJump: TryJump(next, x, speed, .97f); break;
                case NeonChallenge.Gap:
                {
                    float width = next.RightEdge - .6f - next.TargetX;
                    float a = width > 3.2f ? .85f : .5f;
                    if (runner.IsGrounded && x >= next.TargetX - .45f - speed * .05f)
                    { Pulse(0, Mathf.Clamp01(a * AmplitudeBoost), .1f, $"gap jump {a:0.00}"); handled.Add(next); }
                    break;
                }
                case NeonChallenge.Grab:
                {
                    if (x > next.RightEdge - 1f && runner.IsGrounded) { handled.Add(next); break; }
                    // Reach for the nearest bar ahead once it is comfortably inside maximum reach.
                    foreach (var t in targets)
                    {
                        if (t == null) continue;
                        Vector2 d = (Vector2)t.transform.position - runner.Body.position;
                        if (d.x > .3f && d.magnitude < 4.9f) { Pulse(.98f, 0, .22f, "grab"); break; }
                    }
                    break;
                }
                case NeonChallenge.Lock:
                    if (x > next.TargetX + .5f) handled.Add(next);
                    break;
            }
        }

        private void TryJump(NeonTimingZone zone, float x, float speed, float amplitude)
        {
            var runner = Session.Runner;
            float v = Mathf.Lerp(6, 14, amplitude);
            float takeoff = zone.TargetX - speed * (v / Gravity) * .9f - speed * .05f;
            if (!runner.IsGrounded || x < takeoff) return;
            Pulse(0, Mathf.Clamp01(amplitude * AmplitudeBoost), .1f, $"jump {amplitude:0.00}");
            handled.Add(zone);
        }
    }
}
