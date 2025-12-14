using System;
using System.Windows.Input;
using MIDI.Configuration.Models;
using MIDI.UI.Commands;

namespace MIDI.Utils.Telemetry.ViewModels
{
    public class TelemetryConsentViewModel : ObservableObject
    {
        private readonly Action _closeAction;

        public ICommand EnableCommand { get; }
        public ICommand DisableCommand { get; }

        public TelemetryConsentViewModel(Action closeAction)
        {
            _closeAction = closeAction;
            EnableCommand = new RelayCommand(_ => EnableTelemetry());
            DisableCommand = new RelayCommand(_ => DisableTelemetry());
        }

        private void EnableTelemetry()
        {
            SaveSettings(true);
            _closeAction?.Invoke();
        }

        private void DisableTelemetry()
        {
            SaveSettings(false);
            _closeAction?.Invoke();
        }

        private void SaveSettings(bool isEnabled)
        {
            TelemetrySettings.Default.IsEnabled = isEnabled;
            TelemetrySettings.Default.HasAskedConsent = true;
            TelemetrySettings.Default.SaveSynchronously();
        }
    }
}