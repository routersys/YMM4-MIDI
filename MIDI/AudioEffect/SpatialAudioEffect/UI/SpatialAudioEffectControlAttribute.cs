using System;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace MIDI.AudioEffect.SpatialAudioEffect.UI
{
    internal class SpatialAudioEffectControlAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new SpatialAudioEffectControl();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is SpatialAudioEffectControl spatialControl)
            {
                spatialControl.SetItemProperties(itemProperties);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is SpatialAudioEffectControl spatialControl)
            {
                spatialControl.ClearItemProperties();
            }
        }
    }
}