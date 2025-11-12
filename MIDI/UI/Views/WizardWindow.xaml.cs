using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using MIDI.UI.ViewModels;
using System.ComponentModel;
using MIDI.Configuration.Models;

namespace MIDI.UI.Views
{
    public partial class WizardWindow : Window
    {
        private readonly WizardViewModel _viewModel;

        public WizardWindow(WizardViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.CloseAction = (result) =>
            {
                DialogResult = result;
                Close();
            };
            Closing += WizardWindow_Closing;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            (Resources["LoadingAnimation"] as Storyboard)?.Begin(this, true);

            await Task.Delay(2000);

            var fadeOutAnimation = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
            fadeOutAnimation.Completed += (s, a) =>
            {
                LoadingGrid.Visibility = Visibility.Collapsed;
                MainGrid.Visibility = Visibility.Visible;
                var fadeInAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
                MainGrid.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            };
            LoadingGrid.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }

        private void WizardWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (DialogResult == null)
            {
                var config = MidiConfiguration.Default;
                if (config.IsFirstLaunch)
                {
                    config.IsFirstLaunch = false;
                    config.SaveSynchronously();
                    MidiEditorSettings.Default.SaveSynchronously();
                }
            }
            Closing -= WizardWindow_Closing;
        }
    }
}