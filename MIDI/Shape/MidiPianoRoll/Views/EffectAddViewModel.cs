using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using MIDI.Shape.MidiPianoRoll.Effects;
using MIDI.Shape.MidiPianoRoll.Models;

namespace MIDI.Shape.MidiPianoRoll.Views
{
    internal class EffectPluginViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public IEffectPlugin Plugin { get; }
        public string Name => Plugin.Name;
        public string GroupName => Plugin.GroupName;
        public string PluginIdentifier => Plugin.GetType().FullName ?? Plugin.Name;

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (SetProperty(ref _isFavorite, value))
                {
                    if (value)
                    {
                        PluginConfigManager.FavoritePlugins.Add(PluginIdentifier);
                    }
                    else
                    {
                        PluginConfigManager.FavoritePlugins.Remove(PluginIdentifier);
                    }
                    FavoriteChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? FavoriteChanged;

        public EffectPluginViewModel(IEffectPlugin plugin)
        {
            Plugin = plugin;
            _isFavorite = PluginConfigManager.FavoritePlugins.Contains(PluginIdentifier);
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal class EffectAddViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private readonly List<EffectPluginViewModel> _allPlugins;

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) UpdateFilteredPlugins(); }
        }

        private string _selectedGroup = "すべて";
        public string SelectedGroup
        {
            get => _selectedGroup;
            set { if (SetProperty(ref _selectedGroup, value)) UpdateFilteredPlugins(); }
        }

        private IEnumerable<EffectPluginViewModel> _filteredPlugins = Enumerable.Empty<EffectPluginViewModel>();
        public IEnumerable<EffectPluginViewModel> FilteredPlugins
        {
            get => _filteredPlugins;
            private set => SetProperty(ref _filteredPlugins, value);
        }

        public List<string> Groups { get; }

        public EffectAddViewModel()
        {
            _allPlugins = EffectRegistry.GetPlugins()
                .Select(p => new EffectPluginViewModel(p))
                .OrderBy(p => p.Name)
                .ToList();

            foreach (var vm in _allPlugins)
            {
                vm.FavoriteChanged += (s, e) =>
                {
                    if (SelectedGroup == "お気に入り")
                    {
                        UpdateFilteredPlugins();
                    }
                };
            }

            Groups = new List<string> { "すべて", "お気に入り" };
            Groups.AddRange(_allPlugins
                .Select(p => p.GroupName)
                .Distinct()
                .OrderBy(g => g));

            UpdateFilteredPlugins();
        }

        private void UpdateFilteredPlugins()
        {
            IEnumerable<EffectPluginViewModel> result = _allPlugins;

            if (SelectedGroup == "お気に入り")
            {
                result = result.Where(p => p.IsFavorite);
            }
            else if (SelectedGroup != "すべて")
            {
                result = result.Where(p => p.GroupName == SelectedGroup);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                result = result.Where(p =>
                    p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    p.GroupName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            FilteredPlugins = result.ToList();
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}