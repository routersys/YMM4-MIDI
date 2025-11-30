using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Views.Converters;
using MIDI.AudioEffect.EQUALIZER.Views;

namespace MIDI.AudioEffect.EQUALIZER.Views
{
    internal class EqualizerEditorAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create() => new EqualizerControl();
        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            var editor = (EqualizerControl)control;
            editor.SetBinding(EqualizerControl.ItemsSourceProperty, ItemPropertiesBinding.Create2(itemProperties));
        }
        public override void ClearBindings(FrameworkElement control) => BindingOperations.ClearBinding(control, EqualizerControl.ItemsSourceProperty);
    }
}