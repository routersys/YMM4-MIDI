using Microsoft.Win32;
using MIDI.AudioEffect.SPATIAL.Models;
using MIDI.AudioEffect.SPATIAL.Services;
using MIDI.TextCompletion.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace MIDI.AudioEffect.SPATIAL.UI
{
    internal class SpatialAudioEffectViewModel : Bindable, IDisposable
    {
        private SpatialAudioEffect? effectItem;
        private readonly SpatialPresetService presetService;
        private readonly IrStatusService statusService;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private IEnumerable<string> categories = [];
        public IEnumerable<string> Categories { get => categories; set => Set(ref categories, value); }

        private IEnumerable<SpatialPreset> presets = [];
        public IEnumerable<SpatialPreset> Presets { get => presets; set => Set(ref presets, value); }

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

        private SpatialPreset? selectedPreset;
        public SpatialPreset? SelectedPreset
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

        private string statusMessage = "";
        public string StatusMessage { get => statusMessage; set => Set(ref statusMessage, value); }

        private double inputLevel = -60;
        public double InputLevel { get => inputLevel; set => Set(ref inputLevel, value); }

        private double outputLevel = -60;
        public double OutputLevel { get => outputLevel; set => Set(ref outputLevel, value); }

        private double vmInputLevel = -60.0;
        private double vmOutputLevel = -60.0;

        private const double DecayRatePerSecond = 60.0;
        private const double UpdateIntervalSeconds = 0.033;
        private const double DecayPerTick = DecayRatePerSecond * UpdateIntervalSeconds;

        public ICommand BrowseLeftCommand { get; }
        public ICommand BrowseRightCommand { get; }

        public SpatialAudioEffect? EffectItem
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
                    UpdateStatus();
                }
            }
        }

        public SpatialAudioEffectViewModel()
        {
            presetService = new SpatialPresetService();
            statusService = new IrStatusService();
            Categories = presetService.GetCategories();
            SelectedCategory = Categories.FirstOrDefault() ?? string.Empty;

            BrowseLeftCommand = new DelegateCommand(BrowseLeft);
            BrowseRightCommand = new DelegateCommand(BrowseRight);
        }

        private void BrowseLeft()
        {
            if (EffectItem == null) return;
            var dialog = new OpenFileDialog
            {
                Filter = "Audio Files|*.wav;*.mp3;*.aiff;*.flac|All Files|*.*",
                Title = "Select Left / Stereo IR"
            };
            if (dialog.ShowDialog() == true)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                EffectItem.IrFileLeft = dialog.FileName;
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }

        private void BrowseRight()
        {
            if (EffectItem == null) return;
            var dialog = new OpenFileDialog
            {
                Filter = "Audio Files|*.wav;*.mp3;*.aiff;*.flac|All Files|*.*",
                Title = "Select Right IR"
            };
            if (dialog.ShowDialog() == true)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                EffectItem.IrFileRight = dialog.FileName;
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
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

            if (e.PropertyName == nameof(SpatialAudioEffect.SelectedPresetCategory))
            {
                var loadedCategory = EffectItem.SelectedPresetCategory;
                if (Categories.Contains(loadedCategory))
                {
                    SelectedCategory = loadedCategory;
                }
            }
            else if (e.PropertyName == nameof(SpatialAudioEffect.SelectedPreset))
            {
                var loadedPresetName = EffectItem.SelectedPreset;
                SelectedPreset = Presets.FirstOrDefault(p => p.Name == loadedPresetName) ?? Presets.FirstOrDefault();
            }
            else if (e.PropertyName == nameof(SpatialAudioEffect.IrFileLeft) || e.PropertyName == nameof(SpatialAudioEffect.IrFileRight))
            {
                UpdateStatus();
                OnPropertyChanged(nameof(EffectItem));
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
            if (!string.IsNullOrEmpty(SelectedPreset.IrFileLeft)) EffectItem.IrFileLeft = SelectedPreset.IrFileLeft;
            if (!string.IsNullOrEmpty(SelectedPreset.IrFileRight)) EffectItem.IrFileRight = SelectedPreset.IrFileRight;
            EffectItem.Gain = SelectedPreset.Gain;
            EffectItem.Mix = SelectedPreset.Mix;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateStatus()
        {
            if (EffectItem == null) return;
            var result = statusService.ValidateFiles(EffectItem.IrFileLeft, EffectItem.IrFileRight);
            StatusMessage = result.Message;
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