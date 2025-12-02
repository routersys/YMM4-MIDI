using System;

namespace MIDI.AudioEffect.EQUALIZER.Models
{
    public class GroupItem
    {
        public string Name { get; set; }
        public string Tag { get; set; }

        public GroupItem(string name, string tag)
        {
            Name = name;
            Tag = tag;
        }

        public GroupItem() : this("", "") { }
    }
}