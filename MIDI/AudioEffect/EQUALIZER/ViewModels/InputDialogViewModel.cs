namespace MIDI.AudioEffect.EQUALIZER.ViewModels
{
    public class InputDialogViewModel : ViewModelBase
    {
        private string _title = "";
        private string _message = "";
        private string _inputText = "";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value);
        }

        public InputDialogViewModel(string message, string title, string defaultText = "")
        {
            Message = message;
            Title = title;
            InputText = defaultText;
        }
    }
}