using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Shape;

namespace MIDI.Shape.MidiPianoRoll.Views
{
    public partial class TimeShiftControl : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public TimeShiftControl()
        {
            InitializeComponent();
            this.Loaded += (s, e) => UpdateButtons();
        }

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(TimeShiftControl),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public TimeSpan ValueTimeSpan
        {
            get { return (TimeSpan)GetValue(ValueTimeSpanProperty); }
            private set { SetValue(ValueTimeSpanPropertyKey, value); }
        }
        private static readonly DependencyPropertyKey ValueTimeSpanPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(ValueTimeSpan),
                typeof(TimeSpan),
                typeof(TimeShiftControl),
                new PropertyMetadata(TimeSpan.Zero));
        public static readonly DependencyProperty ValueTimeSpanProperty = ValueTimeSpanPropertyKey.DependencyProperty;


        public double MidiDurationSeconds
        {
            get { return (double)GetValue(MidiDurationSecondsProperty); }
            set { SetValue(MidiDurationSecondsProperty, value); }
        }
        public static readonly DependencyProperty MidiDurationSecondsProperty =
            DependencyProperty.Register(nameof(MidiDurationSeconds), typeof(double), typeof(TimeShiftControl), new PropertyMetadata(0.0, OnBoundaryChanged));

        public Animation PlaybackSpeed
        {
            get { return (Animation)GetValue(PlaybackSpeedProperty); }
            set { SetValue(PlaybackSpeedProperty, value); }
        }
        public static readonly DependencyProperty PlaybackSpeedProperty =
            DependencyProperty.Register(nameof(PlaybackSpeed), typeof(Animation), typeof(TimeShiftControl), new PropertyMetadata(null, OnBoundaryChanged));


        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TimeShiftControl control)
            {
                try
                {
                    control.ValueTimeSpan = TimeSpan.FromSeconds((double)e.NewValue);
                }
                catch (OverflowException)
                {
                    control.ValueTimeSpan = (double)e.NewValue > 0 ? TimeSpan.MaxValue : TimeSpan.MinValue;
                }
                control.UpdateButtons();
            }
        }

        private static void OnBoundaryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as TimeShiftControl)?.UpdateButtons();
        }

        private void UpdateButtons()
        {
            if (HourUpButton == null) return;

            HourUpButton.IsEnabled = true;
            HourDownButton.IsEnabled = true;
            MinuteUpButton.IsEnabled = true;
            MinuteDownButton.IsEnabled = true;
            SecondUpButton.IsEnabled = true;
            SecondDownButton.IsEnabled = true;
            MsUpButton.IsEnabled = true;
            MsDownButton.IsEnabled = true;
        }

        private void HourUp_Click(object sender, RoutedEventArgs e)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            Value += 3600;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void HourDown_Click(object sender, RoutedEventArgs e)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            Value -= 3600;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void MinuteUp_Click(object sender, RoutedEventArgs e)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            Value += 60;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void MinuteDown_Click(object sender, RoutedEventArgs e)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            Value -= 60;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void SecondUp_Click(object sender, RoutedEventArgs e)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            Value += 1;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void SecondDown_Click(object sender, RoutedEventArgs e)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            Value -= 1;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void MsUp_Click(object sender, RoutedEventArgs e)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            Value += 0.1;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void MsDown_Click(object sender, RoutedEventArgs e)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            Value -= 0.1;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }
}