namespace MIDI.AudioEffect.SPATIAL.Algorithms
{
    public interface IConvolutionAlgorithm
    {
        int OutputSize { get; }
        void Process(float[] input, float[] outputBuffer);
        void Reset();
    }
}