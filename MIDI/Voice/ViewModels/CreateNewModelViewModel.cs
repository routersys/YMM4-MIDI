using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using MIDI.UI.ViewModels.MidiEditor;
using MIDI.Voice.Models;

namespace MIDI.Voice.ViewModels
{
    public class CreateNewModelViewModel : ViewModelBase
    {
        private readonly IEnumerable<string> _existingNames;

        private string _modelName = "";
        public string ModelName
        {
            get => _modelName;
            set
            {
                if (SetField(ref _modelName, value))
                {
                    OnPropertyChanged(nameof(CanConfirm));
                }
            }
        }

        private ModelType _selectedModelType = ModelType.InternalSynth;
        public ModelType SelectedModelType
        {
            get => _selectedModelType;
            set => SetField(ref _selectedModelType, value);
        }

        public bool CanConfirm => !string.IsNullOrWhiteSpace(ModelName) &&
                                  !_existingNames.Contains(ModelName);

        public CreateNewModelViewModel(IEnumerable<string> existingNames)
        {
            _existingNames = existingNames;
            SetDefaultName();
        }

        private void SetDefaultName()
        {
            var baseName = SelectedModelType switch
            {
                ModelType.SoundFont => Translate.NewSFModelName,
                ModelType.UTAU => Translate.NewUtauModelName,
                _ => Translate.NewSynthModelName,
            };

            ModelName = baseName;
            int count = 1;
            while (_existingNames.Contains(ModelName))
            {
                count++;
                ModelName = $"{baseName} {count}";
            }
        }

        public override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName == nameof(SelectedModelType))
            {
                SetDefaultName();
            }
        }
    }
}