using MIDI.AudioEffect.EQUALIZER.ViewModels;
using System.Windows;

namespace MIDI.AudioEffect.EQUALIZER.Views
{
    public partial class EqualizerSettingsWindow : Window
    {
        public EqualizerSettingsWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}