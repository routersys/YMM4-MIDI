using MIDI.AudioEffect.EQUALIZER.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace MIDI.AudioEffect.EQUALIZER.UI
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

        private void PresetListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
        }
    }
}