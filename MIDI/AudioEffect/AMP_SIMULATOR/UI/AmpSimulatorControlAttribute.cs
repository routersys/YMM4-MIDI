using System;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace MIDI.AudioEffect.AMP_SIMULATOR.UI
{
    internal class AmpSimulatorControlAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new AmpSimulatorControl();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is AmpSimulatorControl c)
            {
                c.SetItemProperties(itemProperties);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is AmpSimulatorControl c)
            {
                c.ClearItemProperties();
            }
        }
    }
}