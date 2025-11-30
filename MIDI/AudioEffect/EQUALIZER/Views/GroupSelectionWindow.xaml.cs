using MIDI.AudioEffect.EQUALIZER.ViewModels;
using System.Windows;

namespace MIDI.AudioEffect.EQUALIZER.Views
{
    public partial class GroupSelectionWindow : Window
    {
        public string SelectedGroup => ((GroupSelectionViewModel)DataContext).SelectedGroup?.Key ?? "";

        public GroupSelectionWindow(string[] groupKeys, string[] groupNames, string currentGroup)
        {
            InitializeComponent();
            DataContext = new GroupSelectionViewModel(groupKeys, groupNames, currentGroup);
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