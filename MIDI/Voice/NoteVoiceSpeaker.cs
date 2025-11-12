using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Voice;
using MIDI.Voice.Core;
using System.IO;
using System;
using System.Linq;
using MIDI.Voice.Models;
using System.Collections.Generic;
using MIDI.Voice.Languages.Core;
using MIDI.Voice.Languages.Interface;
using MIDI.Voice.Languages.ACS;
using MIDI.Voice.Languages.EMEL;
using MIDI.Voice.Languages.SUSL;
using MIDI.Utils;

namespace MIDI.Voice
{
    public class NoteVoiceSpeaker : IVoiceSpeaker
    {
        private readonly VoiceModel _model;
        private readonly ParsingOrchestrator _parser;
        private NoteSynthesizer _synthesizer;

        public string EngineName => Translate.NoteVoiceSpeakerEngineName;
        public string SpeakerName => _model.Name;
        public string API => "MIDI_NoteSynth";
        public string ID => $"NoteSynth_{_model.Name}";

        public bool IsVoiceDataCachingRequired => true;
        public SupportedTextFormat Format => SupportedTextFormat.Custom;
        public IVoiceLicense? License => null;
        public IVoiceResource? Resource => null;

        public NoteVoiceSpeaker(VoiceModel model)
        {
            _model = model;
            _synthesizer = CreateSynthesizer();
            _model.PropertyChanged += Model_PropertyChanged;
            if (_model.InternalSynthSettings != null)
            {
                _model.InternalSynthSettings.PropertyChanged += Model_PropertyChanged;
            }
            _model.Layers.CollectionChanged += Model_PropertyChanged;

            var parsers = new List<ILanguageParser>
            {
                new EmelParserAdapter(),
                new SuslParserAdapter(),
                new AcsParser()
            };
            _parser = new ParsingOrchestrator(parsers);
        }

        private void Model_PropertyChanged(object? sender, EventArgs e)
        {
            _synthesizer = CreateSynthesizer();
        }


        private NoteSynthesizer CreateSynthesizer()
        {
            var tempSettings = new VoiceSynthSettings();
            tempSettings.SampleRate = NoteVoiceSettings.Default.SynthSettings.SampleRate;
            tempSettings.CurrentModelName = _model.Name;
            tempSettings.VoiceModels = new System.Collections.ObjectModel.ObservableCollection<VoiceModel> { (VoiceModel)_model.Clone() };

            return new NoteSynthesizer(tempSettings);
        }

        public bool IsMatch(string api, string id)
        {
            return api == API && id == ID;
        }

        public IVoiceParameter CreateVoiceParameter()
        {
            return new NoteVoiceParameter();
        }

        public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        {
            if (currentParameter is NoteVoiceParameter parameter)
                return parameter;
            else
                return CreateVoiceParameter();
        }

        public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter)
        {
            return Task.FromResult(text);
        }

        public async Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce, IVoiceParameter? parameter, string filePath)
        {
            var result = _parser.Parse(text);

            if (!result.IsSuccess || result.Output == null)
            {
                Logger.Error($"解析エラー: {result.Errors.FirstOrDefault()?.ErrorCode} {result.Errors.FirstOrDefault()?.MessageParameters.FirstOrDefault()}", null);
                var errorBeeps = _synthesizer.GenerateErrorBeeps(2);
                await WriteWavFileAsync(filePath, errorBeeps, _synthesizer.SampleRate);
                return null;
            }

            var events = result.Output;

            if (events is List<object> eventList && !eventList.Any())
            {
                Logger.Warn("解析結果が空のイベントリストでした。", null!);
                var errorBeeps = _synthesizer.GenerateErrorBeeps(1);
                await WriteWavFileAsync(filePath, errorBeeps, _synthesizer.SampleRate);
                return null;
            }

            float[] audioData;
            try
            {
                audioData = await _synthesizer.SynthesizeEventsAsync(events);
            }
            catch (Exception ex)
            {
                Logger.Error("合成タスクの実行に失敗しました (WorldlineHost.exeのクラッシュ等)。", ex);
                var errorBeeps = _synthesizer.GenerateErrorBeeps(4);
                await WriteWavFileAsync(filePath, errorBeeps, _synthesizer.SampleRate);
                return null;
            }

            if (audioData.Length == 0)
            {
                Logger.Error("合成結果が空でした (オーディオデータ長 0)。", null);
                var errorBeeps = _synthesizer.GenerateErrorBeeps(3);
                await WriteWavFileAsync(filePath, errorBeeps, _synthesizer.SampleRate);
                return null;
            }

            await WriteWavFileAsync(filePath, audioData, _synthesizer.SampleRate);

            return null;
        }

        private byte[] CreateSilentWav(int durationMs = 100)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            int sampleRate = _synthesizer.SampleRate;
            short numChannels = 1;
            short bitsPerSample = 16;
            int numSamples = sampleRate * durationMs / 1000;
            int dataSize = numSamples * numChannels * (bitsPerSample / 8);

            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(numChannels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * numChannels * (bitsPerSample / 8));
            writer.Write((short)(numChannels * (bitsPerSample / 8)));
            writer.Write(bitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            for (int i = 0; i < numSamples; i++)
            {
                writer.Write((short)0);
            }

            return ms.ToArray();
        }

        private async Task WriteWavFileAsync(string filePath, float[] audioData, int sampleRate)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(fs);

            short numChannels = 1;
            short bitsPerSample = 16;
            int dataSize = audioData.Length * (bitsPerSample / 8);

            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(numChannels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * numChannels * (bitsPerSample / 8));
            writer.Write((short)(numChannels * (bitsPerSample / 8)));
            writer.Write(bitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            foreach (float sample in audioData)
            {
                short shortSample = (short)Math.Clamp(sample * 32767.0f, short.MinValue, short.MaxValue);
                writer.Write(shortSample);
            }
            await fs.FlushAsync();
        }
    }
}