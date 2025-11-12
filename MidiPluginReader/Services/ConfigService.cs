using MidiPlugin.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MidiPlugin.Services
{
    public class ConfigService
    {
        private readonly string _configPath;
        private readonly string _currentExePath;

        public ConfigService()
        {
            var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appConfigFolder = Path.Combine(appDataFolder, AppConfig.AppName);
            if (!Directory.Exists(appConfigFolder))
            {
                Directory.CreateDirectory(appConfigFolder);
            }
            _configPath = Path.Combine(appConfigFolder, "settings.txt");
            _currentExePath = Assembly.GetExecutingAssembly().Location;
        }

        public List<string> LoadPrioritizedExePaths()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    return new List<string>();
                }
                return File.ReadAllLines(_configPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public void SavePrioritizedExePaths(List<string> prioritizedPaths)
        {
            File.WriteAllLines(_configPath, prioritizedPaths);
        }

        public void RegisterCurrentExecutable()
        {
            var paths = LoadPrioritizedExePaths();
            if (!paths.Any(p => p.Equals(_currentExePath, StringComparison.OrdinalIgnoreCase)))
            {
                paths.Add(_currentExePath);
                SavePrioritizedExePaths(paths);
            }
        }
    }
}