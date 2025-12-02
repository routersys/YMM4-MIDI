using MIDI.AudioEffect.EQUALIZER.Models;
using MIDI.AudioEffect.EQUALIZER.Services;
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
using System;
using System.Text.Json.Serialization;
using System.Linq;

namespace MIDI.AudioEffect.EQUALIZER
{
    [AudioEffect("EQUALIZER", ["MIDI"], ["イコライザー"], IsAviUtlSupported = false)]
    public class EqualizerAudioEffect : AudioEffectBase
    {
        public override string Label => "EQUALIZER";

        private EQBand[] _items = new EQBand[32];

        public EQBand[] Items
        {
            get => _items;
            set
            {
                if (_items != value)
                {
                    if (value == null || value.Length == 0)
                    {
                        _items = new EQBand[32];
                        for (int i = 0; i < 32; i++) _items[i] = new EQBand { IsUsed = false };
                    }
                    else if (value.Length < 32)
                    {
                        _items = new EQBand[32];
                        Array.Copy(value, _items, value.Length);
                        for (int i = value.Length; i < 32; i++) _items[i] = new EQBand { IsUsed = false };
                    }
                    else
                    {
                        _items = value;
                    }

                    UpdateBandsCollection();
                    OnPropertyChanged(nameof(Items));
                }
            }
        }

        [JsonIgnore]
        [Display(GroupName = "Equalizer", Name = "")]
        [EqualizerEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public ObservableCollection<EQBand> Bands { get; } = new();

        [JsonIgnore]
        public double CurrentProgress { get => _currentProgress; set => Set(ref _currentProgress, value); }
        private double _currentProgress = 0.0;

        public EqualizerAudioEffect()
        {
            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i] == null)
                    _items[i] = new EQBand { IsUsed = false, Header = $"バンド {i + 1}" };
            }

            Bands.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Bands));

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
                    ApplyBands(loadedBands);
                    return;
                }
            }

            ResetAllBands();
            var b = Items[0];
            b.IsUsed = true;
            b.IsEnabled = true;
            b.Type = (Models.FilterType)FilterType.Peak;
            b.Frequency.Values[0].Value = 500;
            b.Gain.Values[0].Value = 0;
            b.Q.Values[0].Value = 1.0;

            UpdateBandsCollection();
        }

        public void ApplyBands(IEnumerable<EQBand> sourceBands)
        {
            ResetAllBands();
            int index = 0;
            foreach (var src in sourceBands)
            {
                if (index >= Items.Length) break;

                var target = Items[index];
                target.IsUsed = true;
                target.CopyFrom(src);
                index++;
            }
            UpdateBandsCollection();
        }

        private void ResetAllBands()
        {
            foreach (var item in Items)
            {
                if (item != null)
                {
                    item.IsUsed = false;
                }
            }
        }

        public void UpdateBandsCollection()
        {
            Bands.Clear();
            foreach (var item in Items)
            {
                if (item != null && item.IsUsed)
                {
                    Bands.Add(item);
                }
            }
            OnPropertyChanged(nameof(Bands));
        }

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new EqualizerProcessor(this, duration);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => Items.Where(x => x != null);

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];
    }
}