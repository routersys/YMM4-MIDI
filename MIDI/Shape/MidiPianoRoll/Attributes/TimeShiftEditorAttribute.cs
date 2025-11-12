using System.Linq;
using System.Windows;
using System.Windows.Data;
using MIDI.Shape.MidiPianoRoll.Views;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace MIDI.Shape.MidiPianoRoll.Attributes
{
    internal class TimeShiftEditorAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new TimeShiftControl();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not TimeShiftControl editor) return;

            var prop = itemProperties.FirstOrDefault(p => p.PropertyInfo.Name == nameof(MidiPianoRollParameter.TimeShift));
            if (prop != null)
                editor.SetBinding(TimeShiftControl.ValueProperty, ItemPropertiesBinding.Create2(new[] { prop }));

            var durationProp = itemProperties.FirstOrDefault(p => p.PropertyOwner is MidiPianoRollParameter && p.PropertyInfo.Name == nameof(MidiPianoRollParameter.MidiDurationSeconds));
            if (durationProp != null)
                editor.SetBinding(TimeShiftControl.MidiDurationSecondsProperty, ItemPropertiesBinding.Create2(new[] { durationProp }));

            var speedProp = itemProperties.FirstOrDefault(p => p.PropertyOwner is MidiPianoRollParameter && p.PropertyInfo.Name == nameof(MidiPianoRollParameter.PlaybackSpeed));
            if (speedProp != null)
                editor.SetBinding(TimeShiftControl.PlaybackSpeedProperty, ItemPropertiesBinding.Create2(new[] { speedProp }));
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not TimeShiftControl editor) return;
            BindingOperations.ClearBinding(editor, TimeShiftControl.ValueProperty);
            BindingOperations.ClearBinding(editor, TimeShiftControl.MidiDurationSecondsProperty);
            BindingOperations.ClearBinding(editor, TimeShiftControl.PlaybackSpeedProperty);
        }
    }
}