using UnityEngine;

namespace Biopulse.Neon
{
    public enum Sfx { Jump, Land, Grab, Release, Death, Mopped, Checkpoint, Unlock, Score, UiMove, UiConfirm, Robot, Chirp }

    // Everything is synthesised at startup: no third-party audio, no licences, identical on every device.
    // Jump pitch follows Р amplitude and the lock hum follows hold progress, so feedback is not only visual.
    public sealed class NeonAudio : MonoBehaviour
    {
        public static NeonAudio Instance { get; private set; }
        public int MusicKey = 0;          // semitones relative to A minor
        public float MusicBpm = 100;
        [Range(0, 1)] public float MusicVolume = .32f, SfxVolume = .7f;

        private const int Rate = 32000;
        private AudioClip[] clips;
        private AudioSource[] voices;
        private AudioSource music, hum;
        private int nextVoice;
        private static System.Random rng = new System.Random(7);

        private void Awake()
        {
            Instance = this;
            int n = System.Enum.GetValues(typeof(Sfx)).Length;
            clips = new AudioClip[n];
            for (int i = 0; i < n; i++) clips[i] = Build((Sfx)i);
            voices = new AudioSource[10];
            for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
            hum = gameObject.AddComponent<AudioSource>(); hum.clip = Make("hum", 1f, t => (Sin(110 * t) * .6f + Sin(220.5f * t) * .3f + Saw(55 * t) * .15f) * .5f);
            hum.loop = true; hum.volume = 0; hum.Play();
            music = gameObject.AddComponent<AudioSource>(); music.clip = BuildMusic(); music.loop = true; music.volume = MusicVolume; music.Play();
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static void Play(Sfx s, float pitch = 1, float volume = 1)
        {
            var a = Instance; if (a == null) return;
            var v = a.voices[a.nextVoice]; a.nextVoice = (a.nextVoice + 1) % a.voices.Length;
            v.clip = a.clips[(int)s]; v.pitch = pitch; v.volume = a.SfxVolume * volume; v.Play();
        }

        // 0 = silent; 0..1 = lock hold progress (rising pitch).
        public static void Hum(float level, float progress)
        {
            var a = Instance; if (a == null) return;
            a.hum.volume = Mathf.MoveTowards(a.hum.volume, level * .35f * a.SfxVolume, Time.unscaledDeltaTime * 3);
            a.hum.pitch = 1 + progress * 1.2f;
        }

        // ---------- synthesis ----------
        private static float Sin(float cycles) => Mathf.Sin(cycles * 2 * Mathf.PI);
        private static float Saw(float cycles) => 2 * (cycles - Mathf.Floor(cycles + .5f));
        private static float Sqr(float cycles) => cycles - Mathf.Floor(cycles) < .5f ? 1 : -1;
        private static float Noise() => (float)(rng.NextDouble() * 2 - 1);
        private static float Env(float t, float attack, float length) => t < attack ? t / attack : Mathf.Clamp01(1 - (t - attack) / (length - attack));

        private static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1, 1);
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Build(Sfx s)
        {
            float phase = 0;
            switch (s)
            {
                case Sfx.Jump:      // rising chirp; runtime pitch scales with amplitude
                    return Make("jump", .2f, t => { float f = Mathf.Lerp(260, 820, t / .2f); phase += f / Rate; return (Sqr(phase) * .25f + Sin(phase) * .4f) * Env(t, .005f, .2f); });
                case Sfx.Land:
                    { float lp = 0; return Make("land", .12f, t => { lp += (Noise() - lp) * .08f; return (lp * 2.5f + Sin(60 * t) * .5f) * Env(t, .002f, .12f); }); }
                case Sfx.Grab:      // magnetic "tchk-zzt"
                    return Make("grab", .16f, t => { float f = Mathf.Lerp(1400, 300, t / .16f); phase += f / Rate; return (Sqr(phase) * .3f + Noise() * .2f * Env(t, .001f, .03f)) * Env(t, .002f, .16f); });
                case Sfx.Release:   // whoosh
                    { float lp = 0; return Make("release", .28f, t => { float k = Mathf.Lerp(.02f, .25f, t / .28f); lp += (Noise() - lp) * k; return lp * 1.8f * Env(t, .05f, .28f); }); }
                case Sfx.Death:     // comic "wah-wah-waaah"
                    return Make("death", .75f, t => { float f = Mathf.Lerp(520, 90, t / .75f) * (1 + .08f * Sin(9 * t)); phase += f / Rate; return (Saw(phase) * .35f + Sqr(phase * .5f) * .15f) * Env(t, .01f, .75f); });
                case Sfx.Mopped:    // squeaky wet "boing"
                    return Make("mopped", .45f, t => { float f = 300 + 500 * Mathf.Abs(Sin(6 * t)) * (1 - t / .45f); phase += f / Rate; return Sin(phase) * .6f * Env(t, .005f, .45f) + Noise() * .1f * Env(t, .001f, .1f); });
                case Sfx.Checkpoint:
                    return Arp("checkpoint", new[] { 523.3f, 659.3f, 784f, 1046.5f }, .07f, .5f);
                case Sfx.Unlock:
                    return Arp("unlock", new[] { 392f, 523.3f, 659.3f, 784f, 1046.5f, 1318.5f }, .05f, .6f);
                case Sfx.Score:
                    return Make("score", .08f, t => Sin(1320 * t) * .5f * Env(t, .002f, .08f));
                case Sfx.UiMove:
                    return Make("uimove", .05f, t => Sqr(900 * t) * .25f * Env(t, .001f, .05f));
                case Sfx.UiConfirm:
                    return Arp("uiconfirm", new[] { 660f, 990f }, .06f, .2f);
                case Sfx.Robot:     // robot babble
                    return Make("robot", .4f, t => { int step = (int)(t * 18); float f = 300 + (step * 7919 % 9) * 70; return Sqr(f * t) * .22f * Env(t, .01f, .4f) * (Sqr(18 * t) > 0 ? 1 : .4f); });
                default:            // chirp for buddy quips
                    return Make("chirp", .12f, t => { float f = Mathf.Lerp(900, 1500, Mathf.Sin(t / .12f * Mathf.PI)); phase += f / Rate; return Sin(phase) * .4f * Env(t, .005f, .12f); });
            }
        }

        private static AudioClip Arp(string name, float[] notes, float step, float total)
        {
            return Make(name, total, t =>
            {
                float sum = 0;
                for (int i = 0; i < notes.Length; i++)
                {
                    float lt = t - i * step; if (lt < 0) break;
                    sum += (Sin(notes[i] * lt) * .5f + Sqr(notes[i] * lt) * .08f) * Mathf.Exp(-lt * 7);
                }
                return sum * .45f;
            });
        }

        // Procedural synthwave loop: 4 chords x 2 bars, kick/snare/hats, arpeggiated bass, soft detuned pad.
        private AudioClip BuildMusic()
        {
            float beat = 60f / MusicBpm;
            int bars = 8; float length = bars * 4 * beat;
            int count = Mathf.CeilToInt(length * Rate);
            var data = new float[count];
            float k = Mathf.Pow(2, MusicKey / 12f);
            float[][] chords =
            {
                new[] { 220f, 261.6f, 329.6f },   // Am
                new[] { 174.6f, 220f, 261.6f },   // F
                new[] { 261.6f, 329.6f, 392f },   // C
                new[] { 196f, 246.9f, 293.7f },   // G
            };
            float bassLp = 0, hatHp = 0, prevNoise = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                float beatPos = t / beat;
                int bar = (int)(beatPos / 4) % bars;
                var chord = chords[(bar / 2) % 4];
                float inBeat = beatPos - Mathf.Floor(beatPos);
                int beatIndex = (int)beatPos % 4;
                // kick
                float kt = inBeat * beat;
                float kick = Sin((50 + 120 * Mathf.Exp(-kt * 30)) * kt) * Mathf.Exp(-kt * 9) * .9f;
                // snare on 2 & 4
                float snare = (beatIndex == 1 || beatIndex == 3) ? Noise() * Mathf.Exp(-kt * 18) * .35f : 0;
                // hats on 8ths (high-passed noise)
                float eighth = beatPos * 2 - Mathf.Floor(beatPos * 2);
                float n = Noise(); hatHp = n - prevNoise; prevNoise = n;
                float hat = hatHp * Mathf.Exp(-eighth * beat * .5f * 60) * .12f;
                // bass: 16th arpeggio of the chord root/fifth an octave down
                int sixteenth = (int)(beatPos * 4) % 4;
                float bf = chord[sixteenth == 2 ? 2 : 0] * .5f * k;
                float st = (beatPos * 4 - Mathf.Floor(beatPos * 4)) * beat / 4;
                float raw = Saw(bf * t) * Mathf.Exp(-st * 10);
                bassLp += (raw - bassLp) * .12f;
                // pad: detuned saws, slow swell per chord
                float padEnv = .5f + .5f * Mathf.Sin((beatPos / 8) * Mathf.PI * 2 - Mathf.PI / 2);
                float pad = 0;
                foreach (var f in chord) pad += Saw(f * k * t) + Saw(f * k * 1.006f * t);
                pad *= .03f * (.4f + .6f * padEnv);
                // sidechain feel: duck everything but kick on each beat
                float duck = .55f + .45f * Mathf.Clamp01(inBeat * 3);
                data[i] = Mathf.Clamp((kick + snare + hat + (bassLp * .5f + pad) * duck) * .55f, -1, 1);
            }
            var clip = AudioClip.Create("music", count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
