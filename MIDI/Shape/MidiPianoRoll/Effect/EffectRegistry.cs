using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MIDI.Shape.MidiPianoRoll.Effects.Default;

namespace MIDI.Shape.MidiPianoRoll.Effects
{
    public static class EffectRegistry
    {
        private static readonly List<IEffectPlugin> _defaultPlugins = new List<IEffectPlugin>
        {
            new NoteHitEffectPlugin(),
            new NoteSplashEffectPlugin(),
        };

        private static readonly List<IEffectPlugin> _plugins;
        private static string BaseDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

        static EffectRegistry()
        {
            _plugins = new List<IEffectPlugin>(_defaultPlugins);
            LoadExternalPlugins();
        }

        private static void LoadExternalPlugins()
        {
            _plugins.RemoveAll(p => !_defaultPlugins.Contains(p));

            string extensionsDir = Path.Combine(BaseDir, "Extensions");

            if (!Directory.Exists(extensionsDir))
            {
                try
                {
                    Directory.CreateDirectory(extensionsDir);
                }
                catch (Exception)
                {
                    return;
                }
            }

            try
            {
                var dllFiles = Directory.GetFiles(extensionsDir, "*.dll", SearchOption.AllDirectories);

                foreach (var dllFile in dllFiles)
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(dllFile);
                        var pluginTypes = assembly.GetTypes()
                            .Where(t => typeof(IEffectPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                        foreach (var type in pluginTypes)
                        {
                            if (Activator.CreateInstance(type) is IEffectPlugin plugin)
                            {
                                if (!_plugins.Any(p => p.GetType() == type))
                                {
                                    _plugins.Add(plugin);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public static void ReloadPlugins()
        {
            LoadExternalPlugins();
        }

        public static bool IsDefaultPlugin(IEffectPlugin plugin)
        {
            if (plugin == null) return false;
            return _defaultPlugins.Any(p => p.GetType() == plugin.GetType());
        }

        public static IEnumerable<IEffectPlugin> GetPlugins()
        {
            return _plugins;
        }

        public static IEffectPlugin? GetPlugin(Type parameterType)
        {
            return _plugins.FirstOrDefault(p => p.ParameterType == parameterType);
        }
    }
}