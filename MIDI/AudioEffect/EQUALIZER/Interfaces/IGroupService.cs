using MIDI.AudioEffect.EQUALIZER.Models;
using System.Collections.ObjectModel;

namespace MIDI.AudioEffect.EQUALIZER.Interfaces
{
    public interface IGroupService
    {
        ObservableCollection<GroupItem> UserGroups { get; }
        void AddGroup(string name);
        void DeleteGroup(GroupItem group);
        void MoveGroupUp(GroupItem group);
        void MoveGroupDown(GroupItem group);
        void Save();
        void Load();
    }
}