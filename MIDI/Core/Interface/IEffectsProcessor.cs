namespace MIDI
{
    public interface IEffectsProcessor : IDisposable
    {
        void Reset();
        float ApplyChannelEffects(float input, ChannelState channelState, double time);
        bool ApplyAudioEnhancements(Span<float> buffer);
        void NormalizeAudio(Span<float> buffer);
    }
}