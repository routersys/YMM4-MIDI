using System.Collections.Generic;

namespace MidiPlugin.Models
{
    public class PresetCertificate
    {
        public string Issuer { get; set; } = "Unknown";
        public string ComputerName { get; set; } = "Unknown";
        public string UserName { get; set; } = "Unknown";
        public string CreatedAt { get; set; } = "Unknown";
    }

    public class PresetDetails
    {
        public PresetCertificate Certificate { get; set; }
        public List<string> ChangedItems { get; set; } = new List<string>();
        public Dictionary<string, List<string>> ChangedCategories { get; set; } = new Dictionary<string, List<string>>();
    }
}