using System.Collections.Generic;
using System.Windows;

namespace MIDI
{
    public partial class SavePresetOptionsWindow : Window
    {
        public List<string>? SelectedCategories { get; private set; }

        public SavePresetOptionsWindow()
        {
            InitializeComponent();
            var viewModel = new SavePresetOptionsViewModel();
            DataContext = viewModel;
            viewModel.CloseAction = (selected) =>
            {
                SelectedCategories = selected;
                DialogResult = selected != null;
                Close();
            };
        }
    }
}