using MIDI.AudioEffect.EQUALIZER.ViewModels;
using System.Windows;

namespace MIDI.AudioEffect.EQUALIZER.Views
{
    public partial class GroupSelectionWindow : Window
    {
        public Models.GroupItem? SelectedGroup => ((GroupSelectionViewModel)DataContext).SelectedGroup;

        public GroupSelectionWindow(string currentGroup)
        {
            InitializeComponent();
            DataContext = new GroupSelectionViewModel(currentGroup);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (((GroupSelectionViewModel)DataContext).SelectedGroup != null)
            {
                DialogResult = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}