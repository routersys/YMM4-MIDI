using System.Collections.Generic;
using System.Linq;

namespace MIDI.AudioEffect.EQUALIZER.ViewModels
{
    public class GroupOption
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    public class GroupSelectionViewModel : ViewModelBase
    {
        private GroupOption? _selectedGroup;

        public List<GroupOption> GroupOptions { get; }

        public GroupOption? SelectedGroup
        {
            get => _selectedGroup;
            set => SetProperty(ref _selectedGroup, value);
        }

        public GroupSelectionViewModel(string[] groupKeys, string[] groupNames, string currentGroup)
        {
            GroupOptions = new List<GroupOption>();
            for (int i = 0; i < groupKeys.Length; i++)
            {
                GroupOptions.Add(new GroupOption
                {
                    Key = groupKeys[i],
                    DisplayName = groupNames[i]
                });
            }
            SelectedGroup = GroupOptions.FirstOrDefault(g => g.Key == currentGroup) ?? GroupOptions.FirstOrDefault();
        }
    }
}