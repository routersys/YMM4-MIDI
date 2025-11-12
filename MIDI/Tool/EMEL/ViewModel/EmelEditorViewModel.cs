using ICSharpCode.AvalonEdit.Document;
using MIDI.UI.Commands;
using MIDI.UI.ViewModels.MidiEditor;
using MIDI.Voice.EMEL;
using MIDI.Voice.EMEL.Parsing;
using System.Text;
using System.Windows.Input;
using MIDI.Voice.EMEL.Errors;
using System;

namespace MIDI.Tool.EMEL.ViewModel
{
    public class EmelEditorViewModel : ViewModelBase
    {
        private TextDocument _document;
        private string _compiledOutput;
        private string _errorMessage;
        private int _selectedTabIndex;
        private int _currentErrorLine;

        public TextDocument Document
        {
            get => _document;
            set => SetField(ref _document, value);
        }

        public string CompiledOutput
        {
            get => _compiledOutput;
            set => SetField(ref _compiledOutput, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetField(ref _errorMessage, value);
        }

        public int CurrentErrorLine
        {
            get => _currentErrorLine;
            set => SetField(ref _currentErrorLine, value);
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetField(ref _selectedTabIndex, value);
        }

        public ICommand CompileCommand { get; }
        public ICommand DocumentTextChangedCommand { get; }

        public EmelEditorViewModel()
        {
            _document = new TextDocument();
            _document.Text = "#!EMEL\n\nTrack 1 {\n    Note(C4, 1.0, 100);\n}";
            _compiledOutput = string.Empty;
            _errorMessage = string.Empty;
            _selectedTabIndex = 0;
            _currentErrorLine = 0;

            CompileCommand = new RelayCommand(CompileEmel);
            DocumentTextChangedCommand = new RelayCommand(CheckForErrors);
        }

        private void CompileEmel(object? parameter)
        {
            CurrentErrorLine = 0;
            try
            {
                string code = Document.Text;
                var compiler = new EmelCompiler();
                string result = compiler.Compile(code);

                CompiledOutput = result;
                ErrorMessage = string.Empty;
                SelectedTabIndex = 0;
            }
            catch (EmelException ex)
            {
                CompiledOutput = string.Empty;
                ErrorMessage = $"[行: {ex.Line}, 列: {ex.Column}] {ex.Message}";
                CurrentErrorLine = ex.Line;
                SelectedTabIndex = 1;
            }
            catch (System.Exception ex)
            {
                CompiledOutput = string.Empty;
                ErrorMessage = $"予期しないコンパイルエラー:\n{ex.Message}\n{ex.StackTrace}";
                CurrentErrorLine = 0;
                SelectedTabIndex = 1;
            }
        }

        private void CheckForErrors(object? parameter)
        {
            CurrentErrorLine = 0;
            ErrorMessage = string.Empty;

            try
            {
                string code = Document.Text;
                var compiler = new EmelCompiler();
                compiler.Compile(code);
            }
            catch (EmelException ex)
            {
                ErrorMessage = $"[行: {ex.Line}, 列: {ex.Column}] {ex.Message}";
                CurrentErrorLine = ex.Line;
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"予期しないコンパイルエラー:\n{ex.Message}";
                CurrentErrorLine = 0;
            }
        }
    }
}