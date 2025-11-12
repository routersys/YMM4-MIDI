using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using MIDI.UI.ViewModels.MidiEditor;

namespace MIDI.Voice.ViewModels
{
    public class RenameVoiceModelViewModel : ViewModelBase
    {
        private readonly IEnumerable<string> _existingNames;
        private readonly string _originalName;

        private string _newModelName = "";
        public string NewModelName
        {
            get => _newModelName;
            set
            {
                if (SetField(ref _newModelName, value))
                {
                    OnPropertyChanged(nameof(CanConfirm));
                }
            }
        }

        public bool CanConfirm => !string.IsNullOrWhiteSpace(NewModelName) &&
                                  NewModelName != _originalName &&
                                  !_existingNames.Contains(NewModelName);

        public RenameVoiceModelViewModel(string originalName, IEnumerable<string> existingNames)
        {
            _originalName = originalName;
            _newModelName = originalName;
            _existingNames = existingNames.Where(n => n != originalName);
        }
    }
}