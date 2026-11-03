namespace Biopulse.Gameplay
{
    public enum GameCommandKind { Jump }

    public readonly struct GameCommand
    {
        public readonly long Id;
        public readonly GameCommandKind Kind;
        public readonly float Intensity;
        public readonly double InputTimeSeconds, RecognizedTimeSeconds;
        public GameCommand(long id, GameCommandKind kind, float intensity, double inputTime, double recognizedTime)
        { Id = id; Kind = kind; Intensity = intensity; InputTimeSeconds = inputTime; RecognizedTimeSeconds = recognizedTime; }
    }
}
