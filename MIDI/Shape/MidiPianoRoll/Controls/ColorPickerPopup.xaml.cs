using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MIDI.Shape.MidiPianoRoll.Controls
{
    public partial class ColorPickerPopup : UserControl
    {
        private readonly ColorPickerViewModel _viewModel;

        public ColorPickerPopup()
        {
            InitializeComponent();
            _viewModel = new ColorPickerViewModel();
            DataContext = _viewModel;
            _viewModel.ColorChanged += (s, color) => Color = color;
        }

        public Color Color
        {
            get { return (Color)GetValue(ColorProperty); }
            set { SetValue(ColorProperty, value); }
        }

        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register(
                nameof(Color),
                typeof(Color),
                typeof(ColorPickerPopup),
                new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPickerPopup picker && e.NewValue is Color newColor)
            {
                picker._viewModel.SetColor(newColor);
            }
        }
    }

    internal class ColorPickerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<Color>? ColorChanged;
        private bool _isUpdating = false;

        private byte _a;
        public byte A
        {
            get => _a;
            set { if (SetProperty(ref _a, value)) UpdateColorFromChannels(); }
        }

        private byte _r;
        public byte R
        {
            get => _r;
            set { if (SetProperty(ref _r, value)) UpdateColorFromChannels(); }
        }

        private byte _g;
        public byte G
        {
            get => _g;
            set { if (SetProperty(ref _g, value)) UpdateColorFromChannels(); }
        }

        private byte _b;
        public byte B
        {
            get => _b;
            set { if (SetProperty(ref _b, value)) UpdateColorFromChannels(); }
        }

        private string _hex = "#FFFFFFFF";
        public string Hex
        {
            get => _hex;
            set { if (SetProperty(ref _hex, value)) UpdateColorFromHex(); }
        }

        public void SetColor(Color color)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            A = color.A;
            R = color.R;
            G = color.G;
            B = color.B;
            Hex = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            _isUpdating = false;
        }

        private void UpdateColorFromChannels()
        {
            if (_isUpdating) return;
            _isUpdating = true;
            Hex = $"#{A:X2}{R:X2}{G:X2}{B:X2}";
            _isUpdating = false;
            ColorChanged?.Invoke(this, Color.FromArgb(A, R, G, B));
        }

        private void UpdateColorFromHex()
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                var hex = Hex.TrimStart('#');
                if (hex.Length == 8 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
                {
                    A = (byte)(argb >> 24);
                    R = (byte)(argb >> 16);
                    G = (byte)(argb >> 8);
                    B = (byte)(argb);
                    ColorChanged?.Invoke(this, Color.FromArgb(A, R, G, B));
                }
                else if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
                {
                    A = 255;
                    R = (byte)(rgb >> 16);
                    G = (byte)(rgb >> 8);
                    B = (byte)(rgb);
                    Hex = $"#{A:X2}{R:X2}{G:X2}{B:X2}";
                    ColorChanged?.Invoke(this, Color.FromArgb(A, R, G, B));
                }
            }
            catch { }
            _isUpdating = false;
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}