using MIDI.AudioEffect.EQUALIZER.Interfaces;
using MIDI.AudioEffect.EQUALIZER.Models;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MIDI.AudioEffect.EQUALIZER.Services
{
    public class GroupService : IGroupService
    {
        private readonly string _configPath;
        private ObservableCollection<GroupItem> _userGroups = new();

        public ObservableCollection<GroupItem> UserGroups => _userGroups;

        public GroupService()
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            _configPath = Path.Combine(assemblyLocation, "Config", "groups.json");

            Load();
        }

        public void Load()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    var groups = JsonConvert.DeserializeObject<ObservableCollection<GroupItem>>(json);
                    if (groups != null)
                    {
                        _userGroups = groups;
                        EnsureOtherGroupExists();
                    }
                }
                catch
                {
                    InitializeDefaultGroups();
                }
            }
            else
            {
                InitializeDefaultGroups();
            }

            _userGroups.CollectionChanged += (s, e) => Save();
        }

        private void InitializeDefaultGroups()
        {
            _userGroups = new ObservableCollection<GroupItem>
            {
                new GroupItem("ボーカル", "vocal"),
                new GroupItem("BGM", "bgm"),
                new GroupItem("効果音", "sfx"),
                new GroupItem("その他", "other")
            };
        }

        private void EnsureOtherGroupExists()
        {
            var other = _userGroups.FirstOrDefault(g => g.Tag == "other");
            if (other == null)
            {
                _userGroups.Add(new GroupItem("その他", "other"));
            }
            else
            {
                if (_userGroups.IndexOf(other) != _userGroups.Count - 1)
                {
                    _userGroups.Remove(other);
                    _userGroups.Add(other);
                }
            }
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonConvert.SerializeObject(_userGroups, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        public void AddGroup(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            if (_userGroups.Any(g => g.Name == name)) return;

            var newItem = new GroupItem(name, Guid.NewGuid().ToString());

            var otherGroup = _userGroups.FirstOrDefault(g => g.Tag == "other");
            if (otherGroup != null)
            {
                var index = _userGroups.IndexOf(otherGroup);
                _userGroups.Insert(index, newItem);
            }
            else
            {
                _userGroups.Add(newItem);
            }
        }

        public void DeleteGroup(GroupItem group)
        {
            if (group == null) return;
            if (group.Tag == "other") return;

            if (_userGroups.Contains(group))
            {
                _userGroups.Remove(group);
            }
        }

        public void MoveGroupUp(GroupItem group)
        {
            if (group == null || group.Tag == "other") return;

            var index = _userGroups.IndexOf(group);
            if (index > 0)
            {
                _userGroups.Move(index, index - 1);
            }
        }

        public void MoveGroupDown(GroupItem group)
        {
            if (group == null || group.Tag == "other") return;

            var index = _userGroups.IndexOf(group);
            if (index >= 0 && index < _userGroups.Count - 2)
            {
                _userGroups.Move(index, index + 1);
            }
        }
    }
}