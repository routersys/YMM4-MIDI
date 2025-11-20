using System;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace MIDI.AudioEffect.ENHANCER.UI
{
    internal class EnhancerEffectControlAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new EnhancerEffectControl();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is EnhancerEffectControl enhancerControl)
            {
                enhancerControl.SetItemProperties(itemProperties);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is EnhancerEffectControl enhancerControl)
            {
                enhancerControl.ClearItemProperties();
            }
        }
    }
}