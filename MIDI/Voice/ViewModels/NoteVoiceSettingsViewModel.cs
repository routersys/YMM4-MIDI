using Microsoft.Win32;
using MIDI.Configuration.Models;
using MIDI.UI.Commands;
using MIDI.UI.ViewModels.MidiEditor;
using MIDI.UI.ViewModels.Models;
using MIDI.UI.ViewModels.Services;
using MIDI.Utils;
using MIDI.Voice.Models;
using MIDI.Voice.Services;
using MIDI.Voice.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static System.Net.WebRequestMethods;
using File = System.IO.File;

namespace MIDI.Voice.ViewModels
{
    public class NoteVoiceSettingsViewModel : ViewModelBase
    {
        private readonly NoteVoiceSettings _settings;
        public VoiceSynthSettings SynthSettings => _settings.SynthSettings;

        public ObservableCollection<VoiceModel> VoiceModels => _settings.SynthSettings.VoiceModels;
        public ObservableCollection<SoundFontLayer> SoundFontLayers { get; } = new();

        public List<string> WaveformOptions { get; } = new List<string> { "Sine", "Square", "Sawtooth", "Triangle", "Noise", "Organ" };
        public List<int> SampleRateOptions { get; } = new List<int> { 22050, 44100, 48000, 96000 };
        public ObservableCollection<SoundFontFileViewModel> AvailableSoundFonts { get; } = new();
        public ObservableCollection<UtauVoiceViewModel> AvailableUtauVoices { get; } = new();

        private SoundFontFileViewModel? _selectedAvailableSoundFont;
        public SoundFontFileViewModel? SelectedAvailableSoundFont
        {
            get => _selectedAvailableSoundFont;
            set => SetField(ref _selectedAvailableSoundFont, value);
        }

        private UtauVoiceViewModel? _selectedUtauVoice;
        public UtauVoiceViewModel? SelectedUtauVoice
        {
            get => _selectedUtauVoice;
            set
            {
                if (SetField(ref _selectedUtauVoice, value))
                {
                    if (SelectedModel != null && SelectedModel.ModelType == ModelType.UTAU)
                    {
                        SelectedModel.UtauVoicePath = value?.BasePath;
                    }
                }
            }
        }

        private SoundFontLayer? _selectedLayer;
        public SoundFontLayer? SelectedLayer
        {
            get => _selectedLayer;
            set => SetField(ref _selectedLayer, value);
        }

        private VoiceModel? _selectedModel;
        public VoiceModel? SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (SetField(ref _selectedModel, value) && value != null)
                {
                    SynthSettings.CurrentModelName = value.Name;
                    if (value.ModelType == ModelType.SoundFont)
                    {
                        LoadLayersFromModel(value);
                        IsSoundFontSettingsExpanded = true;
                    }
                    else
                    {
                        SoundFontLayers.Clear();
                        IsSoundFontSettingsExpanded = false;
                    }

                    if (value.ModelType == ModelType.UTAU)
                    {
                        _ = LoadAvailableUtauVoicesAsync(value.UtauVoicePath);
                        IsUtauSettingsExpanded = true;
                    }
                    else
                    {
                        AvailableUtauVoices.Clear();
                        IsUtauSettingsExpanded = false;
                    }

                    if (value.ModelType == ModelType.InternalSynth)
                    {
                        IsInternalSynthSettingsExpanded = true;
                    }
                    else
                    {
                        IsInternalSynthSettingsExpanded = false;
                    }

                    OnPropertyChanged(nameof(IsSoundFontModelSelected));
                    OnPropertyChanged(nameof(IsInternalSynthModelSelected));
                    OnPropertyChanged(nameof(IsUtauModelSelected));
                    RaiseCanExecuteChangedCommands();
                }
            }
        }

        public bool IsSoundFontModelSelected => SelectedModel?.ModelType == ModelType.SoundFont;
        public bool IsInternalSynthModelSelected => SelectedModel?.ModelType == ModelType.InternalSynth;
        public bool IsUtauModelSelected => SelectedModel?.ModelType == ModelType.UTAU;


        public ICommand BrowseSoundFontCommand { get; }
        public ICommand AddLayerCommand { get; }
        public ICommand RemoveLayerCommand { get; }
        public ICommand MoveLayerUpCommand { get; }
        public ICommand MoveLayerDownCommand { get; }
        public ICommand CreateNewModelCommand { get; }
        public ICommand SaveCurrentModelCommand { get; }
        public ICommand DeleteModelCommand { get; }
        public ICommand RefreshFilesCommand { get; }
        public ICommand EditModelNameCommand { get; }

        public ICommand RefreshUtauVoicesCommand { get; }


        private readonly IFileService _fileService;
        private readonly UtauVoiceService _utauVoiceService;

        private bool _isUtauSettingsExpanded = true;
        public bool IsUtauSettingsExpanded { get => _isUtauSettingsExpanded; set => SetField(ref _isUtauSettingsExpanded, value); }

        private bool _isSoundFontSettingsExpanded = true;
        public bool IsSoundFontSettingsExpanded { get => _isSoundFontSettingsExpanded; set => SetField(ref _isSoundFontSettingsExpanded, value); }

        private bool _isInternalSynthSettingsExpanded = true;
        public bool IsInternalSynthSettingsExpanded { get => _isInternalSynthSettingsExpanded; set => SetField(ref _isInternalSynthSettingsExpanded, value); }

        private bool _isLoadingUtauVoices = false;
        public bool IsLoadingUtauVoices
        {
            get => _isLoadingUtauVoices;
            set => SetField(ref _isLoadingUtauVoices, value);
        }


        public NoteVoiceSettingsViewModel(NoteVoiceSettings settings)
        {
            _settings = settings;
            _fileService = new FileService(MidiConfiguration.Default);
            _utauVoiceService = new UtauVoiceService();

            BrowseSoundFontCommand = new RelayCommand(_ => BrowseSoundFont(), _ => IsSoundFontModelSelected);
            AddLayerCommand = new RelayCommand(AddLayer, CanAddLayer);
            RemoveLayerCommand = new RelayCommand(RemoveLayer, CanRemoveLayer);
            MoveLayerUpCommand = new RelayCommand(MoveLayerUp, CanMoveLayerUp);
            MoveLayerDownCommand = new RelayCommand(MoveLayerDown, CanMoveLayerDown);
            CreateNewModelCommand = new RelayCommand(ShowCreateNewModelWindow);
            SaveCurrentModelCommand = new RelayCommand(SaveCurrentModel, CanSaveCurrentModel);
            DeleteModelCommand = new RelayCommand(DeleteModel, CanDeleteModel);
            RefreshFilesCommand = new RelayCommand(async _ => await LoadAvailableSoundFontsAsync());
            EditModelNameCommand = new RelayCommand(EditModelName, CanEditModelName);

            RefreshUtauVoicesCommand = new RelayCommand(async _ => await LoadAvailableUtauVoicesAsync(SelectedModel?.UtauVoicePath), _ => IsUtauModelSelected);

            _ = LoadAvailableSoundFontsAsync();
            InitializeModelsAndLayers();
            SynthSettings.PropertyChanged += OnSynthSettingsPropertyChanged;

            SynthSettings.UtauVoiceBaseFolders.CollectionChanged += (s, e) => {
                try
                {
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to save UTAU folders after change.", ex);
                }
            };

            VoiceModels.CollectionChanged += VoiceModels_CollectionChanged;
            foreach (var model in VoiceModels)
            {
                if (model == null) continue;
                model.PropertyChanged += Model_PropertyChanged;
            }
            SoundFontLayers.CollectionChanged += (s, e) => SaveCurrentModel(null);
        }

        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VoiceModel.Name))
            {
                _settings.Save();
            }
        }


        private void VoiceModels_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (VoiceModel item in e.NewItems.OfType<VoiceModel>())
                {
                    if (item == null) continue;
                    item.PropertyChanged += Model_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (VoiceModel item in e.OldItems.OfType<VoiceModel>())
                {
                    if (item == null) continue;
                    item.PropertyChanged -= Model_PropertyChanged;
                }
            }
            _settings.Save();
            RaiseCanExecuteChangedCommands();
        }


        private void OnSynthSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VoiceSynthSettings.CurrentModelName))
            {
                SelectedModel = VoiceModels.FirstOrDefault(m => m != null && m.Name == SynthSettings.CurrentModelName);
            }
        }

        private void InitializeModelsAndLayers()
        {
            var currentModel = VoiceModels.FirstOrDefault(m => m != null && m.Name == SynthSettings.CurrentModelName);
            if (currentModel == null)
            {
                currentModel = VoiceModels.FirstOrDefault(m => m != null);
                if (currentModel != null)
                {
                    SynthSettings.CurrentModelName = currentModel.Name;
                }
            }

            SelectedModel = currentModel;

            if (SelectedModel?.ModelType == ModelType.SoundFont)
            {
                LoadLayersFromModel(SelectedModel);
            }
            else
            {
                SoundFontLayers.Clear();
            }

            if (SelectedModel?.ModelType == ModelType.UTAU)
            {
                _ = LoadAvailableUtauVoicesAsync(SelectedModel.UtauVoicePath);
            }
            else
            {
                AvailableUtauVoices.Clear();
            }
        }


        private void LoadLayersFromModel(VoiceModel model)
        {
            if (model.ModelType != ModelType.SoundFont)
            {
                Application.Current?.Dispatcher?.Invoke(() => SoundFontLayers.Clear());
                return;
            }

            var layersToLoad = model.Layers.Select(l => (SoundFontLayer)l.Clone()).ToList();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                SoundFontLayers.Clear();
                foreach (var layer in layersToLoad)
                {
                    SoundFontLayers.Add(layer);
                }
                RaiseCanExecuteChangedCommands();
            });
        }

        private async Task LoadAvailableSoundFontsAsync()
        {
            try
            {
                var files = await _fileService.GetSoundFontFilesAsync();
                var newFiles = files.Where(f => !f.IsMissing).ToList();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var currentFiles = AvailableSoundFonts.ToList();
                    var filesToAdd = newFiles.Where(nf => !currentFiles.Any(cf => cf.FileName == nf.FileName)).ToList();
                    var filesToRemove = currentFiles.Where(cf => !newFiles.Any(nf => nf.FileName == cf.FileName)).ToList();

                    foreach (var toRemove in filesToRemove)
                    {
                        AvailableSoundFonts.Remove(toRemove);
                    }
                    foreach (var toAdd in filesToAdd)
                    {
                        AvailableSoundFonts.Add(toAdd);
                    }

                    if (SelectedAvailableSoundFont == null && AvailableSoundFonts.Any())
                    {
                        SelectedAvailableSoundFont = AvailableSoundFonts.FirstOrDefault();
                    }
                    else if (SelectedAvailableSoundFont != null && !AvailableSoundFonts.Contains(SelectedAvailableSoundFont))
                    {
                        SelectedAvailableSoundFont = AvailableSoundFonts.FirstOrDefault();
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.FailedToLoadSoundFonts, ex);
                MessageBox.Show($"SoundFontファイルの読み込みに失敗しました: {ex.Message}", Translate.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RaiseCanExecuteChangedCommands();
            }
        }

        private async Task LoadAvailableUtauVoicesAsync(string? selectedVoicePath = null)
        {
            if (SelectedModel?.ModelType != ModelType.UTAU)
            {
                Application.Current?.Dispatcher?.Invoke(AvailableUtauVoices.Clear);
                return;
            }

            IsLoadingUtauVoices = true;
            await Task.Yield();

            List<UtauVoiceViewModel> newVms;
            try
            {
                var folders = SynthSettings.UtauVoiceBaseFolders.ToList();
                var voiceInfos = await _utauVoiceService.LoadUtauVoicesAsync(folders);

                newVms = await Task.Run(() =>
                    voiceInfos.Select(info => new UtauVoiceViewModel(info)).ToList()
                );
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load UTAU voices.", ex);
                MessageBox.Show($"UTAU音源の読み込みに失敗しました: {ex.Message}", Translate.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current?.Dispatcher?.Invoke(AvailableUtauVoices.Clear);
                IsLoadingUtauVoices = false;
                RaiseCanExecuteChangedCommands();
                return;
            }

            try
            {
                var currentSelectedPath = selectedVoicePath;

                AvailableUtauVoices.Clear();
                foreach (var vm in newVms)
                {
                    AvailableUtauVoices.Add(vm);
                }

                if (currentSelectedPath != null)
                {
                    SelectedUtauVoice = AvailableUtauVoices.FirstOrDefault(vm => vm.BasePath == currentSelectedPath);
                }
                if (SelectedUtauVoice == null)
                {
                    SelectedUtauVoice = AvailableUtauVoices.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update UTAU voice list in UI.", ex);
            }
            finally
            {
                IsLoadingUtauVoices = false;
                RaiseCanExecuteChangedCommands();
            }
        }


        private void BrowseSoundFont()
        {
            if (SelectedModel?.ModelType != ModelType.SoundFont) return;
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "SoundFont files (*.sf2)|*.sf2|All files (*.*)|*.*",
                    Title = "SoundFontファイルを選択"
                };

                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                var defaultSfDir = Path.Combine(assemblyLocation, MidiConfiguration.Default.SoundFont.DefaultSoundFontDirectory);
                if (!Directory.Exists(defaultSfDir))
                {
                    try { Directory.CreateDirectory(defaultSfDir); }
                    catch (Exception ex)
                    {
                        Logger.Error(LogMessages.DirectoryCreateError, ex, defaultSfDir);
                    }
                }
                openFileDialog.InitialDirectory = defaultSfDir;

                if (openFileDialog.ShowDialog() == true)
                {
                    string fileName = Path.GetFileName(openFileDialog.FileName);
                    string targetPath = Path.Combine(defaultSfDir, fileName);

                    if (!File.Exists(targetPath) || new FileInfo(openFileDialog.FileName).FullName != new FileInfo(targetPath).FullName)
                    {
                        File.Copy(openFileDialog.FileName, targetPath, true);
                        _ = LoadAvailableSoundFontsAsync();
                    }


                    var existingVm = AvailableSoundFonts.FirstOrDefault(f => f.FileName == fileName);
                    if (existingVm == null)
                    {
                        var newVm = new SoundFontFileViewModel(fileName);
                        AvailableSoundFonts.Add(newVm);
                        SelectedAvailableSoundFont = newVm;
                    }
                    else
                    {
                        SelectedAvailableSoundFont = existingVm;
                    }

                    if (!SoundFontLayers.Any(l => l.SoundFontFile == fileName) && SelectedAvailableSoundFont != null)
                    {
                        AddLayer(null);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.SoundFontBrowseError, ex);
                MessageBox.Show($"SoundFontの参照中にエラーが発生しました: {ex.Message}", Translate.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RaiseCanExecuteChangedCommands();
            }
        }

        private bool CanAddLayer(object? parameter) => SelectedModel?.ModelType == ModelType.SoundFont && SelectedAvailableSoundFont != null && !SoundFontLayers.Any(l => l.SoundFontFile == SelectedAvailableSoundFont.FileName);
        private void AddLayer(object? parameter)
        {
            if (SelectedAvailableSoundFont != null && CanAddLayer(parameter))
            {
                SoundFontLayers.Add(new SoundFontLayer { SoundFontFile = SelectedAvailableSoundFont.FileName });
                RaiseCanExecuteChangedCommands();
            }
        }

        private bool CanRemoveLayer(object? parameter) => SelectedModel?.ModelType == ModelType.SoundFont && SelectedLayer != null;
        private void RemoveLayer(object? parameter)
        {
            if (SelectedLayer != null && CanRemoveLayer(parameter))
            {
                SoundFontLayers.Remove(SelectedLayer);
                RaiseCanExecuteChangedCommands();
            }
        }

        private bool CanMoveLayerUp(object? parameter) => SelectedModel?.ModelType == ModelType.SoundFont && SelectedLayer != null && SoundFontLayers.IndexOf(SelectedLayer) > 0;
        private void MoveLayerUp(object? parameter)
        {
            if (SelectedLayer != null && CanMoveLayerUp(parameter))
            {
                var index = SoundFontLayers.IndexOf(SelectedLayer);
                SoundFontLayers.Move(index, index - 1);
            }
        }

        private bool CanMoveLayerDown(object? parameter) => SelectedModel?.ModelType == ModelType.SoundFont && SelectedLayer != null && SoundFontLayers.IndexOf(SelectedLayer) < SoundFontLayers.Count - 1;
        private void MoveLayerDown(object? parameter)
        {
            if (SelectedLayer != null && CanMoveLayerDown(parameter))
            {
                var index = SoundFontLayers.IndexOf(SelectedLayer);
                SoundFontLayers.Move(index, index + 1);
            }
        }

        private void ShowCreateNewModelWindow(object? parameter)
        {
            var existingNames = VoiceModels.Select(m => m.Name);
            var createViewModel = new CreateNewModelViewModel(existingNames);
            var createWindow = new CreateNewModelWindow(createViewModel)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (createWindow.ShowDialog() == true)
            {
                CreateNewModel(createViewModel.ModelName, createViewModel.SelectedModelType);
            }
        }

        private void CreateNewModel(string newModelName, ModelType modelType)
        {
            var newModel = new VoiceModel { Name = newModelName, ModelType = modelType };

            if (modelType == ModelType.SoundFont)
            {
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                var defaultSfPath = Path.Combine(assemblyLocation, "GeneralUser-GS.sf2");
                if (File.Exists(defaultSfPath))
                {
                    newModel.Layers.Add(new SoundFontLayer { SoundFontFile = "GeneralUser-GS.sf2" });
                }
            }
            else if (modelType == ModelType.InternalSynth)
            {
                newModel.InternalSynthSettings = new InternalSynthModel();
            }
            else if (modelType == ModelType.UTAU)
            {
            }

            VoiceModels.Add(newModel);
            SelectedModel = newModel;
        }

        private bool CanSaveCurrentModel(object? parameter)
        {
            if (SelectedModel == null) return false;
            return SelectedModel.ModelType switch
            {
                ModelType.InternalSynth => true,
                ModelType.SoundFont => SoundFontLayers.Any(),
                ModelType.UTAU => true,
                _ => false
            };
        }

        private void SaveCurrentModel(object? parameter)
        {
            if (SelectedModel != null && CanSaveCurrentModel(parameter))
            {
                if (SelectedModel.ModelType == ModelType.SoundFont)
                {
                    SelectedModel.Layers.Clear();
                    foreach (var layer in SoundFontLayers)
                    {
                        SelectedModel.Layers.Add((SoundFontLayer)layer.Clone());
                    }
                }
                else if (SelectedModel.ModelType == ModelType.UTAU)
                {
                }
                _settings.Save();
            }
        }

        private bool CanDeleteModel(object? parameter) => SelectedModel != null && VoiceModels.Count > 1 && SelectedModel.Name != Translate.DefaultSynthModelName && SelectedModel.Name != Translate.DefaultSoundFontModelName;

        private void DeleteModel(object? parameter)
        {
            if (SelectedModel != null && CanDeleteModel(parameter))
            {
                var result = MessageBox.Show(string.Format(Translate.ConfirmDeleteModelMessage, SelectedModel.Name), Translate.DeleteConfirmationTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    var index = VoiceModels.IndexOf(SelectedModel);
                    VoiceModels.Remove(SelectedModel);

                    if (VoiceModels.Count > 0)
                    {
                        SelectedModel = VoiceModels[Math.Max(0, Math.Min(index, VoiceModels.Count - 1))];
                    }
                    else
                    {
                        SelectedModel = null;
                    }
                    _settings.Save();
                    RaiseCanExecuteChangedCommands();
                }
            }
        }

        private bool CanEditModelName(object? parameter) => SelectedModel != null && SelectedModel.Name != Translate.DefaultSynthModelName && SelectedModel.Name != Translate.DefaultSoundFontModelName;

        private void EditModelName(object? parameter)
        {
            if (SelectedModel != null && CanEditModelName(parameter))
            {
                var existingNames = VoiceModels.Select(m => m.Name);
                var renameViewModel = new RenameVoiceModelViewModel(SelectedModel.Name, existingNames);
                var renameWindow = new RenameVoiceModelWindow(renameViewModel)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                if (renameWindow.ShowDialog() == true)
                {
                    if (renameViewModel.CanConfirm)
                    {
                        string oldName = SelectedModel.Name;
                        SelectedModel.Name = renameViewModel.NewModelName;
                        if (SynthSettings.CurrentModelName == oldName)
                        {
                            SynthSettings.CurrentModelName = SelectedModel.Name;
                        }
                        _settings.Save();
                        OnPropertyChanged(nameof(VoiceModels));
                        OnPropertyChanged(nameof(SelectedModel));
                    }
                    else
                    {
                        MessageBox.Show(Translate.InvalidOrExistingModelNameError, Translate.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }


        private void RaiseCanExecuteChangedCommands()
        {
            Application.Current?.Dispatcher?.Invoke(() => {
                (BrowseSoundFontCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddLayerCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RemoveLayerCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveLayerUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveLayerDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SaveCurrentModelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteModelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (EditModelNameCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RefreshUtauVoicesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            });
            OnPropertyChanged(nameof(IsSoundFontModelSelected));
            OnPropertyChanged(nameof(IsInternalSynthModelSelected));
            OnPropertyChanged(nameof(IsUtauModelSelected));
        }
    }
}