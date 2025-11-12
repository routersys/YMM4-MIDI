using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using MIDI.UI.Commands;
using MIDI.UI.Views;
using MIDI.Tool.EMEL.View;

namespace MIDI.Tool.ViewModel
{
    public class MidiEditorToolViewModel : INotifyPropertyChanged
    {
        public ICommand OpenMidiEditorCommand { get; }
        public ICommand OpenEmelEditorCommand { get; }

        private static MidiEditorWindow? midiEditorWindowInstance;
        private static EmelEditorWindow? emelEditorWindowInstance;

        public event PropertyChangedEventHandler? PropertyChanged;

        public MidiEditorToolViewModel()
        {
            OpenMidiEditorCommand = new RelayCommand(OpenMidiEditor);
            OpenEmelEditorCommand = new RelayCommand(OpenEmelEditor);
        }

        private void OpenMidiEditor(object? parameter)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (midiEditorWindowInstance != null && midiEditorWindowInstance.IsLoaded)
                {
                    if (midiEditorWindowInstance.WindowState == WindowState.Minimized)
                    {
                        midiEditorWindowInstance.WindowState = WindowState.Normal;
                    }
                    midiEditorWindowInstance.Activate();
                    return;
                }

                midiEditorWindowInstance = new MidiEditorWindow(null)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                midiEditorWindowInstance.Closed += (s, e) => { midiEditorWindowInstance = null; };
                midiEditorWindowInstance.Show();
            });
        }

        private void OpenEmelEditor(object? parameter)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (emelEditorWindowInstance != null && emelEditorWindowInstance.IsLoaded)
                {
                    if (emelEditorWindowInstance.WindowState == WindowState.Minimized)
                    {
                        emelEditorWindowInstance.WindowState = WindowState.Normal;
                    }
                    emelEditorWindowInstance.Activate();
                    return;
                }

                emelEditorWindowInstance = new EmelEditorWindow()
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                emelEditorWindowInstance.Closed += (s, e) => { emelEditorWindowInstance = null; };
                emelEditorWindowInstance.Show();
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}