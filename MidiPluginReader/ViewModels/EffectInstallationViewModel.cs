using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using MidiPlugin.Core;
using MidiPlugin.Models;
using MidiPlugin.Services;
using MidiPlugin.ViewModels.Base;

namespace MidiPlugin.ViewModels
{
    public class EffectInstallationViewModel : ViewModelBase
    {
        private string _statusMessage;
        private string _readmeContent;
        private bool _isTrusted;
        private bool _isInstallButtonEnabled = false;

        public string FilePath { get; }
        public string FileName { get; }
        public ObservableCollection<string> FileEntries { get; }

        public string ReadmeContent
        {
            get => _readmeContent;
            set => SetField(ref _readmeContent, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public bool IsTrusted
        {
            get => _isTrusted;
            set
            {
                if (SetField(ref _isTrusted, value))
                {
                    IsInstallButtonEnabled = value;
                }
            }
        }

        public bool IsInstallButtonEnabled
        {
            get => _isInstallButtonEnabled;
            set
            {
                if (SetField(ref _isInstallButtonEnabled, value))
                {
                    (InstallCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand InstallCommand { get; }

        public EffectInstallationViewModel(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            FileEntries = new ObservableCollection<string>();
            InstallCommand = new RelayCommand(Install, (p) => IsInstallButtonEnabled);

            LoadPluginDetails(filePath);
        }

        private void LoadPluginDetails(string filePath)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(filePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        FileEntries.Add(entry.FullName);
                    }

                    var readmeEntry = archive.Entries
                        .FirstOrDefault(e => e.Name.Equals("readme.txt", StringComparison.OrdinalIgnoreCase));

                    if (readmeEntry != null)
                    {
                        using (var stream = readmeEntry.Open())
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            ReadmeContent = reader.ReadToEnd();
                        }
                    }
                    else
                    {
                        ReadmeContent = "Readme.txt が見つかりませんでした。";
                    }
                }
                StatusMessage = $"{FileName} をインストールする準備ができました。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"プラグインファイルの読み込みに失敗しました: {ex.Message}";
                IsInstallButtonEnabled = false;
                IsTrusted = false;
            }
        }

        private void Install(object parameter)
        {
            try
            {
                string pluginFolderName = Path.GetFileNameWithoutExtension(FilePath);
                string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(baseDir))
                {
                    throw new InvalidOperationException("インストール先の基底ディレクトリを取得できませんでした。");
                }

                var extensionsDir = Path.Combine(baseDir, AppConfig.EffectFolderName);
                var destinationPluginDir = Path.Combine(extensionsDir, pluginFolderName);

                Directory.CreateDirectory(destinationPluginDir);

                using (ZipArchive archive = ZipFile.OpenRead(FilePath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.Combine(destinationPluginDir, entry.FullName);

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                            entry.ExtractToFile(destinationPath, true);
                        }
                    }
                }

                StatusMessage = $"インストールが完了しました。\n以下の場所に展開されました:\n{destinationPluginDir}";
                IsInstallButtonEnabled = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"エラーが発生しました: {ex.Message}";
                IsInstallButtonEnabled = true;
            }
        }
    }
}