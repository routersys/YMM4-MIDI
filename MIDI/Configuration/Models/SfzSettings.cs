using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public class SfzSettings : INotifyPropertyChanged
    {
        private bool _enableSfz;
        public bool EnableSfz { get => _enableSfz; set => SetField(ref _enableSfz, value); }

        private string _sfzSearchPath = "SFZ";
        public string SfzSearchPath { get => _sfzSearchPath; set => SetField(ref _sfzSearchPath, value); }

        private ObservableCollection<SfzProgramMap> _programMaps = new();
        public ObservableCollection<SfzProgramMap> ProgramMaps { get => _programMaps; set => SetField(ref _programMaps, value); }

        public void CopyFrom(SfzSettings source)
        {
            EnableSfz = source.EnableSfz;
            SfzSearchPath = source.SfzSearchPath;
            ProgramMaps = new ObservableCollection<SfzProgramMap>(source.ProgramMaps.Select(p => (SfzProgramMap)p.Clone()));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}