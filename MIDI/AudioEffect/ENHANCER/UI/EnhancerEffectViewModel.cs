using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using YukkuriMovieMaker.Commons;
using MIDI.AudioEffect.ENHANCER.Services;
using MIDI.AudioEffect.ENHANCER.Models;

namespace MIDI.AudioEffect.ENHANCER.UI
{
    internal class EnhancerEffectViewModel : Bindable, IDisposable
    {
        private EnhancerAudioEffect? effectItem;
        private readonly EnhancerPresetService presetService;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private IEnumerable<string> categories = [];
        public IEnumerable<string> Categories { get => categories; set => Set(ref categories, value); }

        private IEnumerable<EnhancerPreset> presets = [];
        public IEnumerable<EnhancerPreset> Presets { get => presets; set => Set(ref presets, value); }

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

        private EnhancerPreset? selectedPreset;
        public EnhancerPreset? SelectedPreset
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

        public EnhancerAudioEffect? EffectItem
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

        public EnhancerEffectViewModel()
        {
            presetService = new EnhancerPresetService();
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

            if (e.PropertyName == nameof(EnhancerAudioEffect.SelectedPresetCategory))
            {
                var loadedCategory = EffectItem.SelectedPresetCategory;
                if (Categories.Contains(loadedCategory))
                {
                    SelectedCategory = loadedCategory;
                }
            }
            else if (e.PropertyName == nameof(EnhancerAudioEffect.SelectedPreset))
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
            EffectItem.Drive = SelectedPreset.Drive;
            EffectItem.Frequency = SelectedPreset.Frequency;
            EffectItem.Mix = SelectedPreset.Mix;
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