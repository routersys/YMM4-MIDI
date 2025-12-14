using System;

namespace MIDI.Utils.Telemetry.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public class TelemetryCategoryAttribute : Attribute
    {
        public string Category { get; }

        public TelemetryCategoryAttribute(string category)
        {
            Category = category;
        }
    }
}