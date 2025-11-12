using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;
using System.Windows.Input;
using System.Windows.Data;
using System.Text.RegularExpressions;

namespace MIDI.AudioEffect.DELAY.UI
{
    public partial class DelayEffectControl : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private ItemProperty[] itemProperties = [];
        private DelayEffectViewModel? viewModel;
        private readonly DispatcherTimer timer;

        private static readonly Regex _regex = new Regex("[^0-9]+");

        public DelayEffectControl()
        {
            InitializeComponent();
            timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            timer.Tick += Timer_Tick;
            Loaded += (s, e) => timer.Start();
            Unloaded += (s, e) => timer.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            viewModel?.UpdateLevels();
        }

        public void SetItemProperties(ItemProperty[] properties)
        {
            itemProperties = properties;
            if (itemProperties.Length > 0 && itemProperties[0].PropertyOwner is DelayAudioEffect owner)
            {
                viewModel = new DelayEffectViewModel
                {
                    EffectItem = owner
                };
                viewModel.BeginEdit += OnBeginEdit;
                viewModel.EndEdit += OnEndEdit;
                DataContext = viewModel;
            }
        }

        public void ClearItemProperties()
        {
            if (viewModel != null)
            {
                viewModel.BeginEdit -= OnBeginEdit;
                viewModel.EndEdit -= OnEndEdit;
                viewModel.Dispose();
                viewModel = null;
            }
            DataContext = null;
            itemProperties = [];
        }

        private void OnBeginEdit(object? sender, EventArgs e)
        {
            BeginEdit?.Invoke(this, e);
        }

        private void OnEndEdit(object? sender, EventArgs e)
        {
            EndEdit?.Invoke(this, e);
        }

        private void Slider_BeginEdit(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
        }

        private void Slider_EndEdit(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void MixValueTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                MixValueTextBlock.Visibility = Visibility.Collapsed;

                BindingExpression be = MixValueTextBox.GetBindingExpression(TextBox.TextProperty);
                be?.UpdateTarget();
                MixValueTextBox.Text = MixSlider.Value.ToString("F0");

                MixValueTextBox.Visibility = Visibility.Visible;
                MixValueTextBox.SelectAll();
                MixValueTextBox.Focus();
                e.Handled = true;
            }
        }

        private void MixValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateMixValueFromTextBox();
            MixValueTextBox.Visibility = Visibility.Collapsed;
            MixValueTextBlock.Visibility = Visibility.Visible;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void MixValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                UpdateMixValueFromTextBox();
                MixValueTextBox.Visibility = Visibility.Collapsed;
                MixValueTextBlock.Visibility = Visibility.Visible;
                EndEdit?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                BindingExpression be = MixValueTextBox.GetBindingExpression(TextBox.TextProperty);
                be?.UpdateTarget();

                MixValueTextBox.Visibility = Visibility.Collapsed;
                MixValueTextBlock.Visibility = Visibility.Visible;
                EndEdit?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        private void UpdateMixValueFromTextBox()
        {
            BindingExpression be = MixValueTextBox.GetBindingExpression(TextBox.TextProperty);
            if (be != null)
            {
                if (double.TryParse(MixValueTextBox.Text, out double newValue))
                {
                    if (newValue < MixSlider.Minimum) newValue = MixSlider.Minimum;
                    if (newValue > MixSlider.Maximum) newValue = MixSlider.Maximum;
                    MixValueTextBox.Text = newValue.ToString("F0");
                }
                else
                {
                    MixValueTextBox.Text = MixSlider.Value.ToString("F0");
                }
                be.UpdateSource();
            }
        }

        private void MixValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = _regex.IsMatch(e.Text);
        }
    }
}