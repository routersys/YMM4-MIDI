using System;
using System.Reflection;
using System.Windows;
using MIDI.Shape.MidiPianoRoll.Models;

namespace MIDI.Shape.MidiPianoRoll.Controls
{
    public class MidiAnimatableSliderAttribute : MidiPropertyEditorAttribute
    {
        public double Minimum { get; }
        public double Maximum { get; }
        public string StringFormat { get; }
        public string Unit { get; }

        public MidiAnimatableSliderAttribute(double minimum, double maximum, string stringFormat = "F2", string unit = "")
        {
            Minimum = minimum;
            Maximum = maximum;
            StringFormat = stringFormat;
            Unit = unit;
        }

        public override FrameworkElement CreateElement(PropertyInfo propertyInfo)
        {
            if (propertyInfo.PropertyType != typeof(AnimatableDouble))
            {
                throw new InvalidOperationException($"MidiAnimatableSliderAttribute requires a property of type {nameof(AnimatableDouble)}.");
            }

            return new MidiAnimatableSlider
            {
                Minimum = Minimum,
                Maximum = Maximum,
                StringFormat = StringFormat,
                Unit = Unit
            };
        }
    }
}