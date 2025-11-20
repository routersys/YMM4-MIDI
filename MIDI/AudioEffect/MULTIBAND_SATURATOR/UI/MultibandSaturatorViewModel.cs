using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using YukkuriMovieMaker.Commons;
using MIDI.AudioEffect.MULTIBAND_SATURATOR.Services;
using MIDI.AudioEffect.MULTIBAND_SATURATOR.Models;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.UI
{
    internal class MultibandSaturatorViewModel : Bindable, IDisposable
    {
        private MultibandSaturatorAudioEffect? effectItem;
        private readonly MultibandSaturatorPresetService presetService;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private IEnumerable<string> categories = [];
        public IEnumerable<string> Categories { get => categories; set => Set(ref categories, value); }

        private IEnumerable<MultibandSaturatorPreset> presets = [];
        public IEnumerable<MultibandSaturatorPreset> Presets { get => presets; set => Set(ref presets, value); }

        private string selectedCategory = string.Empty;
        public string SelectedCategory
        {
            get => selectedCategory;
            set
            {
                if (Set(ref selectedCategory, value)) OnCategoryChanged();
            }
        }

        private MultibandSaturatorPreset? selectedPreset;
        public MultibandSaturatorPreset? SelectedPreset
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

        private double lowMeter = -60;
        public double LowMeter { get => lowMeter; set => Set(ref lowMeter, value); }

        private double midMeter = -60;
        public double MidMeter { get => midMeter; set => Set(ref midMeter, value); }

        private double highMeter = -60;
        public double HighMeter { get => highMeter; set => Set(ref highMeter, value); }

        private double vmIn = -60, vmOut = -60, vmL = -60, vmM = -60, vmH = -60;
        private const double DecayRate = 2.0;

        public MultibandSaturatorAudioEffect? EffectItem
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

        public MultibandSaturatorViewModel()
        {
            presetService = new MultibandSaturatorPresetService();
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
            if (e.PropertyName == nameof(MultibandSaturatorAudioEffect.SelectedPresetCategory))
            {
                if (Categories.Contains(EffectItem.SelectedPresetCategory))
                    SelectedCategory = EffectItem.SelectedPresetCategory;
            }
            else if (e.PropertyName == nameof(MultibandSaturatorAudioEffect.SelectedPreset))
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
            EffectItem.FreqLowMid = SelectedPreset.FreqLowMid;
            EffectItem.FreqMidHigh = SelectedPreset.FreqMidHigh;
            EffectItem.LowDrive = SelectedPreset.LowDrive;
            EffectItem.LowLevel = SelectedPreset.LowLevel;
            EffectItem.MidDrive = SelectedPreset.MidDrive;
            EffectItem.MidLevel = SelectedPreset.MidLevel;
            EffectItem.HighDrive = SelectedPreset.HighDrive;
            EffectItem.HighLevel = SelectedPreset.HighLevel;
            EffectItem.MasterMix = SelectedPreset.MasterMix;
            EffectItem.MasterGain = SelectedPreset.MasterGain;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateLevels()
        {
            if (EffectItem == null) return;
            UpdateMeter(ref vmIn, EffectItem.InputLevel); InputLevel = vmIn;
            UpdateMeter(ref vmOut, EffectItem.OutputLevel); OutputLevel = vmOut;
            UpdateMeter(ref vmL, EffectItem.LowMeter); LowMeter = vmL;
            UpdateMeter(ref vmM, EffectItem.MidMeter); MidMeter = vmM;
            UpdateMeter(ref vmH, EffectItem.HighMeter); HighMeter = vmH;
        }

        private void UpdateMeter(ref double current, double target)
        {
            if (target > current) current = target;
            else current = Math.Max(-60, current - DecayRate);
        }

        public void Dispose()
        {
            if (EffectItem != null) EffectItem.PropertyChanged -= Item_PropertyChanged;
        }
    }
}