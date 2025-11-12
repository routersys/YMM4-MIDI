using System;
using System.Collections.Generic;

namespace MIDI
{
    public interface IAudioRenderer : IDisposable
    {
        float[] Render(string midiFilePath, TimeSpan? durationLimit);
        void Seek(long samplePosition);
        int Read(Span<float> buffer, long position);
    }
}