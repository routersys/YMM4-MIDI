using MIDI.UI.Commands;
using MIDI.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

namespace MIDI.UI.ViewModels
{
    public class ReleaseDisplayViewModel : INotifyPropertyChanged
    {
        public GitHubRelease Release { get; }
        public bool IsCurrentVersion { get; }
        public string TagName => Release.TagName;
        public double DescriptionLength => Math.Min(300, Math.Max(20, Release.Body.Split('\n').Length * 5));

        public ReleaseDisplayViewModel(GitHubRelease release, bool isCurrentVersion)
        {
            Release = release;
            IsCurrentVersion = isCurrentVersion;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LicenseFileViewModel
    {
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class BooleanToCurrentVerStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "(Current)" : "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ReleaseNotesViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ReleaseDisplayViewModel> Releases { get; }
        public ObservableCollection<LicenseFileViewModel> LicenseFiles { get; } = new ObservableCollection<LicenseFileViewModel>();
        public string CurrentVersion { get; }

        private ReleaseDisplayViewModel? _selectedRelease;
        public ReleaseDisplayViewModel? SelectedRelease
        {
            get => _selectedRelease;
            set
            {
                if (SetField(ref _selectedRelease, value) && value != null)
                {
                    ReleaseNotesDocument = MarkdownToFlowDocumentConverter.Convert(value.Release.Body);
                    OnPropertyChanged(nameof(ReleaseNotesDocument));
                }
            }
        }

        private LicenseFileViewModel? _selectedLicenseFile;
        public LicenseFileViewModel? SelectedLicenseFile
        {
            get => _selectedLicenseFile;
            set => SetField(ref _selectedLicenseFile, value);
        }

        private bool _isReleaseNotesTabSelected = true;
        public bool IsReleaseNotesTabSelected
        {
            get => _isReleaseNotesTabSelected;
            set => SetField(ref _isReleaseNotesTabSelected, value);
        }

        private bool _isLicenseTabSelected;
        public bool IsLicenseTabSelected
        {
            get => _isLicenseTabSelected;
            set => SetField(ref _isLicenseTabSelected, value);
        }

        private bool _isStatisticsTabSelected;
        public bool IsStatisticsTabSelected
        {
            get => _isStatisticsTabSelected;
            set => SetField(ref _isStatisticsTabSelected, value);
        }

        public FlowDocument ReleaseNotesDocument { get; private set; }

        public ICommand OpenGitHubCommand { get; }
        public ICommand OpenSupportCommand { get; }

        private static string PluginDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

        public ReleaseNotesViewModel(List<GitHubRelease> releases, string currentVersion)
        {
            Releases = new ObservableCollection<ReleaseDisplayViewModel>(
                releases.Select(r => new ReleaseDisplayViewModel(r, r.TagName.TrimStart('v') == currentVersion.TrimStart('v')))
            );
            CurrentVersion = currentVersion;
            SelectedRelease = Releases.FirstOrDefault(r => r.IsCurrentVersion) ?? Releases.FirstOrDefault();
            ReleaseNotesDocument = MarkdownToFlowDocumentConverter.Convert(SelectedRelease?.Release.Body ?? "リリースノートが見つかりませんでした。");

            OpenGitHubCommand = new RelayCommand(_ => Process.Start(new ProcessStartInfo("https://github.com/routersys/YMM4-MIDI") { UseShellExecute = true }));
            OpenSupportCommand = new RelayCommand(_ => Process.Start(new ProcessStartInfo("https://github.com/routersys/YMM4-MIDI/issues") { UseShellExecute = true }));

            LoadLicenses();
        }

        private void LoadLicenses()
        {
            try
            {
                var licenseDir = Path.Combine(PluginDir, "LICENSE");
                if (Directory.Exists(licenseDir))
                {
                    var files = Directory.GetFiles(licenseDir, "*.txt");
                    foreach (var file in files)
                    {
                        LicenseFiles.Add(new LicenseFileViewModel
                        {
                            FileName = Path.GetFileName(file),
                            Content = File.ReadAllText(file)
                        });
                    }
                }
            }
            catch { }

            if (LicenseFiles.Count == 0)
            {
                LicenseFiles.Add(new LicenseFileViewModel { FileName = "No Licenses Found", Content = "LICENSE ディレクトリが見つかりませんでした。" });
            }
            SelectedLicenseFile = LicenseFiles.FirstOrDefault();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}