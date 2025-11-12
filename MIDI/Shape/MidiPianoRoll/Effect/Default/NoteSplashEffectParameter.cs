using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MIDI.Shape.MidiPianoRoll.Controls;
using MIDI.Shape.MidiPianoRoll.Models;

namespace MIDI.Shape.MidiPianoRoll.Effects.Default
{
    public class NoteSplashEffectParameter : EffectParameterBase
    {
        [Display(Name = "スプラッシュサイズ", Description = "ノートのスプラッシュエフェクトのサイズ（ピクセル）。")]
        [MidiAnimatableSlider(1, 100, "F0", "px")]
        public AnimatableDouble NoteSplashEffectSize { get; } = new AnimatableDouble(20);

        public NoteSplashEffectParameter()
        {
            RegisterAnimatable(NoteSplashEffectSize);
        }

        public override string EffectName => "ノートスプラッシュ";

        public override SharedDataBase CreateSharedData() => new SharedData(this);

        public class SharedData : EffectParameterBase.SharedDataBase
        {
            public AnimatableDouble.SharedData NoteSplashEffectSize { get; set; }

            public SharedData()
            {
                NoteSplashEffectSize = new AnimatableDouble.SharedData(new AnimatableDouble(20));
            }

            public SharedData(NoteSplashEffectParameter p) : base(p)
            {
                NoteSplashEffectSize = new AnimatableDouble.SharedData(p.NoteSplashEffectSize);
            }

            public override void Apply(EffectParameterBase p)
            {
                base.Apply(p);
                if (p is NoteSplashEffectParameter param)
                {
                    NoteSplashEffectSize.Apply(param.NoteSplashEffectSize);
                }
            }

            public override Type GetParameterType() => typeof(NoteSplashEffectParameter);
        }
    }
}