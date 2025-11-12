using Microsoft.Win32;
using System.Reflection;
using MidiPlugin.Models;

namespace MidiPlugin.Services
{
    public class FileAssociationService
    {
        private readonly string _exePath = Assembly.GetExecutingAssembly().Location;

        private void RegisterAssociation(string extension, string protocolName, string description)
        {
            try
            {
                using (var classesRoot = Registry.CurrentUser.OpenSubKey("Software\\Classes", true))
                {
                    if (classesRoot == null) return;

                    using (var extKey = classesRoot.CreateSubKey(extension))
                    {
                        extKey?.SetValue("", protocolName);
                    }

                    using (var protocolKey = classesRoot.CreateSubKey(protocolName))
                    {
                        if (protocolKey == null) return;
                        protocolKey.SetValue("", description);
                        using (var iconKey = protocolKey.CreateSubKey("DefaultIcon"))
                        {
                            iconKey?.SetValue("", $"\"{_exePath}\",0");
                        }
                        using (var commandKey = protocolKey.CreateSubKey(@"shell\open\command"))
                        {
                            commandKey?.SetValue("", $"\"{_exePath}\" \"%1\"");
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void UnregisterAssociation(string extension, string protocolName)
        {
            try
            {
                using (var classesRoot = Registry.CurrentUser.OpenSubKey("Software\\Classes", true))
                {
                    classesRoot?.DeleteSubKeyTree(extension, false);
                    classesRoot?.DeleteSubKeyTree(protocolName, false);
                }
            }
            catch
            {
            }
        }

        public bool IsAssociated()
        {
            try
            {
                bool presetAssociated = false;
                using (var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{AppConfig.PresetFileExtension}"))
                {
                    presetAssociated = key?.GetValue("")?.ToString() == AppConfig.PresetProtocolName;
                }

                bool effectAssociated = false;
                using (var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{AppConfig.EffectFileExtension}"))
                {
                    effectAssociated = key?.GetValue("")?.ToString() == AppConfig.EffectProtocolName;
                }

                return presetAssociated && effectAssociated;
            }
            catch
            {
                return false;
            }
        }

        public void Associate()
        {
            RegisterAssociation(AppConfig.PresetFileExtension, AppConfig.PresetProtocolName, AppConfig.PresetAppDescription);
            RegisterAssociation(AppConfig.EffectFileExtension, AppConfig.EffectProtocolName, AppConfig.EffectAppDescription);
        }

        public void Disassociate()
        {
            UnregisterAssociation(AppConfig.PresetFileExtension, AppConfig.PresetProtocolName);
            UnregisterAssociation(AppConfig.EffectFileExtension, AppConfig.EffectProtocolName);
        }
    }
}