using YukkuriMovieMaker.Plugin.FileSource;
using System.IO;

namespace MIDI
{
    public class MidiAudioSourcePlugin : IAudioFileSourcePlugin
    {
        public string Name => "MIDI読み込み";

        public IAudioFileSource? CreateAudioFileSource(string filePath, int audioTrackIndex)
        {
            var extension = Path.GetExtension(filePath)?.ToLower();
            if (extension == ".mid" || extension == ".midi")
            {
                try
                {
                    return new MidiAudioSource(filePath);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }
}