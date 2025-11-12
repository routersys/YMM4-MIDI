namespace MIDI.AudioEffect.SpatialAudioEffect.Algorithms
{
    public interface IConvolutionAlgorithm
    {
        int OutputSize { get; }
        void Process(float[] input, float[] outputBuffer);
        void Reset();
    }
}