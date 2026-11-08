using System;
using Biopulse.Emg;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Biopulse.Input
{
    // Composition root for the next stage. Gameplay subscribes only to IntentRecognized.
    public sealed class EmgInputDriver : MonoBehaviour
    {
        [SerializeField] private EmgSettings settings = new EmgSettings();
        [SerializeField, Range(0, 1)] private float flexAmplitude = .6f;
        [SerializeField, Range(0, 1)] private float extendAmplitude = .6f;
        [SerializeField] private bool showDiagnostics = true;
        [SerializeField] private bool loadSavedCalibrationOnStart;
        public bool LoadSavedCalibrationOnStart { get => loadSavedCalibrationOnStart; set => loadSavedCalibrationOnStart = value; }
        private IEmgSource source;
        private EmulatorEmgSource emulator;
        private EmgPipeline pipeline;
        private bool focused = true, waitForRelease;
        private bool applicationPaused;
        private bool showTouchControls;
        private bool awaitingFirstSample;
        private double pipelineResetAt;
        private EmgIntent lastIntent;
        private int intentCount;
        private float touchFlex, touchExtend;
        private bool flexHeld, extendHeld;
        private int flexTouchId = -1, extendTouchId = -1;

        public EmgPipeline Pipeline => pipeline;
        public IEmgSource Source => source;
        public EmgCalibration CurrentCalibration { get; private set; }
        public string SettingsSignature => JsonUtility.ToJson(settings);
        public bool ShowTouchControls { get => showTouchControls; set => showTouchControls = value; }
        // Opt-in for unattended synthetic diagnostics. Never bypasses focus handling in a player build.
        public bool AllowBackgroundReplayInEditor { get; set; }
        // Unattended verification runs (autopilot build) may keep scripted input flowing without focus. Never set for players.
        public bool AllowUnfocusedScriptedInput { get; set; }
        private bool Scripted => ScriptedFlex >= 0 || ScriptedExtend >= 0;
        private bool BackgroundReplay => (Application.isEditor && AllowBackgroundReplayInEditor && source is ReplayEmgSource) ||
            ((Application.isEditor || AllowUnfocusedScriptedInput) && Scripted);
        public float FlexAmplitude { get => flexAmplitude; set => flexAmplitude = Mathf.Clamp01(value); }
        public float ExtendAmplitude { get => extendAmplitude; set => extendAmplitude = Mathf.Clamp01(value); }
        public bool ShowDiagnostics { get => showDiagnostics; set => showDiagnostics = value; }
        public event Action<EmgIntent> IntentRecognized;
        public event Action<EmgSample> SampleReceived;

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            SetSource(new EmulatorEmgSource(), new EmgCalibration(EmulatorEmgSource.SourceId));
            if (loadSavedCalibrationOnStart && Biopulse.Calibration.ProfileStore.TryLoad(source.Info, SettingsSignature, out var saved))
                ApplyCalibration(saved.ToCalibration());
        }
        private void OnDisable()
        {
            if (pipeline != null) pipeline.IntentRecognized -= Forward;
            source?.Dispose(); source = null; emulator = null;
            EnhancedTouchSupport.Disable();
            ClearTouches();
        }

        public void SetSource(IEmgSource replacement, EmgCalibration calibration)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            var replacementPipeline = new EmgPipeline(replacement.Info, settings, calibration);
            if (pipeline != null) pipeline.IntentRecognized -= Forward;
            source?.Dispose();
            source = replacement; emulator = replacement as EmulatorEmgSource; pipeline = replacementPipeline;
            CurrentCalibration = calibration;
            awaitingFirstSample = true; pipelineResetAt = Time.realtimeSinceStartupAsDouble;
            pipeline.IntentRecognized += Forward;
            source.Start(); waitForRelease = true; ClearTouches();
        }
        public void ApplyCalibration(EmgCalibration calibration)
        {
            if (source == null) throw new InvalidOperationException("No source selected.");
            var replacement = new EmgPipeline(source.Info, settings, calibration);
            pipeline.IntentRecognized -= Forward;
            for (int i = 0; i < 128 && source.TryRead(out _); i++) { }
            pipeline = replacement; CurrentCalibration = calibration; pipeline.IntentRecognized += Forward;
            awaitingFirstSample = true; pipelineResetAt = Time.realtimeSinceStartupAsDouble;
            waitForRelease = true; ClearTouches();
        }
        private void Forward(EmgIntent intent)
        { lastIntent = intent; intentCount++; IntentRecognized?.Invoke(intent); }

        private void Update()
        {
            if (source == null || (!BackgroundReplay && (!focused || applicationPaused))) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (emulator != null)
            {
                ReadControls();
                if (ScriptedFlex >= 0) { flexHeld = ScriptedFlex > 0; touchFlex = ScriptedFlex; }
                if (ScriptedExtend >= 0) { extendHeld = ScriptedExtend > 0; touchExtend = ScriptedExtend; }
                bool anyHeld = flexHeld || extendHeld;
                if (waitForRelease)
                {
                    if (!anyHeld) waitForRelease = false;
                    emulator.Publish(now, 0, 0);
                }
                else emulator.Publish(now, flexHeld ? (touchFlex >= 0 ? touchFlex : flexAmplitude) : 0,
                    extendHeld ? (touchExtend >= 0 ? touchExtend : extendAmplitude) : 0);
            }
            int budget = 128;
            while (budget-- > 0 && source.TryRead(out var sample))
                if (pipeline.Push(sample, Time.realtimeSinceStartupAsDouble))
                { awaitingFirstSample = false; SampleReceived?.Invoke(sample); }
            double checkedAt = Time.realtimeSinceStartupAsDouble;
            // Applying a profile from SampleReceived may leave the new pipeline empty until the next frame.
            // It is unready during this startup window, but not disconnected or in need of Resume.
            if (!awaitingFirstSample || source.Status != EmgSourceStatus.Streaming || checkedAt - pipelineResetAt > settings.StaleSeconds)
                pipeline.Tick(checkedAt, source.Status);
        }

        // Screen-space (bottom-left origin) rectangle of a channel's touch zone; the HUD draws over it.
        public Rect TouchZone(bool flex) => Zone(flex);
        // Scripted emulator channels (autopilot / demo). Negative = not scripted. Still flows through the
        // whole Source → Pipeline → Intent path, exactly like a finger on the touch zone.
        public float ScriptedFlex { get; set; } = -1;
        public float ScriptedExtend { get; set; } = -1;
        public bool FlexHeld => flexHeld;
        public bool ExtendHeld => extendHeld;

        private Rect Zone(bool flex)
        {
            Rect safe = Screen.safeArea;
            float gap = 16;
            float width = (safe.width - 3 * gap) / 2;
            float height = Mathf.Min(210, safe.height * .27f);
            return new Rect(safe.x + gap + (flex ? 0 : width + gap), safe.y + gap, width, height);
        }
        private void ClearTouches() { flexTouchId = extendTouchId = -1; }
        private void ReadControls()
        {
            flexHeld = Keyboard.current?.fKey.isPressed == true;
            extendHeld = Keyboard.current?.rKey.isPressed == true;
            // Emulator amplitude presets (weak / medium / strong) so proportionality is testable from a keyboard.
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) flexAmplitude = extendAmplitude = .3f;
                if (kb.digit2Key.wasPressedThisFrame) flexAmplitude = extendAmplitude = .6f;
                if (kb.digit3Key.wasPressedThisFrame) flexAmplitude = extendAmplitude = 1f;
            }
            touchFlex = touchExtend = -1;
            bool foundFlex = false, foundExtend = false;
            foreach (var touch in Touch.activeTouches)
            {
                if (!touch.isInProgress) continue;
                // Capture at touch start; sliding out never transfers a held finger to another channel.
                if (touch.began)
                {
                    if (flexTouchId < 0 && Zone(true).Contains(touch.startScreenPosition)) flexTouchId = touch.touchId;
                    else if (extendTouchId < 0 && Zone(false).Contains(touch.startScreenPosition)) extendTouchId = touch.touchId;
                }
                if (touch.touchId == flexTouchId)
                { foundFlex = flexHeld = true; touchFlex = Mathf.InverseLerp(Zone(true).yMin, Zone(true).yMax, touch.screenPosition.y); }
                if (touch.touchId == extendTouchId)
                { foundExtend = extendHeld = true; touchExtend = Mathf.InverseLerp(Zone(false).yMin, Zone(false).yMax, touch.screenPosition.y); }
            }
            if (!foundFlex) flexTouchId = -1;
            if (!foundExtend) extendTouchId = -1;
            // PC test keys with fixed strengths (emulator only; still flows through the full pipeline).
            if (kb != null)
            {
                float kf = -1, ke = -1;
                if (kb.qKey.isPressed) kf = .3f; if (kb.wKey.isPressed) kf = .6f; if (kb.eKey.isPressed) kf = 1f;
                if (kb.aKey.isPressed) ke = .3f; if (kb.sKey.isPressed || kb.spaceKey.isPressed) ke = .6f; if (kb.dKey.isPressed) ke = 1f;
                if (kb.bKey.isPressed) { kf = .7f; ke = .7f; }
                if (kf >= 0) { flexHeld = true; touchFlex = kf; }
                if (ke >= 0) { extendHeld = true; touchExtend = ke; }
            }
            if (Touch.activeTouches.Count == 0 && Mouse.current?.leftButton.isPressed == true)
            {
                var position = Mouse.current.position.ReadValue();
                // Same metaphor as touch: pointer height inside the zone sets amplitude.
                if (Zone(true).Contains(position)) { flexHeld = true; touchFlex = Mathf.InverseLerp(Zone(true).yMin, Zone(true).yMax, position.y); }
                if (Zone(false).Contains(position)) { extendHeld = true; touchExtend = Mathf.InverseLerp(Zone(false).yMin, Zone(false).yMax, position.y); }
            }
        }
        private void OnApplicationFocus(bool value)
        {
            focused = value; waitForRelease = true; ClearTouches();
            if (!value && !BackgroundReplay) pipeline?.Suspend("Application focus lost; release controls and resume");
        }
        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused; waitForRelease = true; ClearTouches();
            // Unity also sends paused=false on startup. Only a real pause invalidates input.
            if (paused && !BackgroundReplay) pipeline?.Suspend("Application paused; release controls and resume");
        }
        private void OnGUI()
        {
            if (pipeline == null) return;
            if (!showDiagnostics)
            {
                if (showTouchControls) { DrawZone(true, flexHeld); DrawZone(false, extendHeld); }
                return;
            }
            var safe = Screen.safeArea;
            GUILayout.BeginArea(new Rect(safe.x + 16, Screen.height - safe.yMax + 16, safe.width - 32, Mathf.Max(140, safe.height * .56f)), GUI.skin.box);
            GUILayout.Label("BIOPULSE — EMG INPUT DIAGNOSTICS (developer UI)");
            GUILayout.Label("F = Flex (С)    R = Extend (Р)    F+R = both. Release both before another gesture.");
            GUILayout.Label("Mouse: hold a zone; keyboard uses amplitudes below. Touch: height in zone sets amplitude.");
            GUILayout.Label($"Source: {source.Info.Id} / {source.Status} | State: {pipeline.State}");
            GUILayout.Label($"Raw С {pipeline.Raw.Flexor:F2} / Р {pipeline.Raw.Extensor:F2}    Processed С {pipeline.Frame.Flexor:F2} / Р {pipeline.Frame.Extensor:F2}");
            GUILayout.Label($"Last: {lastIntent.Kind} #{lastIntent.Id} | intents {intentCount} | rejected {pipeline.RejectedSamples} | missing {pipeline.MissingSamples}");
            GUILayout.Label(pipeline.Fault ?? "Ready (emulator calibration 0..1)");
            GUILayout.Label($"С amplitude: {flexAmplitude:F2}");
            flexAmplitude = GUILayout.HorizontalSlider(flexAmplitude, 0, 1);
            GUILayout.Label($"Р amplitude: {extendAmplitude:F2}");
            extendAmplitude = GUILayout.HorizontalSlider(extendAmplitude, 0, 1);
            if (GUILayout.Button("Resume after fault (first release both channels)")) pipeline.Resume();
            GUILayout.EndArea();
            DrawZone(true, flexHeld); DrawZone(false, extendHeld);
        }
        private void DrawZone(bool flex, bool held)
        {
            Rect rect = Zone(flex); rect.y = Screen.height - rect.yMax;
            Color previous = GUI.backgroundColor; GUI.backgroundColor = held ? Color.green : Color.gray;
            GUI.Box(rect, flex ? "С / FLEX\nHold F or this zone\nTouch: slide vertically" : "Р / EXTEND\nHold R or this zone\nTouch: slide vertically");
            GUI.backgroundColor = previous;
        }
    }
}
