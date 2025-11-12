using System.Windows;
using System.Windows.Controls;
using MIDI.UI.ViewModels;

namespace MIDI.UI.Views
{
    public class WizardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? BooleanTemplate { get; set; }
        public DataTemplate? StringTemplate { get; set; }
        public DataTemplate? IntTemplate { get; set; }
        public DataTemplate? DoubleTemplate { get; set; }
        public DataTemplate? ColorTemplate { get; set; }
        public DataTemplate? EnumTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is WizardSettingItem setting)
            {
                return setting.Type switch
                {
                    SettingType.Boolean => BooleanTemplate!,
                    SettingType.String => StringTemplate!,
                    SettingType.Int => IntTemplate!,
                    SettingType.Double => DoubleTemplate!,
                    SettingType.Color => ColorTemplate!,
                    SettingType.Enum => EnumTemplate!,
                    _ => base.SelectTemplate(item, container)!,
                };
            }
            return base.SelectTemplate(item, container)!;
        }
    }
}