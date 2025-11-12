namespace MIDI
{
    public interface IFilterProcessor
    {
        float ApplyFilters(float input, InstrumentSettings instrument, double time, ChannelState channelState);
    }
}