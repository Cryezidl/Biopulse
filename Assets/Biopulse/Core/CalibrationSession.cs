using System;
using System.Collections.Generic;

namespace Biopulse.Emg
{
    public enum CalibrationStage { Rest, Flex, Extend, Complete, Failed }
    // Captures source envelopes, never normalized values. Time is supplied by source samples.
    public sealed class CalibrationSession
    {
        private readonly string sourceId;
        private readonly List<float> restFlex = new List<float>(), restExtend = new List<float>();
        private readonly List<float> pulse = new List<float>(), references = new List<float>();
        private double first = double.NaN, last = double.NegativeInfinity, pulseStart;
        private bool active, waitForRest = true;
        private float flexBaseline, extendBaseline, flexNoise, extendNoise, flexReference;
        public CalibrationStage Stage { get; private set; }
        public int Repetitions => references.Count;
        public float RestProgress { get; private set; }
        public string Error { get; private set; }
        public EmgCalibration Result { get; private set; }
        public CalibrationSession(string sourceId) { this.sourceId = sourceId; }
        public void Interrupt(string reason) { Stage = CalibrationStage.Failed; Error = reason; }
        public void Push(EmgSample sample)
        {
            if (Stage == CalibrationStage.Failed || Stage == CalibrationStage.Complete) return;
            if (!Numbers.Finite(sample.TimeSeconds) || !Numbers.Finite(sample.Flexor) || !Numbers.Finite(sample.Extensor) ||
                sample.Quality != EmgSampleQuality.Valid || sample.TimeSeconds <= last ||
                (!double.IsNegativeInfinity(last) && sample.TimeSeconds - last > .25))
            { Interrupt("Поток прерван или содержит неверные данные. Повторите настройку."); return; }
            last = sample.TimeSeconds;
            if (Stage == CalibrationStage.Rest)
            {
                if (double.IsNaN(first)) first = last;
                if (restFlex.Count >= 6000) { Interrupt("Слишком много отсчётов для этого режима калибровки."); return; }
                restFlex.Add(sample.Flexor); restExtend.Add(sample.Extensor);
                RestProgress = (float)Math.Min(1, (last - first) / 3);
                if (last - first < 3) return;
                flexBaseline = Percentile(restFlex, .5f); extendBaseline = Percentile(restExtend, .5f);
                flexNoise = Percentile(restFlex, .95f) - Percentile(restFlex, .05f);
                extendNoise = Percentile(restExtend, .95f) - Percentile(restExtend, .05f);
                Stage = CalibrationStage.Flex; waitForRest = true; return;
            }
            bool flex = Stage == CalibrationStage.Flex;
            float baseline = flex ? flexBaseline : extendBaseline;
            float noise = flex ? flexNoise : extendNoise;
            float otherBaseline = flex ? extendBaseline : flexBaseline;
            float otherNoise = flex ? extendNoise : flexNoise;
            float value = flex ? sample.Flexor : sample.Extensor;
            float other = flex ? sample.Extensor : sample.Flexor;
            float onset = baseline + Math.Max(.08f, noise * 3);
            float off = baseline + Math.Max(.035f, noise * 1.5f);
            bool bothRest = value <= off && other <= otherBaseline + Math.Max(.035f, otherNoise * 1.5f);
            if (waitForRest) { if (bothRest) waitForRest = false; return; }
            if (other > otherBaseline + Math.Max(.08f, otherNoise * 3))
            { active = false; pulse.Clear(); waitForRest = true; return; }
            if (!active && value >= onset) { active = true; pulseStart = last; pulse.Clear(); }
            if (!active) return;
            if (last - pulseStart > 5) { Interrupt("Сокращение слишком долгое. Расслабьтесь и повторите настройку."); return; }
            if (value > off)
            {
                if (pulse.Count >= 6000) { Interrupt("Превышен размер записи активации."); return; }
                pulse.Add(value); return;
            }
            active = false; waitForRest = true;
            if (last - pulseStart < .25 || pulse.Count < 3) return;
            references.Add(Percentile(pulse, .75f)); pulse.Clear();
            if (references.Count < 3) return;
            float reference = Percentile(references, .5f);
            if (reference - baseline < Math.Max(.08f, noise * 5))
            { Interrupt("Диапазон слишком мал относительно шума. Повторите в комфортном режиме."); return; }
            if (flex) { flexReference = reference; references.Clear(); Stage = CalibrationStage.Extend; }
            else
            {
                Result = new EmgCalibration(sourceId, flexBaseline, flexReference, extendBaseline, reference);
                Stage = CalibrationStage.Complete;
            }
        }
        private static float Percentile(List<float> values, float percentile)
        {
            var sorted = values.ToArray(); Array.Sort(sorted);
            return sorted[(int)Math.Round((sorted.Length - 1) * percentile)];
        }
    }
}
