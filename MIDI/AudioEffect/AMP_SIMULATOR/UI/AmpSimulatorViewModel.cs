using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using YukkuriMovieMaker.Commons;
using MIDI.AudioEffect.AMP_SIMULATOR.Services;
using MIDI.AudioEffect.AMP_SIMULATOR.Models;

namespace MIDI.AudioEffect.AMP_SIMULATOR.UI
{
    internal class AmpSimulatorViewModel : Bindable, IDisposable
    {
        private AmpSimulatorAudioEffect? effectItem;
        private readonly AmpPresetService presetService;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private IEnumerable<string> categories = [];
        public IEnumerable<string> Categories { get => categories; set => Set(ref categories, value); }

        private IEnumerable<AmpPreset> presets = [];
        public IEnumerable<AmpPreset> Presets { get => presets; set => Set(ref presets, value); }

        private string selectedCategory = string.Empty;
        public string SelectedCategory
        {
            get => selectedCategory;
            set
            {
                if (Set(ref selectedCategory, value)) OnCategoryChanged();
            }
        }

        private AmpPreset? selectedPreset;
        public AmpPreset? SelectedPreset
        {
            get => selectedPreset;
            set
            {
                if (Set(ref selectedPreset, value)) OnPresetChanged();
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

        public AmpSimulatorAudioEffect? EffectItem
        {
            get => effectItem;
            set
            {
                if (effectItem != null) effectItem.PropertyChanged -= Item_PropertyChanged;
                Set(ref effectItem, value);
                if (effectItem != null)
                {
                    effectItem.PropertyChanged += Item_PropertyChanged;
                    LoadDataFromEffect();
                }
            }
        }

        public AmpSimulatorViewModel()
        {
            presetService = new AmpPresetService();
            Categories = presetService.GetCategories();
            SelectedCategory = Categories.FirstOrDefault() ?? string.Empty;
        }

        private void LoadDataFromEffect()
        {
            if (EffectItem == null) return;
            var cat = EffectItem.SelectedPresetCategory;
            SelectedCategory = Categories.Contains(cat) ? cat : (Categories.FirstOrDefault() ?? "");
            var pre = EffectItem.SelectedPreset;
            SelectedPreset = Presets.FirstOrDefault(p => p.Name == pre) ?? Presets.FirstOrDefault();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (EffectItem == null) return;
            if (e.PropertyName == nameof(AmpSimulatorAudioEffect.SelectedPresetCategory))
            {
                if (Categories.Contains(EffectItem.SelectedPresetCategory))
                    SelectedCategory = EffectItem.SelectedPresetCategory;
            }
            else if (e.PropertyName == nameof(AmpSimulatorAudioEffect.SelectedPreset))
            {
                SelectedPreset = Presets.FirstOrDefault(p => p.Name == EffectItem.SelectedPreset) ?? Presets.FirstOrDefault();
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
            EffectItem.InputGain = SelectedPreset.InputGain;
            EffectItem.Bass = SelectedPreset.Bass;
            EffectItem.Middle = SelectedPreset.Middle;
            EffectItem.Treble = SelectedPreset.Treble;
            EffectItem.Presence = SelectedPreset.Presence;
            EffectItem.MasterVolume = SelectedPreset.MasterVolume;
            EffectItem.Sag = SelectedPreset.Sag;
            EffectItem.Bias = SelectedPreset.Bias;
            EffectItem.CabinetResonance = SelectedPreset.CabinetResonance;
            EffectItem.CabinetBright = SelectedPreset.CabinetBright;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateLevels()
        {
            if (EffectItem == null) return;

            double processorInputPeak = EffectItem.InputLevel;
            double processorOutputPeak = EffectItem.OutputLevel;

            if (processorInputPeak > vmInputLevel) vmInputLevel = processorInputPeak;
            else vmInputLevel = Math.Max(-60.0, vmInputLevel - DecayPerTick);

            if (processorOutputPeak > vmOutputLevel) vmOutputLevel = processorOutputPeak;
            else vmOutputLevel = Math.Max(-60.0, vmOutputLevel - DecayPerTick);

            InputLevel = vmInputLevel;
            OutputLevel = vmOutputLevel;
        }

        public void Dispose()
        {
            if (EffectItem != null) EffectItem.PropertyChanged -= Item_PropertyChanged;
        }
    }
}