using System.Linq;
using System.Windows;
using System.Windows.Data;
using MIDI.Shape.MidiPianoRoll.Views;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace MIDI.Shape.MidiPianoRoll.Attributes
{
    internal class EffectSelectorAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new EffectSelector();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not EffectSelector editor) return;

            var effectsProps = itemProperties
                .Where(p => p.PropertyInfo.Name == nameof(MidiPianoRollParameter.Effects))
                .ToArray();

            if (effectsProps.Length > 0)
                editor.SetBinding(EffectSelector.ItemsSourceProperty, ItemPropertiesBinding.Create2(effectsProps));
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not EffectSelector editor) return;
            BindingOperations.ClearBinding(editor, EffectSelector.ItemsSourceProperty);
        }
    }
}