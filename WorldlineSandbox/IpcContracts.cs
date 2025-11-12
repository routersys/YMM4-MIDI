using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace WorldlineHost
{
    [JsonSerializable(typeof(SynthRequestData))]
    [JsonSerializable(typeof(SynthResponse))]
    internal partial class IpcJsonContext : JsonSerializerContext { }

    public class SynthRequestData
    {
        public List<ItemData> Items { get; set; } = new();
        public double[] F0 { get; set; } = System.Array.Empty<double>();
        public double[] Gender { get; set; } = System.Array.Empty<double>();
        public double[] Tension { get; set; } = System.Array.Empty<double>();
        public double[] Breathiness { get; set; } = System.Array.Empty<double>();
        public double[] Voicing { get; set; } = System.Array.Empty<double>();
    }

    public class ItemData
    {
        public double[] Sample { get; set; } = System.Array.Empty<double>();
        public byte[]? FrqData { get; set; }
        public double[] Pitches { get; set; } = System.Array.Empty<double>();

        public int SampleFs { get; set; }
        public int Tone { get; set; }
        public double ConVel { get; set; }
        public double Offset { get; set; }
        public double RequiredLength { get; set; }
        public double Consonant { get; set; }
        public double CutOff { get; set; }
        public double Volume { get; set; }
        public double Modulation { get; set; }
        public double Tempo { get; set; }
        public int FlagG { get; set; }
        public int FlagO { get; set; }
        public int FlagP { get; set; }
        public int FlagMt { get; set; }
        public int FlagMb { get; set; }
        public int FlagMv { get; set; }

        public double PosMs { get; set; }
        public double SkipMs { get; set; }
        public double LengthMs { get; set; }
        public double FadeInMs { get; set; }
        public double FadeOutMs { get; set; }
    }

    public class SynthResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public float[]? AudioData { get; set; }
    }
}