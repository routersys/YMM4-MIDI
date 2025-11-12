using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MidiPlugin.Converters
{
    public class ColorBrightnessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush originalBrush && double.TryParse(parameter as string, out double factor))
            {
                Color originalColor = originalBrush.Color;

                byte r = (byte)Math.Max(0, Math.Min(255, originalColor.R * factor));
                byte g = (byte)Math.Max(0, Math.Min(255, originalColor.G * factor));
                byte b = (byte)Math.Max(0, Math.Min(255, originalColor.B * factor));

                return new SolidColorBrush(Color.FromRgb(r, g, b));
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}