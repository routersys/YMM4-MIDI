using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using MIDI.Shape.MidiPianoRoll.Controls;
using MIDI.Shape.MidiPianoRoll.Effects.Default;
using MIDI.Shape.MidiPianoRoll.Models;

namespace MIDI.Shape.MidiPianoRoll.Effects
{
    public abstract class EffectParameterBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private readonly List<AnimatableDouble> _registeredAnimatables = new List<AnimatableDouble>();

        protected void RegisterAnimatable(AnimatableDouble animatable, [CallerMemberName] string? propertyName = null)
        {
            if (animatable == null || string.IsNullOrEmpty(propertyName) || _registeredAnimatables.Contains(animatable)) return;

            animatable.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(propertyName);
            };
            _registeredAnimatables.Add(animatable);
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsEnabled { get => isEnabled; set => SetProperty(ref isEnabled, value); }
        private bool isEnabled = true;

        public abstract string EffectName { get; }

        public abstract SharedDataBase CreateSharedData();

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(NoteHitEffectParameter.SharedData), "NoteHit")]
        [JsonDerivedType(typeof(NoteSplashEffectParameter.SharedData), "NoteSplash")]
        public abstract class SharedDataBase
        {
            public bool IsEnabled { get; set; }

            public SharedDataBase() { }

            public SharedDataBase(EffectParameterBase p)
            {
                IsEnabled = p.IsEnabled;
            }

            public virtual void Apply(EffectParameterBase p)
            {
                p.IsEnabled = IsEnabled;
            }

            public abstract Type GetParameterType();
        }
    }
}