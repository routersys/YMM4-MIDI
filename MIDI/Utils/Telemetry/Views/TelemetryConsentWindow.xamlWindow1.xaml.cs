using System.Windows;
using System.Windows.Input;
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}