using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Data;
using System.Text.RegularExpressions;
using YukkuriMovieMaker.Commons;

namespace MIDI.AudioEffect.ENHANCER.UI
{
    public partial class EnhancerEffectControl : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private ItemProperty[] itemProperties = [];
        private EnhancerEffectViewModel? viewModel;
        private readonly DispatcherTimer timer;

        private static readonly Regex _regex = new Regex("[^0-9.-]+");

        public EnhancerEffectControl()
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
            if (itemProperties.Length > 0 && itemProperties[0].PropertyOwner is EnhancerAudioEffect owner)
            {
                viewModel = new EnhancerEffectViewModel
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

        private void ValueTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is TextBlock textBlock && textBlock.Tag is string propertyName)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                textBlock.Visibility = Visibility.Collapsed;

                var textBoxName = propertyName + "TextBox";
                var textBox = FindName(textBoxName) as TextBox;
                if (textBox != null)
                {
                    BindingExpression be = textBox.GetBindingExpression(TextBox.TextProperty);
                    be?.UpdateTarget();

                    textBox.Visibility = Visibility.Visible;
                    textBox.SelectAll();
                    textBox.Focus();
                }
                e.Handled = true;
            }
        }

        private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is string propertyName)
            {
                UpdateValueFromTextBox(textBox);
                textBox.Visibility = Visibility.Collapsed;

                var textBlockName = propertyName + "TextBlock";
                var textBlock = FindName(textBlockName) as TextBlock;
                if (textBlock != null) textBlock.Visibility = Visibility.Visible;

                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is string propertyName)
            {
                if (e.Key == Key.Enter)
                {
                    UpdateValueFromTextBox(textBox);
                    textBox.Visibility = Visibility.Collapsed;

                    var textBlockName = propertyName + "TextBlock";
                    var textBlock = FindName(textBlockName) as TextBlock;
                    if (textBlock != null) textBlock.Visibility = Visibility.Visible;

                    EndEdit?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    BindingExpression be = textBox.GetBindingExpression(TextBox.TextProperty);
                    be?.UpdateTarget();

                    textBox.Visibility = Visibility.Collapsed;

                    var textBlockName = propertyName + "TextBlock";
                    var textBlock = FindName(textBlockName) as TextBlock;
                    if (textBlock != null) textBlock.Visibility = Visibility.Visible;

                    EndEdit?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
            }
        }

        private void UpdateValueFromTextBox(TextBox textBox)
        {
            BindingExpression be = textBox.GetBindingExpression(TextBox.TextProperty);
            if (be != null)
            {
                be.UpdateSource();
            }
        }

        private void ValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = _regex.IsMatch(e.Text);
        }
    }
}