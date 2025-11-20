using System;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.UI
{
    internal class MultibandSaturatorControlAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new MultibandSaturatorControl();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is MultibandSaturatorControl c)
            {
                c.SetItemProperties(itemProperties);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is MultibandSaturatorControl c)
            {
                c.ClearItemProperties();
            }
        }
    }
}