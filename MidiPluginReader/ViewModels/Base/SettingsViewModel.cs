using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MidiPlugin.Core;
using MidiPlugin.Services;
using MidiPlugin.ViewModels.Base;

namespace MidiPlugin.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly FileAssociationService _associationService;
        private readonly ConfigService _configService;
        private string _statusMessage;
        private bool _isAssociated;
        private string _selectedPath;
        private string _saveStatusMessage;

        public ObservableCollection<string> PrioritizedPaths { get; }

        public string SelectedPath
        {
            get => _selectedPath;
            set
            {
                if (SetField(ref _selectedPath, value))
                {
                    (MoveUpCommand as RelayCommand).RaiseCanExecuteChanged();
                    (MoveDownCommand as RelayCommand).RaiseCanExecuteChanged();
                    (RemovePathCommand as RelayCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsAssociated
        {
            get => _isAssociated;
            set
            {
                if (SetField(ref _isAssociated, value))
                {
                    UpdateStatusMessage();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public string SaveStatusMessage
        {
            get => _saveStatusMessage;
            set => SetField(ref _saveStatusMessage, value);
        }

        public ICommand AssociateCommand { get; }
        public ICommand DisassociateCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand SavePriorityCommand { get; }
        public ICommand RemovePathCommand { get; }

        public SettingsViewModel()
        {
            _associationService = new FileAssociationService();
            _configService = new ConfigService();
            PrioritizedPaths = new ObservableCollection<string>();
            PrioritizedPaths.CollectionChanged += (s, e) => (RemovePathCommand as RelayCommand).RaiseCanExecuteChanged();

            AssociateCommand = new RelayCommand(p => Associate(), p => !IsAssociated);
            DisassociateCommand = new RelayCommand(p => Disassociate(), p => IsAssociated);

            MoveUpCommand = new RelayCommand(p => MoveUp(), p => CanMoveUp());
            MoveDownCommand = new RelayCommand(p => MoveDown(), p => CanMoveDown());
            SavePriorityCommand = new RelayCommand(async p => await SavePriority());
            RemovePathCommand = new RelayCommand(RemovePath, p => CanRemovePath());

            LoadPrioritizedPaths();
            CheckAssociationStatus();
        }

        private void RemovePath(object path)
        {
            if (path is string pathToRemove)
            {
                PrioritizedPaths.Remove(pathToRemove);
            }
        }

        private void LoadPrioritizedPaths()
        {
            var paths = _configService.LoadPrioritizedExePaths();
            PrioritizedPaths.Clear();
            foreach (var path in paths)
            {
                PrioritizedPaths.Add(path);
            }
        }

        private void CheckAssociationStatus()
        {
            IsAssociated = _associationService.IsAssociated();
        }

        private void UpdateStatusMessage()
        {
            string presetExt = Models.AppConfig.PresetFileExtension;
            string effectExt = Models.AppConfig.EffectFileExtension;

            StatusMessage = IsAssociated
                ? $"拡張子 '{presetExt}' および '{effectExt}' は現在このアプリケーションに関連付けられています。"
                : $"拡張子 '{presetExt}' および '{effectExt}' は関連付けられていません。";
        }

        private void Associate()
        {
            _associationService.Associate();
            CheckAssociationStatus();
        }

        private void Disassociate()
        {
            _associationService.Disassociate();
            CheckAssociationStatus();
        }

        private bool CanMoveUp() => SelectedPath != null && PrioritizedPaths.IndexOf(SelectedPath) > 0;
        private bool CanMoveDown() => SelectedPath != null && PrioritizedPaths.IndexOf(SelectedPath) < PrioritizedPaths.Count - 1;
        private bool CanRemovePath() => SelectedPath != null && PrioritizedPaths.Count > 1;

        private void MoveUp()
        {
            if (CanMoveUp())
            {
                int index = PrioritizedPaths.IndexOf(SelectedPath);
                PrioritizedPaths.Move(index, index - 1);
            }
        }

        private void MoveDown()
        {
            if (CanMoveDown())
            {
                int index = PrioritizedPaths.IndexOf(SelectedPath);
                PrioritizedPaths.Move(index, index + 1);
            }
        }

        private async Task SavePriority()
        {
            _configService.SavePrioritizedExePaths(PrioritizedPaths.ToList());
            SaveStatusMessage = "保存しました。";
            await Task.Delay(3000);
            SaveStatusMessage = string.Empty;
        }
    }
}