using NAudio.Wave;

namespace MIDI.AudioEffect.SPATIAL.Services
{
    public class IrStatusService
    {
        public (bool IsValid, string Message) ValidateFiles(string leftPath, string rightPath)
        {
            var (leftValid, leftMsg, leftChannels) = ValidateFile(leftPath);

            if (leftValid && leftChannels == 2)
            {
                return (leftValid, $"L (ステレオIR): {leftMsg}, R (L側で上書き)");
            }

            var (rightValid, rightMsg, _) = ValidateFile(rightPath);

            if (leftValid && rightValid)
            {
                return (true, $"L: {leftMsg}, R: {rightMsg}");
            }

            return (false, $"L: {leftMsg}, R: {rightMsg}");
        }

        private (bool IsValid, string Message, int Channels) ValidateFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return (true, "未指定", 0);
            }

            if (!System.IO.File.Exists(path))
            {
                return (false, "エラー: ファイル未検出", 0);
            }

            try
            {
                using (var reader = new WaveFileReader(path))
                {
                    if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm &&
                        reader.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                    {
                        return (false, "エラー: 非対応形式 (PCM/Floatのみ)", 0);
                    }
                    if (reader.WaveFormat.Channels > 2)
                    {
                        return (false, "エラー: 3ch以上は非対応", reader.WaveFormat.Channels);
                    }
                    return (true, "成功", reader.WaveFormat.Channels);
                }
            }
            catch
            {
                return (false, "エラー: WAV読込失敗", 0);
            }
        }
    }
}