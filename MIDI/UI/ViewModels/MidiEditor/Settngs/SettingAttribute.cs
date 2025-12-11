using System;

namespace MIDI.UI.ViewModels.MidiEditor.Settings
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingAttribute : Attribute
    {
        public string Name { get; }
        public string? Description { get; set; }

        public SettingAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class SettingGroupAttribute : Attribute
    {
        public string Name { get; }
        public string? Icon { get; set; }

        public SettingGroupAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class MajorSettingGroupAttribute : Attribute
    {
        public string Name { get; }

        public MajorSettingGroupAttribute(string name)
        {
            Name = name;
        }
    }
}