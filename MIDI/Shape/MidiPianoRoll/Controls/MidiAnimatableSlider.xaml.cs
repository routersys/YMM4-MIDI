using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MIDI.Shape.MidiPianoRoll.Models;

namespace MIDI.Shape.MidiPianoRoll.Controls
{
    public partial class MidiAnimatableSlider : UserControl
    {
        public MidiAnimatableSlider()
        {
            InitializeConverters();
            InitializeComponent();
            UpdateTextBindings(StringFormat);
        }

        private void InitializeConverters()
        {
            if (Application.Current.Resources["BooleanToVisibilityConverter"] == null)
            {
                Application.Current.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
            }
            if (Application.Current.Resources["AnimationModeToContentConverter"] == null)
            {
                Application.Current.Resources["AnimationModeToContentConverter"] = new AnimationModeToContentConverter();
            }
            if (Application.Current.Resources["AnimationModeToVisibilityConverter"] == null)
            {
                Application.Current.Resources["AnimationModeToVisibilityConverter"] = new AnimationModeToVisibilityConverter();
            }
        }

        public AnimatableDouble Value
        {
            get { return (AnimatableDouble)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(AnimatableDouble),
                typeof(MidiAnimatableSlider),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public double Minimum
        {
            get { return (double)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(MidiAnimatableSlider), new PropertyMetadata(0.0));

        public double Maximum
        {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(MidiAnimatableSlider), new PropertyMetadata(1.0));

        public string StringFormat
        {
            get { return (string)GetValue(StringFormatProperty); }
            set { SetValue(StringFormatProperty, value); }
        }
        public static readonly DependencyProperty StringFormatProperty =
            DependencyProperty.Register(
                nameof(StringFormat),
                typeof(string),
                typeof(MidiAnimatableSlider),
                new PropertyMetadata("F2", OnStringFormatChanged));

        public string Unit
        {
            get { return (string)GetValue(UnitProperty); }
            set { SetValue(UnitProperty, value); }
        }
        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(MidiAnimatableSlider), new PropertyMetadata(""));

        private static void OnStringFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MidiAnimatableSlider slider)
            {
                slider.UpdateTextBindings((string)e.NewValue);
            }
        }

        private void UpdateTextBindings(string format)
        {
            var valueBinding = new Binding("Value.Value")
            {
                Source = this,
                Mode = BindingMode.TwoWay,
                StringFormat = format,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            ValueTextBox.SetBinding(TextBox.TextProperty, valueBinding);

            var startValueBinding = new Binding("Value.StartValue")
            {
                Source = this,
                Mode = BindingMode.TwoWay,
                StringFormat = format,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            StartValueTextBox.SetBinding(TextBox.TextProperty, startValueBinding);

            var endValueBinding = new Binding("Value.EndValue")
            {
                Source = this,
                Mode = BindingMode.TwoWay,
                StringFormat = format,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            EndValueTextBox.SetBinding(TextBox.TextProperty, endValueBinding);

            var repeatStartValueBinding = new Binding("Value.StartValue")
            {
                Source = this,
                Mode = BindingMode.TwoWay,
                StringFormat = format,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            RepeatStartValueTextBox.SetBinding(TextBox.TextProperty, repeatStartValueBinding);

            var repeatEndValueBinding = new Binding("Value.EndValue")
            {
                Source = this,
                Mode = BindingMode.TwoWay,
                StringFormat = format,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            RepeatEndValueTextBox.SetBinding(TextBox.TextProperty, repeatEndValueBinding);

            var repeatPeriodBinding = new Binding("Value.RepeatPeriod")
            {
                Source = this,
                Mode = BindingMode.TwoWay,
                StringFormat = "F2",
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            RepeatPeriodTextBox.SetBinding(TextBox.TextProperty, repeatPeriodBinding);

            var randomStartValueBinding = new Binding("Value.StartValue")
            {
                Source = this,
                Mode = BindingMode.TwoWay,
                StringFormat = format,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            RandomStartValueTextBox.SetBinding(TextBox.TextProperty, randomStartValueBinding);

            var randomEndValueBinding = new Binding("Value.EndValue")
            {
                Source = this,
                Mode = BindingMode.TwoWay,
                StringFormat = format,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            RandomEndValueTextBox.SetBinding(TextBox.TextProperty, randomEndValueBinding);

            var randomPeriodBinding = new Binding("Value.RandomPeriod")
            {
                Source = this,
                Mode = BindingMode.TwoWay,
                StringFormat = "F2",
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            RandomPeriodTextBox.SetBinding(TextBox.TextProperty, randomPeriodBinding);
        }

        private void MenuItem_Mode_Click(object sender, RoutedEventArgs e)
        {
            if (Value == null || sender is not MenuItem menuItem || menuItem.Tag is not AnimationMode newMode)
            {
                return;
            }

            if (Value.Mode == AnimationMode.Fixed && newMode != AnimationMode.Fixed)
            {
                Value.StartValue = Value.Value;
                Value.EndValue = Value.Value;
            }

            Value.Mode = newMode;
        }

        private void AnimationMenuButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (AnimationMenuButton.ContextMenu != null)
            {
                AnimationMenuButton.ContextMenu.PlacementTarget = AnimationMenuButton;
                AnimationMenuButton.ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }
    }

    internal class AnimationModeToContentConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is AnimationMode mode)
            {
                return mode switch
                {
                    AnimationMode.Fixed => "-",
                    AnimationMode.Linear => "直",
                    AnimationMode.Random => "ラ",
                    AnimationMode.Repeat => "反",
                    _ => "-",
                };
            }
            return "-";
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

    internal class AnimationModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is AnimationMode mode && parameter is string targetModeStr)
            {
                if (Enum.TryParse<AnimationMode>(targetModeStr, true, out var targetMode))
                {
                    return mode == targetMode ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}