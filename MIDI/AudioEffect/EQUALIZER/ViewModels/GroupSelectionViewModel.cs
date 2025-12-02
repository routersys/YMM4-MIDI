using MIDI.AudioEffect.EQUALIZER.Models;
using MIDI.AudioEffect.EQUALIZER.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace MIDI.AudioEffect.EQUALIZER.ViewModels
{
    public class GroupSelectionViewModel : ViewModelBase
    {
        private GroupItem? _selectedGroup;

        public ObservableCollection<GroupItem> GroupOptions { get; } = new();

        public GroupItem? SelectedGroup
        {
            get => _selectedGroup;
            set => SetProperty(ref _selectedGroup, value);
        }

        public GroupSelectionViewModel(string currentGroup)
        {
            var groupService = ServiceLocator.GroupService;

            foreach (var g in groupService.UserGroups)
            {
                if (g.Tag != "favorites" && g.Tag != "")
                {
                    GroupOptions.Add(g);
                }
            }

            SelectedGroup = GroupOptions.FirstOrDefault(g => g.Tag == currentGroup) ?? GroupOptions.FirstOrDefault(g => g.Tag == "other") ?? GroupOptions.FirstOrDefault();
        }
    }
}