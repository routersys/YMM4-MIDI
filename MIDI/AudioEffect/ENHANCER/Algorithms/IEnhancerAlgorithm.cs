namespace MIDI.AudioEffect.ENHANCER.Algorithms
{
    public interface IEnhancerAlgorithm
    {
        double Drive { get; set; }
        double Frequency { get; set; }
        float Process(float input);
        void Reset();
    }
}