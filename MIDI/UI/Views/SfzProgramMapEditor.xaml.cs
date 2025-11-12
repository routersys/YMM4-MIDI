using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MIDI.Configuration.Models;
using MIDI.UI.Commands;

namespace MIDI
{
    public class SfzProgramMapEditorViewModel : INotifyPropertyChanged
    {
        public SfzProgramMap Map { get; set; }
        public string FilePath { get; }
        public Action<bool?>? CloseAction { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand DeleteCommand { get; }

        public SfzProgramMapEditorViewModel(SfzProgramMap map, string filePath)
        {
            Map = map;
            FilePath = filePath;

            SaveCommand = new RelayCommand(_ => CloseAction?.Invoke(true));
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
            DeleteCommand = new RelayCommand(_ => CloseAction?.Invoke(null));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class SfzProgramMapEditor : Window
    {
        public SfzProgramMapEditor()
        {
            InitializeComponent();
        }
    }
}