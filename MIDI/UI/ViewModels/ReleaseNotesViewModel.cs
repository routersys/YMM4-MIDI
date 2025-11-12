using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Documents;
using MIDI.Utils;

namespace MIDI.UI.ViewModels
{
    public class ReleaseDisplayViewModel : INotifyPropertyChanged
    {
        public GitHubRelease Release { get; }
        public bool IsCurrentVersion { get; }
        public string TagName => Release.TagName;

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

    public class ReleaseNotesViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ReleaseDisplayViewModel> Releases { get; }

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

        public FlowDocument ReleaseNotesDocument { get; private set; }

        public ReleaseNotesViewModel(List<GitHubRelease> releases, string currentVersion)
        {
            Releases = new ObservableCollection<ReleaseDisplayViewModel>(
                releases.Select(r => new ReleaseDisplayViewModel(r, r.TagName.Contains(currentVersion)))
            );
            SelectedRelease = Releases.FirstOrDefault(r => r.IsCurrentVersion) ?? Releases.FirstOrDefault();
            ReleaseNotesDocument = MarkdownToFlowDocumentConverter.Convert(SelectedRelease?.Release.Body ?? "リリースノートが見つかりませんでした。");
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