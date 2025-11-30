using MIDI.AudioEffect.EQUALIZER.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MIDI.AudioEffect.EQUALIZER.Interfaces
{
    public interface IPresetService
    {
        event EventHandler PresetsChanged;
        List<string> GetAllPresetNames();
        PresetInfo GetPresetInfo(string name);
        ObservableCollection<EQBand>? LoadPreset(string name);
        bool SavePreset(string name, IEnumerable<EQBand> bands);
        void DeletePreset(string name);
        bool RenamePreset(string oldName, string newName);
        void SetPresetGroup(string name, string group);
        void SetPresetFavorite(string name, bool isFavorite);
        bool ExportPreset(string name, string exportPath);
        bool ImportPreset(string importPath, string name);
    }
}