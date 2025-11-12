using System;
using System.Globalization;
using System.Windows.Data;

namespace MIDI.UI.Views.MidiEditor.Converters
{
    public class DbToPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double db)
            {
                double minDb = -70.0;
                double maxDb = 9.0;
                if (double.IsNegativeInfinity(db) || db < minDb) db = minDb;
                if (db > maxDb) db = maxDb;
                double percentage = (db - minDb) / (maxDb - minDb);
                return Math.Max(0, Math.Min(1, percentage));
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HeightPercentageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || !(values[0] is double) || !(values[1] is double))
                return 0.0;
            double percentage = (double)values[0];
            double actualHeight = (double)values[1];
            return percentage * actualHeight;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class VuToAngleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double vu)
            {
                double db = vu;
                if (double.IsNaN(db) || double.IsInfinity(db) || db < -20.0) db = -20.0;
                if (db > 3.0) db = 3.0;

                double range = 23.0;
                double angleRange = 90.0;

                double normalized = (db + 20.0) / range;
                double angle = (normalized * angleRange) - 45.0;

                return Math.Max(-45.0, Math.Min(45.0, angle));
            }
            return -45.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LufsToPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double lufs)
            {
                double minLufs = -70.0;
                double maxLufs = -5.0;
                if (double.IsNegativeInfinity(lufs) || lufs < minLufs) lufs = minLufs;
                if (lufs > maxLufs) lufs = maxLufs;

                double percentage = (lufs - minLufs) / (maxLufs - minLufs);
                return Math.Max(0, Math.Min(1, percentage));
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}