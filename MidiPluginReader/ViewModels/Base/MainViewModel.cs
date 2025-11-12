using MidiPlugin.Models;
using MidiPlugin.ViewModels.Base;
using System;

namespace MidiPlugin.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel;

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                _currentViewModel = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (args[0].EndsWith(AppConfig.PresetFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentViewModel = new InstallationViewModel(args[0]);
                }
                else if (args[0].EndsWith(AppConfig.EffectFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentViewModel = new EffectInstallationViewModel(args[0]);
                }
                else
                {
                    CurrentViewModel = new SettingsViewModel();
                }
            }
            else
            {
                CurrentViewModel = new SettingsViewModel();
            }
        }
    }
}