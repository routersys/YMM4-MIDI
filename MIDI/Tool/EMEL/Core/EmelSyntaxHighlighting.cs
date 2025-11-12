using System;
using System.IO;
using System.Reflection;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace MIDI.Tool.EMEL.Core
{
    public static class EmelSyntaxHighlighting
    {
        static EmelSyntaxHighlighting()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "MIDI.Tool.EMEL.Resources.EmelSyntax.xshd";

                using (Stream? s = assembly.GetManifestResourceStream(resourceName))
                {
                    if (s == null)
                    {
                        return;
                    }
                    using (XmlReader reader = new XmlTextReader(s))
                    {
                        var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                        HighlightingManager.Instance.RegisterHighlighting("EMEL", new[] { ".emel" }, definition);
                    }
                }
            }
            catch
            {
            }
        }
    }
}