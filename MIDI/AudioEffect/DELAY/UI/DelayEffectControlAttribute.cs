using System;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace MIDI.AudioEffect.DELAY.UI
{
    internal class DelayEffectControlAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new DelayEffectControl();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is DelayEffectControl delayControl)
            {
                delayControl.SetItemProperties(itemProperties);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is DelayEffectControl delayControl)
            {
                delayControl.ClearItemProperties();
            }
        }
    }
}