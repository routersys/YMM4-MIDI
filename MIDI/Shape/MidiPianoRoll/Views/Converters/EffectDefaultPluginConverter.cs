using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MIDI.Shape.MidiPianoRoll.Effects;

namespace MIDI.Shape.MidiPianoRoll.Views.Converters
{
    internal class EffectDefaultPluginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            IEffectPlugin? plugin = null;

            if (value is IEffectPlugin p)
            {
                plugin = p;
            }
            else if (value is EffectPluginViewModel vm)
            {
                plugin = vm.Plugin;
            }

            if (plugin != null)
            {
                return EffectRegistry.IsDefaultPlugin(plugin) ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}