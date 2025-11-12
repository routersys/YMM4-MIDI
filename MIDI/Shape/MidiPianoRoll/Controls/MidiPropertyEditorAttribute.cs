using System;
using System.Reflection;
using System.Windows;

namespace MIDI.Shape.MidiPianoRoll.Controls
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public abstract class MidiPropertyEditorAttribute : Attribute
    {
        public abstract FrameworkElement CreateElement(PropertyInfo propertyInfo);
    }
}