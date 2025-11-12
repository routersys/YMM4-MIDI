using System.Windows;
using System.Windows.Controls;

namespace MidiPlugin.Views
{
    public partial class EffectInstallationView : UserControl
    {
        public EffectInstallationView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}