using System.Windows;
using MIDI.Utils.Telemetry.ViewModels;

namespace MIDI.Utils.Telemetry.Views
{
    public partial class TelemetryConsentWindow : Window
    {
        public TelemetryConsentWindow()
        {
            InitializeComponent();
            DataContext = new TelemetryConsentViewModel(() => {
                this.DialogResult = true;
                this.Close();
            });
        }
    }
}