using System;

namespace MIDI.Utils.Telemetry.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class TelemetryEventAttribute : Attribute
    {
        public string Category { get; }

        public TelemetryEventAttribute(string category)
        {
            Category = category;
        }
    }
}