using System;
using System.Reflection;
using System.Windows;

namespace MIDI.Shape.MidiPianoRoll.Controls
{
    public class MidiColorPickerAttribute : MidiPropertyEditorAttribute
    {
        public override FrameworkElement CreateElement(PropertyInfo propertyInfo)
        {
            return new MidiColorPicker();
        }
    }
}