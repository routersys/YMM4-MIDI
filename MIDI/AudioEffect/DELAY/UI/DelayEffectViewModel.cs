using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using MIDI.AudioEffect.DELAY.Services;
using MIDI.AudioEffect.DELAY.Models;

namespace MIDI.AudioEffect.DELAY.UI
{
    internal class DelayEffectViewModel : Bindable, IDisposable
    {
        private DelayAudioEffect? effectItem;
        private readonly PresetService presetService;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private IEnumerable<string> categories = [];
        public IEnumerable<string> Categories { get => categories; set => Set(ref categories, value); }

        private IEnumerable<DelayPreset> presets = [];
        public IEnumerable<DelayPreset> Presets { get => presets; set => Set(ref presets, value); }

        private string selectedCategory = string.Empty;
        public string SelectedCategory
        {
            get => selectedCategory;
            set
            {
                if (Set(ref selectedCategory, value))
                {
                    OnCategoryChanged();
                }
            }
        }

        private DelayPreset? selectedPreset;
        public DelayPreset? SelectedPreset
        {
            get => selectedPreset;
            set
            {
                if (Set(ref selectedPreset, value))
                {
                    OnPresetChanged();
                }
            }
        }

        private double inputLevel = -60;
        public double InputLevel { get => inputLevel; set => Set(ref inputLevel, value); }

        private double outputLevel = -60;
        public double OutputLevel { get => outputLevel; set => Set(ref outputLevel, value); }

        private double vmInputLevel = -60.0;
        private double vmOutputLevel = -60.0;

        private const double DecayRatePerSecond = 60.0;
        private const double UpdateIntervalSeconds = 0.033;
        private const double DecayPerTick = DecayRatePerSecond * UpdateIntervalSeconds;

        public DelayAudioEffect? EffectItem
        {
            get => effectItem;
            set
            {
                if (effectItem != null)
                {
                    effectItem.PropertyChanged -= Item_PropertyChanged;
                }
                Set(ref effectItem, value);
                if (effectItem != null)
                {
                    effectItem.PropertyChanged += Item_PropertyChanged;
                    LoadDataFromEffect();
                }
            }
        }

        public DelayEffectViewModel()
        {
            presetService = new PresetService();
            Categories = presetService.GetCategories();
            SelectedCategory = Categories.FirstOrDefault() ?? string.Empty;
        }

        private void LoadDataFromEffect()
        {
            if (EffectItem == null) return;

            var loadedCategory = EffectItem.SelectedPresetCategory;
            if (Categories.Contains(loadedCategory))
            {
                SelectedCategory = loadedCategory;
            }
            else
            {
                SelectedCategory = Categories.FirstOrDefault() ?? string.Empty;
            }

            var loadedPresetName = EffectItem.SelectedPreset;
            SelectedPreset = Presets.FirstOrDefault(p => p.Name == loadedPresetName) ?? Presets.FirstOrDefault();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (EffectItem == null) return;

            if (e.PropertyName == nameof(DelayAudioEffect.SelectedPresetCategory))
            {
                var loadedCategory = EffectItem.SelectedPresetCategory;
                if (Categories.Contains(loadedCategory))
                {
                    SelectedCategory = loadedCategory;
                }
            }
            else if (e.PropertyName == nameof(DelayAudioEffect.SelectedPreset))
            {
                var loadedPresetName = EffectItem.SelectedPreset;
                SelectedPreset = Presets.FirstOrDefault(p => p.Name == loadedPresetName) ?? Presets.FirstOrDefault();
            }
        }

        private void OnCategoryChanged()
        {
            Presets = presetService.GetPresets(SelectedCategory);
            SelectedPreset = Presets.FirstOrDefault();
            if (EffectItem != null)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                EffectItem.SelectedPresetCategory = SelectedCategory;
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnPresetChanged()
        {
            if (EffectItem == null || SelectedPreset == null) return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            EffectItem.SelectedPreset = SelectedPreset.Name;
            EffectItem.DelayTime = SelectedPreset.DelayTimeMs;
            EffectItem.Feedback = SelectedPreset.Feedback;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateLevels()
        {
            if (EffectItem == null) return;

            double processorInputPeak = EffectItem.InputLevel;
            double processorOutputPeak = EffectItem.OutputLevel;

            if (processorInputPeak > vmInputLevel)
            {
                vmInputLevel = processorInputPeak;
            }
            else
            {
                vmInputLevel = Math.Max(-60.0, vmInputLevel - DecayPerTick);
            }

            if (processorOutputPeak > vmOutputLevel)
            {
                vmOutputLevel = processorOutputPeak;
            }
            else
            {
                vmOutputLevel = Math.Max(-60.0, vmOutputLevel - DecayPerTick);
            }

            InputLevel = vmInputLevel;
            OutputLevel = vmOutputLevel;
        }

        public void Dispose()
        {
            if (EffectItem != null)
            {
                EffectItem.PropertyChanged -= Item_PropertyChanged;
            }
        }
    }
}