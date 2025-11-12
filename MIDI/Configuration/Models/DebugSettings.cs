using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public class DebugSettings : INotifyPropertyChanged
    {
        private bool _showMidiInfoWindowOnLoad;
        public bool ShowMidiInfoWindowOnLoad { get => _showMidiInfoWindowOnLoad; set => SetField(ref _showMidiInfoWindowOnLoad, value); }

        private bool _enableMidiEditor;
        public bool EnableMidiEditor { get => _enableMidiEditor; set => SetField(ref _enableMidiEditor, value); }

        private bool _enableMidiInput;
        public bool EnableMidiInput { get => _enableMidiInput; set => SetField(ref _enableMidiInput, value); }

        private string _midiInputDevice = string.Empty;
        public string MidiInputDevice { get => _midiInputDevice; set => SetField(ref _midiInputDevice, value); }

        private bool _enableLogging;
        public bool EnableLogging { get => _enableLogging; set => SetField(ref _enableLogging, value); }

        private bool _verboseLogging;
        public bool VerboseLogging { get => _verboseLogging; set => SetField(ref _verboseLogging, value); }

        private string _logFilePath = "midi_debug.log";
        public string LogFilePath { get => _logFilePath; set => SetField(ref _logFilePath, value); }

        private int _maxLogSizeKB = 30;
        public int MaxLogSizeKB { get => _maxLogSizeKB; set => SetField(ref _maxLogSizeKB, value); }

        private bool _enableNamedPipeApi;
        public bool EnableNamedPipeApi { get => _enableNamedPipeApi; set => SetField(ref _enableNamedPipeApi, value); }

        private int _logLevel = 3;
        public int LogLevel { get => _logLevel; set => SetField(ref _logLevel, value); }


        public void CopyFrom(DebugSettings source)
        {
            ShowMidiInfoWindowOnLoad = source.ShowMidiInfoWindowOnLoad;
            EnableMidiEditor = source.EnableMidiEditor;
            EnableMidiInput = source.EnableMidiInput;
            MidiInputDevice = source.MidiInputDevice;
            EnableLogging = source.EnableLogging;
            VerboseLogging = source.VerboseLogging;
            LogFilePath = source.LogFilePath;
            MaxLogSizeKB = source.MaxLogSizeKB;
            EnableNamedPipeApi = source.EnableNamedPipeApi;
            LogLevel = source.LogLevel;
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