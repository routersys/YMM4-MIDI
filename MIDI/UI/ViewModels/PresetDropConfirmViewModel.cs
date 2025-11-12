using System.Collections.Generic;
using System.Windows.Input;
using MIDI.UI.Commands;

namespace MIDI
{
    public class PresetDropConfirmViewModel
    {
        public PresetCertificate? Certificate { get; }
        public List<string> ChangedItems { get; }
        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }
        public System.Action<bool>? CloseAction { get; set; }

        public PresetDropConfirmViewModel(PresetCertificate? certificate, List<string> changedItems)
        {
            Certificate = certificate;
            ChangedItems = changedItems;
            ApplyCommand = new RelayCommand(_ => CloseAction?.Invoke(true));
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }
    }
}