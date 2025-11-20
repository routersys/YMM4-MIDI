using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Data;
using System.Text.RegularExpressions;
using YukkuriMovieMaker.Commons;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.UI
{
    public partial class MultibandSaturatorControl : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;
        private ItemProperty[] itemProperties = [];
        private MultibandSaturatorViewModel? viewModel;
        private readonly DispatcherTimer timer;
        private static readonly Regex _regex = new Regex("[^0-9.-]+");

        public MultibandSaturatorControl()
        {
            InitializeComponent();
            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
            timer.Tick += Timer_Tick;
            Loaded += (s, e) => timer.Start();
            Unloaded += (s, e) => timer.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e) => viewModel?.UpdateLevels();

        public void SetItemProperties(ItemProperty[] properties)
        {
            itemProperties = properties;
            if (itemProperties.Length > 0 && itemProperties[0].PropertyOwner is MultibandSaturatorAudioEffect owner)
            {
                viewModel = new MultibandSaturatorViewModel { EffectItem = owner };
                viewModel.BeginEdit += (s, e) => BeginEdit?.Invoke(this, e);
                viewModel.EndEdit += (s, e) => EndEdit?.Invoke(this, e);
                DataContext = viewModel;
            }
        }

        public void ClearItemProperties()
        {
            if (viewModel != null) { viewModel.Dispose(); viewModel = null; }
            DataContext = null;
            itemProperties = [];
        }

        private void Slider_BeginEdit(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);
        private void Slider_EndEdit(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

        private void ValueTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is TextBlock tb && tb.Tag is string tag)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                tb.Visibility = Visibility.Collapsed;
                if (FindName(tag + "TextBox") is TextBox tx)
                {
                    tx.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                    tx.Visibility = Visibility.Visible;
                    tx.SelectAll();
                    tx.Focus();
                }
                e.Handled = true;
            }
        }

        private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tx && tx.Tag is string tag)
            {
                tx.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                tx.Visibility = Visibility.Collapsed;
                if (FindName(tag + "TextBlock") is TextBlock tb) tb.Visibility = Visibility.Visible;
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tx && tx.Tag is string tag)
            {
                if (e.Key == Key.Enter)
                {
                    tx.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    tx.Visibility = Visibility.Collapsed;
                    if (FindName(tag + "TextBlock") is TextBlock tb) tb.Visibility = Visibility.Visible;
                    EndEdit?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    tx.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                    tx.Visibility = Visibility.Collapsed;
                    if (FindName(tag + "TextBlock") is TextBlock tb) tb.Visibility = Visibility.Visible;
                    EndEdit?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
            }
        }

        private void ValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = _regex.IsMatch(e.Text);
    }
}