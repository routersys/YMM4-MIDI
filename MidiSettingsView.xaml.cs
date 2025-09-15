using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace MIDI
{
    public partial class MidiSettingsView : UserControl
    {
        public MidiSettingsView()
        {
            InitializeComponent();
            DataContext = new MidiSettingsViewModel();
        }
    }

    public class IntListToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<int> intList)
            {
                return string.Join(",", intList);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                var intList = str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(s => int.TryParse(s.Trim(), out var i) ? i : (int?)null)
                                 .Where(i => i.HasValue)
                                 .Select(i => i!.Value);

                if (targetType == typeof(ObservableCollection<int>))
                {
                    return new ObservableCollection<int>(intList);
                }
                return new List<int>(intList);
            }

            if (targetType == typeof(ObservableCollection<int>))
            {
                return new ObservableCollection<int>();
            }
            return new List<int>();
        }
    }
}