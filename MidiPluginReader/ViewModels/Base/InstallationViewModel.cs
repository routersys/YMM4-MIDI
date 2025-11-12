using System.IO;
using System.Windows.Input;
using MidiPlugin.Core;
using MidiPlugin.Models;
using MidiPlugin.Services;
using MidiPlugin.ViewModels.Base;
using System;

namespace MidiPlugin.ViewModels
{
    public class InstallationViewModel : ViewModelBase
    {
        private readonly PresetService _presetService;
        private string _statusMessage;
        private bool _isInstallButtonEnabled = true;

        public string FilePath { get; }
        public string FileName { get; }
        public PresetDetails PresetDetails { get; private set; }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsInstallButtonEnabled
        {
            get => _isInstallButtonEnabled;
            set
            {
                _isInstallButtonEnabled = value;
                OnPropertyChanged();
            }
        }

        public ICommand InstallCommand { get; }

        public InstallationViewModel(string filePath)
        {
            _presetService = new PresetService();
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            InstallCommand = new RelayCommand(Install, (p) => IsInstallButtonEnabled);

            try
            {
                var presetManager = new PresetManager();
                PresetDetails = presetManager.ParsePreset(filePath);
                StatusMessage = $"{FileName} をインストールする準備ができました。";
                IsInstallButtonEnabled = true;

                if (PresetDetails.ChangedItems.Count == 0 && PresetDetails.Certificate == null)
                {
                    throw new Exception("有効な設定項目が含まれていません。");
                }
            }
            catch (Exception ex)
            {
                PresetDetails = new PresetDetails();
                StatusMessage = $"プリセットファイルの解析に失敗しました。\nファイルが破損している可能性があります。";
                IsInstallButtonEnabled = false;

                PresetDetails.ChangedCategories["エラー"] = new System.Collections.Generic.List<string>
                {
                    ex.Message
                };
            }
        }

        private void Install(object parameter)
        {
            try
            {
                IsInstallButtonEnabled = false;
                var installedPath = _presetService.InstallPreset(FilePath);
                StatusMessage = $"インストールが完了しました。\n場所: {installedPath}";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"エラーが発生しました: {ex.Message}";
            }
        }
    }
}