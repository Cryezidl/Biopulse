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
        private IEmgSource source;
        private EmulatorEmgSource emulator;
        private EmgPipeline pipeline;
        private bool focused = true, waitForRelease;
        private bool applicationPaused;
        private EmgIntent lastIntent;
        private int intentCount;
        private float touchFlex, touchExtend;
        private bool flexHeld, extendHeld;
        private int flexTouchId = -1, extendTouchId = -1;

        public EmgPipeline Pipeline => pipeline;
        public IEmgSource Source => source;
        public float FlexAmplitude { get => flexAmplitude; set => flexAmplitude = Mathf.Clamp01(value); }
        public float ExtendAmplitude { get => extendAmplitude; set => extendAmplitude = Mathf.Clamp01(value); }
        public bool ShowDiagnostics { get => showDiagnostics; set => showDiagnostics = value; }
        public event Action<EmgIntent> IntentRecognized;

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            SetSource(new EmulatorEmgSource(), new EmgCalibration(EmulatorEmgSource.SourceId));
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
            pipeline.IntentRecognized += Forward;
            source.Start(); waitForRelease = true; ClearTouches();
        }
        private void Forward(EmgIntent intent)
        { lastIntent = intent; intentCount++; IntentRecognized?.Invoke(intent); }

        private void Update()
        {
            if (source == null || !focused || applicationPaused) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (emulator != null)
            {
                ReadControls();
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
            while (budget-- > 0 && source.TryRead(out var sample)) pipeline.Push(sample, now);
            pipeline.Tick(now, source.Status);
        }

        private Rect Zone(bool flex)
        {
            Rect safe = Screen.safeArea;
            float gap = 16;
            float width = (safe.width - 3 * gap) / 2;
            float height = Mathf.Min(220, safe.height * .38f);
            return new Rect(safe.x + gap + (flex ? 0 : width + gap), safe.y + gap, width, height);
        }
        private void ClearTouches() { flexTouchId = extendTouchId = -1; }
        private void ReadControls()
        {
            flexHeld = Keyboard.current?.fKey.isPressed == true;
            extendHeld = Keyboard.current?.rKey.isPressed == true;
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
            if (Touch.activeTouches.Count == 0 && Mouse.current?.leftButton.isPressed == true)
            {
                var position = Mouse.current.position.ReadValue();
                if (Zone(true).Contains(position)) flexHeld = true;
                if (Zone(false).Contains(position)) extendHeld = true;
            }
        }
        private void OnApplicationFocus(bool value)
        {
            focused = value; waitForRelease = true; ClearTouches();
            if (!value) pipeline?.Suspend("Application focus lost; release controls and resume");
        }
        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused; waitForRelease = true; ClearTouches();
            // Unity also sends paused=false on startup. Only a real pause invalidates input.
            if (paused) pipeline?.Suspend("Application paused; release controls and resume");
        }
        private void OnGUI()
        {
            if (!showDiagnostics || pipeline == null) return;
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
