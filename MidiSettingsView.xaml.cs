using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace MIDI
{
    public partial class MidiSettingsView : UserControl
    {
        public MidiSettingsView()
        {
            InitializeComponent();
            DataContext = new MidiSettingsViewModel();
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is SoundFontFileViewModel vm)
            {
                if (DataContext is MidiSettingsViewModel viewModel && viewModel.EditSoundFontRuleCommand.CanExecute(vm))
                {
                    viewModel.EditSoundFontRuleCommand.Execute(vm);
                }
            }
        }
    }

    public class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            var type = value.GetType();
            if (!type.IsEnum) return value.ToString()!;

            var member = type.GetMember(value.ToString()!).FirstOrDefault();
            var description = member?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString()!;
            return description;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
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

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}