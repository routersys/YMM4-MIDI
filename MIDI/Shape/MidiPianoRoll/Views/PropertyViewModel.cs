using MIDI.Shape.MidiPianoRoll.Controls;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Windows;
using System.Windows.Data;

namespace MIDI.Shape.MidiPianoRoll.Views
{
    internal class PropertyViewModel
    {
        public string DisplayName { get; }
        public string Description { get; }
        public FrameworkElement EditorControl { get; }

        public PropertyViewModel(object instance, PropertyInfo propertyInfo, DisplayAttribute displayAttribute, MidiPropertyEditorAttribute editorAttribute)
        {
            DisplayName = displayAttribute.GetName() ?? propertyInfo.Name;
            Description = displayAttribute.GetDescription() ?? "";

            EditorControl = editorAttribute.CreateElement(propertyInfo);

            var bindingMode = propertyInfo.CanWrite ? BindingMode.TwoWay : BindingMode.OneWay;

            var binding = new Binding(propertyInfo.Name)
            {
                Source = instance,
                Mode = bindingMode,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            var valueProperty = EditorControl.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (valueProperty != null)
            {
                var dp = DependencyPropertyDescriptor.FromName(valueProperty.Name, valueProperty.DeclaringType, EditorControl.GetType());
                if (dp != null)
                {
                    BindingOperations.SetBinding(EditorControl, dp.DependencyProperty, binding);
                }
            }
        }
    }
}