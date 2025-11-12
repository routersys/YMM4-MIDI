using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MIDI.Shape.MidiPianoRoll.Controls;
using MIDI.Shape.MidiPianoRoll.Models;

namespace MIDI.Shape.MidiPianoRoll.Effects.Default
{
    public class NoteHitEffectParameter : EffectParameterBase
    {
        [Display(Name = "ヒット持続時間", Description = "鍵盤のヒットエフェクトの表示時間（秒）。")]
        [MidiAnimatableSlider(0.01, 1.0, "F2", "秒")]
        public AnimatableDouble NoteHitEffectDuration { get; } = new AnimatableDouble(0.1);

        public NoteHitEffectParameter()
        {
            RegisterAnimatable(NoteHitEffectDuration);
        }

        public override string EffectName => "ノートヒットエフェクト";

        public override SharedDataBase CreateSharedData() => new SharedData(this);

        public class SharedData : EffectParameterBase.SharedDataBase
        {
            public AnimatableDouble.SharedData NoteHitEffectDuration { get; set; }

            public SharedData()
            {
                NoteHitEffectDuration = new AnimatableDouble.SharedData(new AnimatableDouble(0.1));
            }

            public SharedData(NoteHitEffectParameter p) : base(p)
            {
                NoteHitEffectDuration = new AnimatableDouble.SharedData(p.NoteHitEffectDuration);
            }

            public override void Apply(EffectParameterBase p)
            {
                base.Apply(p);
                if (p is NoteHitEffectParameter param)
                {
                    NoteHitEffectDuration.Apply(param.NoteHitEffectDuration);
                }
            }

            public override Type GetParameterType() => typeof(NoteHitEffectParameter);
        }
    }
}