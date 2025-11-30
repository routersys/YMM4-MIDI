using MIDI.AudioEffect.EQUALIZER.Interfaces;
using MIDI.AudioEffect.EQUALIZER.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace MIDI.AudioEffect.EQUALIZER.Services
{
    public class PresetService : IPresetService
    {
        private readonly string _presetsDir;
        private readonly string _metadataPath;
        private Dictionary<string, PresetMetadata> _presetMetadata = new();
        private readonly JsonSerializerSettings _serializerSettings;

        public event EventHandler? PresetsChanged;

        public PresetService()
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            _presetsDir = Path.Combine(assemblyLocation, "presets");
            _metadataPath = Path.Combine(_presetsDir, "_metadata.json");

            _serializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented
            };

            if (!Directory.Exists(_presetsDir))
            {
                Directory.CreateDirectory(_presetsDir);
            }
            LoadMetadata();
        }

        private void LoadMetadata()
        {
            if (File.Exists(_metadataPath))
            {
                try
                {
                    var json = File.ReadAllText(_metadataPath);
                    _presetMetadata = JsonConvert.DeserializeObject<Dictionary<string, PresetMetadata>>(json) ?? new();
                }
                catch
                {
                    _presetMetadata = new();
                }
            }
        }

        private void SaveMetadata()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_presetMetadata, Formatting.Indented);
                File.WriteAllText(_metadataPath, json);
            }
            catch { }
        }

        public List<string> GetAllPresetNames()
        {
            if (!Directory.Exists(_presetsDir)) return new List<string>();

            return Directory.GetFiles(_presetsDir, "*.json")
                            .Where(f => Path.GetFileName(f) != "_metadata.json")
                            .Select(Path.GetFileNameWithoutExtension)
                            .Where(name => name != null)
                            .OrderBy(name => name)
                            .ToList()!;
        }

        public PresetInfo GetPresetInfo(string name)
        {
            var metadata = _presetMetadata.GetValueOrDefault(name, new PresetMetadata());
            return new PresetInfo
            {
                Name = name,
                Group = metadata.Group,
                IsFavorite = metadata.IsFavorite
            };
        }

        public ObservableCollection<EQBand>? LoadPreset(string name)
        {
            var filePath = Path.Combine(_presetsDir, $"{name}.json");
            if (!File.Exists(filePath)) return null;

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<ObservableCollection<EQBand>>(json, _serializerSettings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"プリセットの読み込みに失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public bool SavePreset(string name, IEnumerable<EQBand> bands)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("無効なプリセット名です。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                var json = JsonConvert.SerializeObject(bands, _serializerSettings);
                File.WriteAllText(Path.Combine(_presetsDir, $"{name}.json"), json);

                if (!_presetMetadata.ContainsKey(name))
                {
                    _presetMetadata[name] = new PresetMetadata();
                }

                SaveMetadata();
                PresetsChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"プリセットの保存に失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public void DeletePreset(string name)
        {
            var filePath = Path.Combine(_presetsDir, $"{name}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _presetMetadata.Remove(name);
                SaveMetadata();
                PresetsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool RenamePreset(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("無効なプリセット名です。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            var oldPath = Path.Combine(_presetsDir, $"{oldName}.json");
            var newPath = Path.Combine(_presetsDir, $"{newName}.json");

            if (File.Exists(newPath))
            {
                MessageBox.Show("同じ名前のプリセットが既に存在します。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (File.Exists(oldPath))
            {
                File.Move(oldPath, newPath);

                if (_presetMetadata.ContainsKey(oldName))
                {
                    _presetMetadata[newName] = _presetMetadata[oldName];
                    _presetMetadata.Remove(oldName);
                    SaveMetadata();
                }

                PresetsChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        public void SetPresetGroup(string name, string group)
        {
            if (!_presetMetadata.ContainsKey(name))
            {
                _presetMetadata[name] = new PresetMetadata();
            }
            _presetMetadata[name].Group = group;
            SaveMetadata();
        }

        public void SetPresetFavorite(string name, bool isFavorite)
        {
            if (!_presetMetadata.ContainsKey(name))
            {
                _presetMetadata[name] = new PresetMetadata();
            }
            _presetMetadata[name].IsFavorite = isFavorite;
            SaveMetadata();
        }

        public bool ExportPreset(string name, string exportPath)
        {
            var sourcePath = Path.Combine(_presetsDir, $"{name}.json");
            if (!File.Exists(sourcePath)) return false;

            try
            {
                File.Copy(sourcePath, exportPath, true);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"プリセットのエクスポートに失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool ImportPreset(string importPath, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(importPath);
                var bands = JsonConvert.DeserializeObject<ObservableCollection<EQBand>>(json, _serializerSettings);

                if (bands == null) return false;

                var targetPath = Path.Combine(_presetsDir, $"{name}.json");
                File.WriteAllText(targetPath, json);

                if (!_presetMetadata.ContainsKey(name))
                {
                    _presetMetadata[name] = new PresetMetadata { Group = "other" };
                    SaveMetadata();
                }

                PresetsChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}