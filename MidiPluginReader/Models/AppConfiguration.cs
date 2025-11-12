using System.Collections.Generic;

namespace MidiPlugin.Models
{
    public class AppConfiguration
    {
        public List<string> PrioritizedExePaths { get; set; }

        public AppConfiguration()
        {
            PrioritizedExePaths = new List<string>();
        }
    }
}