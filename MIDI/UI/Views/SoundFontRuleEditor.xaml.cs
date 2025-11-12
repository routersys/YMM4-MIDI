using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MIDI.Configuration.Models;
using MIDI.UI.Commands;

namespace MIDI
{
    public class SoundFontRuleEditorViewModel : INotifyPropertyChanged
    {
        public SoundFontRule Rule { get; set; }
        public string SoundFontFile { get; }
        public Action<bool?>? CloseAction { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand DeleteCommand { get; }

        public SoundFontRuleEditorViewModel(SoundFontRule rule, string soundFontFile)
        {
            Rule = rule;
            SoundFontFile = soundFontFile;

            SaveCommand = new RelayCommand(_ =>
            {
                CloseAction?.Invoke(true);
            });

            CancelCommand = new RelayCommand(_ =>
            {
                CloseAction?.Invoke(false);
            });

            DeleteCommand = new RelayCommand(_ =>
            {
                CloseAction?.Invoke(null);
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class SoundFontRuleEditor : Window
    {
        public SoundFontRuleEditor()
        {
            InitializeComponent();
        }
    }
}