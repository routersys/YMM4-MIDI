using System;
using System.Reflection;
using System.Windows;

namespace MIDI.Shape.MidiPianoRoll.Controls
{
    public class MidiToggleSliderAttribute : MidiPropertyEditorAttribute
    {
        public override FrameworkElement CreateElement(PropertyInfo propertyInfo)
        {
            return new MidiToggleSlider();
        }
    }
}