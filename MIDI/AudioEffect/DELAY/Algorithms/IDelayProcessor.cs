namespace MIDI.AudioEffect.DELAY.Algorithms
{
    public interface IDelayProcessor
    {
        double DelayTimeMs { get; set; }
        double Feedback { get; set; }
        float Process(float input);
        void Reset();
    }
}