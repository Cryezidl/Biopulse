using Biopulse.Emg;
using UnityEditor;
using UnityEngine;

namespace Biopulse.Editor
{
    // An editor bench, not a gameplay scene. Runs without Play Mode or changing the current scene.
    public sealed class EmgDiagnosticsWindow : EditorWindow
    {
        private EmulatorEmgSource source;
        private EmgPipeline pipeline;
        private float flexAmplitude = .6f, extendAmplitude = .6f;
        private bool flexHeld, extendHeld, bothHeld, keyFlex, keyExtend;
        private bool disconnect, invalid;
        private EmgIntent lastIntent;
        private int count;
        private double lastUpdate;

        [MenuItem("Biopulse/Input/Open EMG Diagnostics")]
        public static void Open()
        {
            var window = GetWindow<EmgDiagnosticsWindow>("Biopulse EMG");
            window.minSize = new Vector2(640, 480);
        }
        private void OnEnable()
        {
            source = new EmulatorEmgSource(); source.Start();
            pipeline = new EmgPipeline(source.Info, new EmgSettings(), new EmgCalibration(source.Info.Id));
            pipeline.IntentRecognized += intent => { lastIntent = intent; count++; };
            lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update += Step;
        }
        private void OnDisable() { EditorApplication.update -= Step; source?.Dispose(); }
        private void OnLostFocus()
        {
            flexHeld = extendHeld = bothHeld = keyFlex = keyExtend = false;
            pipeline?.Suspend("Window lost focus; release both and resume");
        }
        private void Step()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - lastUpdate < .005) return;
            lastUpdate = now;
            if (!disconnect) source.Publish(now, invalid ? float.NaN : (flexHeld || bothHeld || keyFlex ? flexAmplitude : 0),
                extendHeld || bothHeld || keyExtend ? extendAmplitude : 0);
            while (source.TryRead(out var sample)) pipeline.Push(sample, now);
            pipeline.Tick(now, source.Status); Repaint();
        }
        private void OnGUI()
        {
            if (pipeline == null) return;
            Event e = Event.current;
            if (e.type == EventType.KeyDown || e.type == EventType.KeyUp)
            {
                bool down = e.type == EventType.KeyDown;
                if (e.keyCode == KeyCode.F) { keyFlex = down; e.Use(); }
                if (e.keyCode == KeyCode.R) { keyExtend = down; e.Use(); }
            }
            EditorGUILayout.LabelField("Two-channel EMG input bench", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Hold F (С), R (Р), or the buttons. F+R / Both tests coactivation. Release BOTH to rearm. This window does not move a character.", MessageType.Info);
            flexAmplitude = EditorGUILayout.Slider("С / Flex amplitude", flexAmplitude, 0, 1);
            extendAmplitude = EditorGUILayout.Slider("Р / Extend amplitude", extendAmplitude, 0, 1);
            GUILayout.BeginHorizontal();
            flexHeld = GUILayout.RepeatButton("Hold С / F", GUILayout.Height(55));
            extendHeld = GUILayout.RepeatButton("Hold Р / R", GUILayout.Height(55));
            bothHeld = GUILayout.RepeatButton("Hold Both", GUILayout.Height(55));
            GUILayout.EndHorizontal();
            if (e.rawType == EventType.MouseUp) flexHeld = extendHeld = bothHeld = false;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Source", source.Info.Id + " (synthetic envelope)");
            EditorGUILayout.LabelField("Raw С / Р", $"{pipeline.Raw.Flexor:F3} / {pipeline.Raw.Extensor:F3}");
            EditorGUILayout.LabelField("Processed С / Р", $"{pipeline.Frame.Flexor:F3} / {pipeline.Frame.Extensor:F3}");
            EditorGUILayout.LabelField("State", pipeline.State.ToString());
            EditorGUILayout.LabelField("Last intention", $"#{lastIntent.Id}: {lastIntent.Kind}   С {lastIntent.Flexor:F2} / Р {lastIntent.Extensor:F2}");
            EditorGUILayout.LabelField("Intentions / rejected / missing", $"{count} / {pipeline.RejectedSamples} / {pipeline.MissingSamples}");
            EditorGUILayout.LabelField("Processing time (not screen latency)", $"{(lastIntent.RecognizedTimeSeconds - lastIntent.InputTimeSeconds) * 1000:F1} ms");
            EditorGUILayout.HelpBox(pipeline.Fault ?? "Ready. Default emulator profile: baseline 0, reference 1.", pipeline.Fault == null ? MessageType.None : MessageType.Warning);
            disconnect = EditorGUILayout.Toggle("Simulate missing data", disconnect);
            invalid = EditorGUILayout.Toggle("Simulate NaN", invalid);
            using (new EditorGUI.DisabledScope(!pipeline.ResumeRequired || pipeline.WaitingForRest))
                if (GUILayout.Button("Resume after fault")) pipeline.Resume();
        }
    }
}
