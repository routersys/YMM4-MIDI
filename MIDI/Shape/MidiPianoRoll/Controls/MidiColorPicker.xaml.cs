using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MIDI.Shape.MidiPianoRoll.Models;

namespace MIDI.Shape.MidiPianoRoll.Controls
{
    public partial class MidiColorPicker : UserControl
    {
        public MidiColorPicker()
        {
            InitializeConverters();
            InitializeComponent();
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

        public AnimatableColor Value
        {
            get { return (AnimatableColor)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(AnimatableColor),
                typeof(MidiColorPicker),
                new FrameworkPropertyMetadata(new AnimatableColor(Colors.White), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


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

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            Popup? popup = null;
            switch (button.Name)
            {
                case nameof(FixedColorButton): popup = FixedColorPopup; break;
                case nameof(StartColorButton): popup = StartColorPopup; break;
                case nameof(EndColorButton): popup = EndColorPopup; break;
                case nameof(RepeatStartColorButton): popup = RepeatStartColorPopup; break;
                case nameof(RepeatEndColorButton): popup = RepeatEndColorPopup; break;
                case nameof(RandomStartColorButton): popup = RandomStartColorPopup; break;
                case nameof(RandomEndColorButton): popup = RandomEndColorPopup; break;
            }

            if (popup != null)
            {
                popup.IsOpen = true;
            }
        }
    }
}