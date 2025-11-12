using System;
using System.ComponentModel;
using System.Windows.Input;
using MIDI.UI.Commands;
using MIDI.UI.ViewModels.MidiEditor;
using YukkuriMovieMaker.Commons;
using MIDI.Voice.Views;
using System.Windows;

namespace MIDI.Voice.ViewModels
{
    internal class NoteNotationHelpViewModel : ViewModelBase, IPropertyEditorControl, IDisposable
    {
        readonly ItemProperty[] properties;
        readonly INotifyPropertyChanged item;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ICommand ShowEmelHelpCommand { get; }
        public ICommand ShowSuslHelpCommand { get; }

        public NoteNotationHelpViewModel(ItemProperty[] properties)
        {
            this.properties = properties;
            item = (INotifyPropertyChanged)properties[0].PropertyOwner;

            ShowEmelHelpCommand = new RelayCommand(ShowEmelHelp);
            ShowSuslHelpCommand = new RelayCommand(ShowSuslHelp);
        }

        private void ShowEmelHelp(object? parameter)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            var vm = new HelpWindowViewModel(HelpWindowViewModel.HelpTopic.EMEL);
            var window = new HelpWindow(vm)
            {
                Owner = Application.Current.MainWindow
            };
            window.Show();
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void ShowSuslHelp(object? parameter)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            var vm = new HelpWindowViewModel(HelpWindowViewModel.HelpTopic.SUSL);
            var window = new HelpWindow(vm)
            {
                Owner = Application.Current.MainWindow
            };
            window.Show();
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
        }
    }
}