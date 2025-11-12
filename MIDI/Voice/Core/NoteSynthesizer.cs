using MeltySynth;
using MIDI.Configuration.Models;
using MIDI.Utils;
using MIDI.Voice.Engine.Worldline;
using MIDI.Voice.Models;
using MIDI.Voice.SUSL.Core;
using MIDI.Voice.SUSL.Parsing.AST;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MIDI.Voice.Languages.ACS;

namespace MIDI.Voice.Core
{
    public class NoteSynthesizer
    {
        private readonly VoiceSynthSettings _settings;
        private VoiceModel _model;
        private Synthesizer? _soundFontSynth;
        private SynthesisEngine? _internalSynth;
        private WorldlineSynthesizer? _worldlineSynth;
        private UtauVoicebank? _utauVoicebank;
        private readonly object _synthLock = new object();
        private const double FadeOutDurationSeconds = 0.05;
        private const double MaxDurationSeconds = 600.0;

        public int SampleRate => _settings?.SampleRate ?? 44100;

        public NoteSynthesizer(VoiceSynthSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _model = _settings.VoiceModels.FirstOrDefault(m => m.Name == _settings.CurrentModelName)
                     ?? _settings.VoiceModels.FirstOrDefault()
                     ?? throw new InvalidOperationException("No voice models available.");


            InitializeSynthesizer();
            _settings.PropertyChanged += Settings_PropertyChanged;
            _model.PropertyChanged += Model_PropertyChanged;
            if (_model.InternalSynthSettings != null)
            {
                _model.InternalSynthSettings.PropertyChanged += Model_PropertyChanged;
            }
            _model.Layers.CollectionChanged += Model_PropertyChanged;

        }

        ~NoteSynthesizer()
        {
            _settings.PropertyChanged -= Settings_PropertyChanged;
            _model.PropertyChanged -= Model_PropertyChanged;
            if (_model.InternalSynthSettings != null)
            {
                _model.InternalSynthSettings.PropertyChanged -= Model_PropertyChanged;
            }
            _model.Layers.CollectionChanged -= Model_PropertyChanged;
        }

        private void Model_PropertyChanged(object? sender, EventArgs e)
        {
            InitializeSynthesizer();
        }


        private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VoiceSynthSettings.SampleRate) || e.PropertyName == nameof(VoiceSynthSettings.CurrentModelName))
            {
                var newModel = _settings.VoiceModels.FirstOrDefault(m => m.Name == _settings.CurrentModelName) ?? _settings.VoiceModels.FirstOrDefault();
                if (newModel != null && newModel != _model)
                {
                    _model.PropertyChanged -= Model_PropertyChanged;
                    if (_model.InternalSynthSettings != null)
                    {
                        _model.InternalSynthSettings.PropertyChanged -= Model_PropertyChanged;
                    }
                    _model.Layers.CollectionChanged -= Model_PropertyChanged;

                    _model = newModel;

                    _model.PropertyChanged += Model_PropertyChanged;
                    if (_model.InternalSynthSettings != null)
                    {
                        _model.InternalSynthSettings.PropertyChanged += Model_PropertyChanged;
                    }
                    _model.Layers.CollectionChanged += Model_PropertyChanged;
                }
                InitializeSynthesizer();
            }
        }

        private void InitializeSynthesizer()
        {
            lock (_synthLock)
            {
                _soundFontSynth = null;
                _internalSynth = null;
                _worldlineSynth = null;
                _utauVoicebank = null;

                if (_model.ModelType == ModelType.SoundFont)
                {
                    InitializeSoundFont();
                }
                else if (_model.ModelType == ModelType.UTAU)
                {
                    InitializeUtau();
                }
                else
                {
                    _internalSynth = CreateInternalSynth();
                }
            }
        }

        private void InitializeUtau()
        {
            if (string.IsNullOrEmpty(_model.UtauVoicePath) || !Directory.Exists(_model.UtauVoicePath))
            {
                Logger.Error($"UTAUモデルのパスが無効です。モデル: {_model.Name}, パス: {_model.UtauVoicePath}", null);
                _worldlineSynth = null;
                _utauVoicebank = null;
                return;
            }

            try
            {
                _utauVoicebank = new UtauVoicebank(_model.UtauVoicePath);
                _worldlineSynth = new WorldlineSynthesizer(_utauVoicebank);
                Logger.Info($"UTAUモデルをロードしました。モデル: {_model.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"UTAUモデルのロードに失敗しました。モデル: {_model.Name}", ex);
                _worldlineSynth = null;
                _utauVoicebank = null;
            }
        }


        private SynthesisEngine CreateInternalSynth()
        {
            try
            {
                if (_model.InternalSynthSettings == null)
                    _model.InternalSynthSettings = new InternalSynthModel();

                ValidateSettings(_settings, _model.InternalSynthSettings);
                return new SynthesisEngine(CreateMidiConfigFromVoiceModel(_model), SampleRate);
            }
            catch (ArgumentException ex)
            {
                Logger.Error(LogMessages.InvalidSettingValue, ex, ex.Message);
                var defaultModel = new VoiceModel { InternalSynthSettings = new InternalSynthModel() };
                return new SynthesisEngine(CreateMidiConfigFromVoiceModel(defaultModel), SampleRate);
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.InternalSynthCreationFailed, ex);
                throw;
            }
        }

        private void ValidateSettings(VoiceSynthSettings settings, InternalSynthModel modelSettings)
        {
            if (settings.SampleRate <= 0) throw new ArgumentException("Sample rate must be positive.", nameof(settings.SampleRate));
            if (modelSettings.Attack < 0) throw new ArgumentException("Attack must be non-negative.", nameof(modelSettings.Attack));
            if (modelSettings.Decay < 0) throw new ArgumentException("Decay must be non-negative.", nameof(modelSettings.Decay));
            if (modelSettings.Sustain < 0 || modelSettings.Sustain > 1) throw new ArgumentException("Sustain must be between 0 and 1.", nameof(modelSettings.Sustain));
            if (modelSettings.Release < 0) throw new ArgumentException("Release must be non-negative.", nameof(modelSettings.Release));
        }

        private void InitializeSoundFont()
        {
            _soundFontSynth = null;
            if (_model.ModelType != ModelType.SoundFont) return;

            try
            {
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                if (string.IsNullOrEmpty(assemblyLocation))
                {
                    Logger.Error(LogMessages.AssemblyLocationError, null);
                    return;
                }

                var sfDir = Path.Combine(assemblyLocation, MidiConfiguration.Default.SoundFont.DefaultSoundFontDirectory);
                List<string> layerPaths = _model.Layers
                       .Select(layer => Path.Combine(sfDir, layer.SoundFontFile))
                       .Where(File.Exists)
                       .ToList();

                var maxPolyphony = Math.Clamp(MidiConfiguration.Default.Performance.MaxPolyphony, 8, 256);

                var synthesizerSettings = new SynthesizerSettings(SampleRate)
                {
                    MaximumPolyphony = maxPolyphony
                };

                if (layerPaths.Any())
                {
                    _soundFontSynth = new Synthesizer(layerPaths.First(), synthesizerSettings);
                    Logger.Info(LogMessages.LayeredSoundFontLoaded, layerPaths.First());
                }
                else
                {
                    TryLoadDefaultSoundFont(assemblyLocation, synthesizerSettings);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.SoundFontLoadFailedGeneric, ex);
                TryLoadDefaultSoundFont(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), new SynthesizerSettings(SampleRate) { MaximumPolyphony = Math.Clamp(MidiConfiguration.Default.Performance.MaxPolyphony, 8, 256) });
            }
        }

        private void TryLoadDefaultSoundFont(string? assemblyLocation, SynthesizerSettings synthesizerSettings)
        {
            if (string.IsNullOrEmpty(assemblyLocation))
            {
                Logger.Error(LogMessages.AssemblyLocationError, null, "when trying to load default SoundFont");
                return;
            }
            var defaultPath = Path.Combine(assemblyLocation, "GeneralUser-GS.sf2");
            if (File.Exists(defaultPath))
            {
                try
                {
                    synthesizerSettings.MaximumPolyphony = Math.Clamp(synthesizerSettings.MaximumPolyphony, 8, 256);
                    _soundFontSynth = new Synthesizer(defaultPath, synthesizerSettings);
                    Logger.Info(LogMessages.DefaultSoundFontLoaded, defaultPath);
                }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.DefaultSoundFontLoadFailed, ex, defaultPath);
                    _soundFontSynth = null;
                }
            }
            else
            {
                _soundFontSynth = null;
                Logger.Warn(LogMessages.DefaultSoundFontNotFound, defaultPath);
            }
        }

        public Task<float[]> SynthesizeEventsAsync(object events)
        {
            if (events is SuslProgram suslProgram)
            {
                return Task.Run(() =>
                {
                    lock (_synthLock)
                    {
                        if (_model.ModelType == ModelType.UTAU && _worldlineSynth != null)
                        {
                            try
                            {
                                return _worldlineSynth.Synthesize(suslProgram);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("UTAU(Worldline)での合成に失敗しました。", ex);
                                return Array.Empty<float>();
                            }
                        }
                        else
                        {
                            Logger.Warn("SUSL input requires a UTAU (Worldline) model to be selected and loaded.");
                            return Array.Empty<float>();
                        }
                    }
                });
            }

            if (events is not List<object> eventList || !eventList.Any())
            {
                Logger.Warn(LogMessages.EmptyNoteList);
                return Task.FromResult(Array.Empty<float>());
            }

            return Task.Run(() =>
            {
                lock (_synthLock)
                {
                    try
                    {
                        if (_model.ModelType == ModelType.SoundFont && _soundFontSynth != null)
                        {
                            return SynthesizeWithSoundFont(eventList, _soundFontSynth);
                        }
                        else if (_model.ModelType == ModelType.InternalSynth && _internalSynth != null)
                        {
                            return SynthesizeWithInternal(eventList, _internalSynth);
                        }
                        else if (_model.ModelType == ModelType.UTAU && _worldlineSynth != null)
                        {
                            Logger.Warn("ACS/EMEL input is not supported for UTAU (Worldline) models.");
                            return Array.Empty<float>();
                        }
                        else
                        {
                            Logger.Warn("Synthesizer not initialized for the current model type (non-SUSL).");
                            return Array.Empty<float>();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(LogMessages.NoteSynthesisFailed, ex);
                        return Array.Empty<float>();
                    }
                }
            });
        }


        private float[] SynthesizeWithSoundFont(List<object> events, Synthesizer synthesizer)
        {
            try
            {
                double maxEventTime = events.Max(ev =>
                {
                    if (ev is NoteData nd) return nd.AbsoluteTimeSeconds + nd.DurationSeconds;
                    if (ev is MidiCommandData mcd) return mcd.AbsoluteTimeSeconds;
                    return 0.0;
                });

                double totalDurationStrict = maxEventTime;

                if (totalDurationStrict > MaxDurationSeconds)
                {
                    Logger.Warn(LogMessages.AudioDurationExceededLimit, totalDurationStrict, MaxDurationSeconds);
                    totalDurationStrict = MaxDurationSeconds;
                }

                int totalSamplesStrict = (int)Math.Round(totalDurationStrict * synthesizer.SampleRate);
                if (totalSamplesStrict <= 0) return Array.Empty<float>();

                totalSamplesStrict = Math.Min(totalSamplesStrict, (int)Math.Round(MaxDurationSeconds * synthesizer.SampleRate));

                int renderAheadSamples = (int)Math.Round(1.0 * synthesizer.SampleRate);
                int totalRenderSamples = totalSamplesStrict + renderAheadSamples;

                var renderBuffer = new float[totalRenderSamples];
                if (renderBuffer.Length == 0) return Array.Empty<float>();

                var left = ArrayPool<float>.Shared.Rent(4096);
                var right = ArrayPool<float>.Shared.Rent(4096);

                try
                {
                    int currentSample = 0;
                    synthesizer.Reset();
                    var lastEventSample = 0;

                    var activeNotes = new Dictionary<(int track, int channel, int note), long>();

                    foreach (var ev in events)
                    {
                        int eventSample = 0;
                        if (ev is NoteData nd) eventSample = (int)Math.Round(nd.AbsoluteTimeSeconds * synthesizer.SampleRate);
                        else if (ev is MidiCommandData mcd) eventSample = (int)Math.Round(mcd.AbsoluteTimeSeconds * synthesizer.SampleRate);

                        int samplesToRender = Math.Min(eventSample - lastEventSample, totalRenderSamples - currentSample);

                        if (samplesToRender > 0)
                        {
                            RenderSilence(synthesizer, renderBuffer, currentSample, samplesToRender, left, right);
                            currentSample += samplesToRender;
                            lastEventSample += samplesToRender;
                        }

                        if (currentSample >= totalRenderSamples) break;

                        if (ev is MidiCommandData cmd)
                        {
                            int channel = cmd.Channel - 1;
                            if (channel < 0 || channel > 15) continue;

                            switch (cmd.Type)
                            {
                                case CommandType.ControlChange:
                                    synthesizer.ProcessMidiMessage(channel, 0xB0 | channel, cmd.Data1, cmd.Data2);
                                    break;
                                case CommandType.ProgramChange:
                                    synthesizer.ProcessMidiMessage(channel, 0xC0 | channel, cmd.Data1, 0);
                                    break;
                                case CommandType.PitchBend:
                                    synthesizer.ProcessMidiMessage(channel, 0xE0 | channel, cmd.Data1, cmd.Data2);
                                    break;
                                case CommandType.ChannelPressure:
                                    synthesizer.ProcessMidiMessage(channel, 0xD0 | channel, cmd.Data1, 0);
                                    break;
                                case CommandType.TimingClock: synthesizer.ProcessMidiMessage(0, 0xF8, 0, 0); break;
                                case CommandType.Start: synthesizer.ProcessMidiMessage(0, 0xFA, 0, 0); break;
                                case CommandType.Continue: synthesizer.ProcessMidiMessage(0, 0xFB, 0, 0); break;
                                case CommandType.Stop: synthesizer.ProcessMidiMessage(0, 0xFC, 0, 0); break;
                            }
                        }
                        else if (ev is NoteData note)
                        {
                            int channel = note.Channel - 1;
                            if (channel < 0 || channel > 15) continue;

                            int noteDurationSamples = (int)Math.Round(note.DurationSeconds * synthesizer.SampleRate);
                            long noteEndSample = eventSample + noteDurationSamples;


                            if (note.MidiNoteNumber >= 0)
                            {
                                var velocity = (int)Math.Clamp(note.Volume * 127, 0, 127);

                                if (note.IsError)
                                {
                                    int samplesToRenderError = Math.Min(noteDurationSamples, totalRenderSamples - currentSample);
                                    if (samplesToRenderError > 0)
                                    {
                                        RenderErrorBeep(renderBuffer, currentSample, samplesToRenderError, synthesizer.SampleRate, 4, 880);
                                    }
                                }
                                else
                                {

                                    synthesizer.ProcessMidiMessage(channel, 0xB0 | channel, 1, note.Modulation);
                                    synthesizer.ProcessMidiMessage(channel, 0xB0 | channel, 11, note.Expression);
                                    synthesizer.ProcessMidiMessage(channel, 0xB0 | channel, 10, note.Pan);
                                    synthesizer.ProcessMidiMessage(channel, 0xE0 | channel, note.PitchBend & 0x7F, note.PitchBend >> 7);
                                    synthesizer.ProcessMidiMessage(channel, 0xD0 | channel, note.ChannelPressure, 0);

                                    synthesizer.NoteOn(channel, note.MidiNoteNumber, velocity);
                                    activeNotes[(note.Track, channel, note.MidiNoteNumber)] = noteEndSample;
                                }
                            }

                            foreach (var kvp in activeNotes.Where(kv => kv.Value <= eventSample).ToList())
                            {
                                synthesizer.NoteOff(kvp.Key.channel, kvp.Key.note);
                                activeNotes.Remove(kvp.Key);
                            }

                        }
                    }

                    int remainingSamples = totalRenderSamples - currentSample;
                    if (remainingSamples > 0)
                    {
                        RenderSilence(synthesizer, renderBuffer, currentSample, remainingSamples, left, right);
                    }

                    var finalBuffer = new float[totalSamplesStrict];
                    Array.Copy(renderBuffer, finalBuffer, Math.Min(renderBuffer.Length, finalBuffer.Length));

                    ApplyFadeOut(finalBuffer.AsSpan(), (int)(FadeOutDurationSeconds * synthesizer.SampleRate));

                    return finalBuffer;
                }
                finally
                {
                    ArrayPool<float>.Shared.Return(left);
                    ArrayPool<float>.Shared.Return(right);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.SoundFontSynthesisError, ex);
                return Array.Empty<float>();
            }
        }


        private float[] SynthesizeWithInternal(List<object> events, SynthesisEngine synthEngine)
        {
            try
            {
                double maxEventTime = events.Max(ev =>
                {
                    if (ev is NoteData nd) return nd.AbsoluteTimeSeconds + nd.DurationSeconds;
                    if (ev is MidiCommandData mcd) return mcd.AbsoluteTimeSeconds;
                    return 0.0;
                });
                double totalDurationStrict = maxEventTime;

                float maxReleaseTime = 0.2f;
                var instrumentSettingsDict = synthEngine.InitializeInstrumentSettings();
                foreach (var ev in events)
                {
                    if (ev is NoteData note)
                    {
                        var instrument = synthEngine.GetInstrumentSettings(note.Channel, 0, instrumentSettingsDict);
                        maxReleaseTime = Math.Max(maxReleaseTime, (float)instrument.Release);
                    }
                }


                if (totalDurationStrict > MaxDurationSeconds)
                {
                    Logger.Warn(LogMessages.AudioDurationExceededLimit, totalDurationStrict, MaxDurationSeconds);
                    totalDurationStrict = MaxDurationSeconds;
                }

                int totalSamplesStrict = (int)Math.Round(totalDurationStrict * synthEngine.SampleRate);
                if (totalSamplesStrict <= 0) return Array.Empty<float>();

                totalSamplesStrict = Math.Min(totalSamplesStrict, (int)Math.Round(MaxDurationSeconds * synthEngine.SampleRate));

                int renderAheadSamples = (int)Math.Round(maxReleaseTime * synthEngine.SampleRate) + (int)(0.1 * SampleRate);
                int totalRenderSamples = totalSamplesStrict + renderAheadSamples;


                var renderBuffer = new float[totalRenderSamples];
                if (renderBuffer.Length == 0) return Array.Empty<float>();

                var bufferSpan = renderBuffer.AsSpan();


                var channelStates = new Dictionary<int, ChannelState>();
                for (int i = 1; i <= 16; i++) channelStates[i] = new ChannelState();


                foreach (var ev in events)
                {
                    if (ev is MidiCommandData cmd)
                    {
                        if (cmd.Channel < 1 || cmd.Channel > 16) continue;
                        var state = channelStates[cmd.Channel];
                        switch (cmd.Type)
                        {
                            case CommandType.ControlChange:
                                HandleInternalSynthControlChange(cmd, state);
                                break;
                            case CommandType.ProgramChange:
                                state.Program = cmd.Data1;
                                break;
                            case CommandType.PitchBend:
                                state.PitchBend = (float)((cmd.Data1 | (cmd.Data2 << 7)) - 8192) / 8192.0f * (float)MidiConfiguration.Default.MIDI.PitchBendRange;
                                break;
                            case CommandType.ChannelPressure:
                                state.BreathController = cmd.Data1 / 127.0f;
                                break;
                        }
                    }
                    else if (ev is NoteData note)
                    {
                        if (note.Channel < 1 || note.Channel > 16) continue;
                        long noteStartSampleAbsolute = (long)Math.Round(note.AbsoluteTimeSeconds * synthEngine.SampleRate);
                        long noteDurationSamples = (long)Math.Round(note.DurationSeconds * synthEngine.SampleRate);

                        if (note.IsError)
                        {
                            long samplesToRenderError = Math.Min(noteDurationSamples, totalRenderSamples - noteStartSampleAbsolute);
                            if (samplesToRenderError > 0)
                            {
                                RenderErrorBeep(renderBuffer, (int)noteStartSampleAbsolute, (int)samplesToRenderError, synthEngine.SampleRate, 4, 880);
                            }
                        }
                        else if (note.MidiNoteNumber >= 0)
                        {
                            var state = channelStates[note.Channel];
                            var instrument = synthEngine.GetInstrumentSettings(note.Channel, state.Program, instrumentSettingsDict);

                            state.Modulation = note.Modulation / 127.0f;
                            state.Expression = note.Expression / 127.0f;
                            state.Pan = (note.Pan - 64) / 64.0f;
                            state.PitchBend = (float)(note.PitchBend - 8192) / 8192.0f * (float)MidiConfiguration.Default.MIDI.PitchBendRange;
                            state.BreathController = note.ChannelPressure / 127.0f;

                            long releaseSamples = (long)Math.Round(instrument.Release * synthEngine.SampleRate * state.ReleaseMultiplier);
                            long totalNoteRenderSamples = noteDurationSamples + releaseSamples;

                            var envelope = new ADSREnvelope(
                                instrument.Attack * state.AttackMultiplier,
                                instrument.Decay * state.DecayMultiplier,
                                instrument.Sustain,
                                instrument.Release * state.ReleaseMultiplier,
                                noteDurationSamples, synthEngine.SampleRate, null);


                            for (long i = 0; ; i++)
                            {
                                long currentSampleInNote = i;
                                long currentBufferIndex = noteStartSampleAbsolute + i;

                                if (currentBufferIndex >= totalRenderSamples) break;


                                double time = currentSampleInNote / (double)synthEngine.SampleRate;
                                double freq = synthEngine.GetFrequency(note.MidiNoteNumber, state.PitchBend, 0);
                                float baseAmplitude = (float)Math.Clamp(note.Volume, 0f, 2.0f) * state.Volume * state.Expression;
                                double envelopeValue = envelope.GetValue(currentSampleInNote);


                                if (envelopeValue <= 0.0001 && currentSampleInNote > noteDurationSamples) break;
                                if (currentSampleInNote >= totalNoteRenderSamples) break;


                                float waveValue = synthEngine.GenerateWaveform(instrument.WaveType, freq, time, baseAmplitude, envelopeValue, note.MidiNoteNumber);


                                if (currentBufferIndex < bufferSpan.Length)
                                {
                                    bufferSpan[(int)currentBufferIndex] += waveValue;
                                }
                            }
                        }

                    }
                }


                var finalBuffer = new float[totalSamplesStrict];
                Array.Copy(renderBuffer, finalBuffer, Math.Min(renderBuffer.Length, finalBuffer.Length));

                ApplyFadeOut(finalBuffer.AsSpan(), (int)(FadeOutDurationSeconds * synthEngine.SampleRate));
                return finalBuffer;
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.InternalSynthesisError, ex);
                return Array.Empty<float>();
            }
        }

        private void HandleInternalSynthControlChange(MidiCommandData cmd, ChannelState state)
        {
            switch (cmd.Data1)
            {
                case 1: state.Modulation = cmd.Data2 / 127.0f; break;
                case 7: state.Volume = cmd.Data2 / 127.0f; break;
                case 10: state.Pan = (cmd.Data2 - 64) / 64.0f; break;
                case 11: state.Expression = cmd.Data2 / 127.0f; break;
                case 64: state.Sustain = cmd.Data2 >= 64; break;
                case 71: state.FilterResonanceMultiplier = cmd.Data2 / 64.0; break;
                case 72: state.ReleaseMultiplier = cmd.Data2 / 64.0; break;
                case 73: state.AttackMultiplier = cmd.Data2 / 64.0; break;
                case 74: state.FilterCutoffMultiplier = cmd.Data2 / 64.0; break;
                case 75: state.DecayMultiplier = cmd.Data2 / 64.0; break;

            }
        }

        private void RenderSilence(Synthesizer synthesizer, float[] buffer, int startSample, int numSamples, float[] left, float[] right)
        {
            int samplesRendered = 0;
            while (samplesRendered < numSamples)
            {
                int samplesToRenderInBlock = Math.Min(left.Length, numSamples - samplesRendered);
                if (samplesToRenderInBlock <= 0) break;
                var leftSpan = left.AsSpan(0, samplesToRenderInBlock);
                var rightSpan = right.AsSpan(0, samplesToRenderInBlock);

                try
                {
                    synthesizer.Render(leftSpan, rightSpan);
                }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.SynthesizerRenderError, ex);
                    leftSpan.Clear();
                    rightSpan.Clear();
                }

                for (int i = 0; i < samplesToRenderInBlock; i++)
                {
                    int bufferIndex = startSample + samplesRendered + i;
                    if (bufferIndex < buffer.Length)
                    {
                        buffer[bufferIndex] += (leftSpan[i] + rightSpan[i]) * 0.5f;
                    }
                    else
                    {
                        break;
                    }
                }
                samplesRendered += samplesToRenderInBlock;
            }
        }

        private void RenderNote(Synthesizer synthesizer, float[] buffer, int startSample, int numSamples, float[] left, float[] right)
        {
            RenderSilence(synthesizer, buffer, startSample, numSamples, left, right);
        }

        private void RenderErrorBeep(float[] buffer, int startSample, int numSamples, int sampleRate, int count = 1, int freq = 880, double beepDurationSec = 0.05, double pauseDurationSec = 0.05)
        {
            const float beepVol = 0.5f;
            int beepSamples = (int)(beepDurationSec * sampleRate);
            int pauseSamples = (int)(pauseDurationSec * sampleRate);
            int singleBeepCycle = beepSamples + pauseSamples;

            if (numSamples < (singleBeepCycle * count))
            {
                beepSamples = (int)((double)numSamples / (count * 2));
                pauseSamples = beepSamples;
                singleBeepCycle = beepSamples + pauseSamples;
                if (beepSamples == 0) return;
            }

            int currentSample = 0;
            for (int c = 0; c < count; c++)
            {
                for (int i = 0; i < beepSamples; i++)
                {
                    int bufferIndex = startSample + currentSample;
                    if (bufferIndex >= buffer.Length || bufferIndex >= startSample + numSamples) break;
                    buffer[bufferIndex] += (float)(Math.Sin(2 * Math.PI * freq * (i / (double)sampleRate)) * beepVol);
                    currentSample++;
                }
                currentSample += pauseSamples;
                if (startSample + currentSample >= buffer.Length || startSample + currentSample >= startSample + numSamples) break;
            }
        }

        public float[] GenerateErrorBeeps(int count, double durationSeconds = 0.1, double pauseSeconds = 0.05, int freq = 880)
        {
            int beepSamples = (int)(durationSeconds * SampleRate);
            int pauseSamples = (int)(pauseSeconds * SampleRate);
            int totalSamples = (beepSamples + pauseSamples) * count;
            if (totalSamples == 0) totalSamples = SampleRate / 10;

            var buffer = new float[totalSamples];

            RenderErrorBeep(buffer, 0, totalSamples, SampleRate, count, freq, durationSeconds, pauseSeconds);

            return buffer;
        }

        private void ApplyFadeOut(Span<float> buffer, int fadeLength)
        {
            if (buffer.Length == 0) return;
            fadeLength = Math.Min(fadeLength, buffer.Length);
            int startFadeIndex = buffer.Length - fadeLength;

            for (int i = 0; i < fadeLength; i++)
            {
                float fade = 1.0f - (float)i / fadeLength;

                float smoothedFade = (float)(0.5 * (1.0 - Math.Cos(Math.PI * fade)));
                buffer[startFadeIndex + i] *= smoothedFade;
            }
        }


        private float GetMaxReleaseTime(List<NoteData> notes, Synthesizer synthesizer)
        {
            return 1.0f;
        }

        private float GetMaxReleaseTime(List<NoteData> notes, SynthesisEngine synthEngine)
        {
            return Math.Max(0.001f, (float)(_model.InternalSynthSettings?.Release ?? 0.2));
        }

        private MidiConfiguration CreateMidiConfigFromVoiceModel(VoiceModel voiceModel)
        {
            try
            {
                var midiConfig = MidiConfiguration.Default.Clone();
                midiConfig.Audio.SampleRate = SampleRate;

                if (voiceModel.ModelType == ModelType.InternalSynth && voiceModel.InternalSynthSettings != null)
                {
                    var synthSettings = voiceModel.InternalSynthSettings;
                    midiConfig.Synthesis.DefaultWaveform = synthSettings.DefaultWaveform;
                    midiConfig.Synthesis.DefaultAttack = synthSettings.Attack;
                    midiConfig.Synthesis.DefaultDecay = synthSettings.Decay;
                    midiConfig.Synthesis.DefaultSustain = synthSettings.Sustain;
                    midiConfig.Synthesis.DefaultRelease = synthSettings.Release;
                    midiConfig.Synthesis.EnableBandlimitedSynthesis = synthSettings.EnableBandlimited;
                    midiConfig.SoundFont.EnableSoundFont = false;
                    midiConfig.SFZ.EnableSfz = false;
                }
                else if (voiceModel.ModelType == ModelType.SoundFont)
                {
                    midiConfig.SoundFont.EnableSoundFont = true;
                    midiConfig.SFZ.EnableSfz = false;
                    midiConfig.SoundFont.Layers.Clear();
                    foreach (var layer in voiceModel.Layers)
                    {
                        midiConfig.SoundFont.Layers.Add((SoundFontLayer)layer.Clone());
                    }
                }
                else
                {
                    midiConfig.SoundFont.EnableSoundFont = false;
                    midiConfig.SFZ.EnableSfz = false;
                }

                midiConfig.Synthesis.A4Frequency = 440.0;
                midiConfig.Synthesis.MinFrequency = 20.0;
                midiConfig.Synthesis.MaxFrequency = 20000.0;
                return midiConfig;
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.MidiConfigCreationError, ex);
                return MidiConfiguration.Default;
            }
        }
    }
}