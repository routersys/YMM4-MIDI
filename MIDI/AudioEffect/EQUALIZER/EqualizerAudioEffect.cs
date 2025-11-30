using MIDI.AudioEffect.EQUALIZER.Models;
using MIDI.AudioEffect.EQUALIZER.Views;
using MIDI.Configuration.Models;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace MIDI.AudioEffect.EQUALIZER
{
    [AudioEffect("EQUALIZER", ["MIDI"], ["イコライザー"], IsAviUtlSupported = false)]
    public class EqualizerAudioEffect : AudioEffectBase
    {
        public override string Label => "EQUALIZER";

        [Display(GroupName = "Equalizer", Name = "")]
        [EqualizerEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public ObservableCollection<EQBand> Bands { get; } = new();

        public EqualizerAudioEffect()
        {
            LoadDefaultPreset();
        }

        private void LoadDefaultPreset()
        {
            var defaultPreset = EqualizerSettings.Default.DefaultPreset;
            if (!string.IsNullOrEmpty(defaultPreset))
            {
                var loadedBands = ServiceLocator.PresetService.LoadPreset(defaultPreset);
                if (loadedBands != null)
                {
                    Bands.Clear();
                    foreach (var band in loadedBands)
                    {
                        Bands.Add(band);
                    }
                    return;
                }
            }

            if (Bands.Count == 0)
            {
                Bands.Add(new EQBand(true, MIDI.AudioEffect.EQUALIZER.Models.FilterType.Peak, 500, 0, 1.0, StereoMode.Stereo, "バンド 1"));
            }
        }

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new EqualizerProcessor(this, duration);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => Bands;
        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];
    }
}