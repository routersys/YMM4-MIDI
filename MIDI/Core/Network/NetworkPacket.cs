using System;
using System.Runtime.InteropServices;

namespace MIDI.Core.Network
{
    public enum PacketType : byte
    {
        Discovery = 0x01,
        DiscoveryAck = 0x02,
        TimeSync = 0x03,
        AudioChunk = 0x04,
        Heartbeat = 0x05,
        TaskRequest = 0x06,
        SessionInit = 0x07,
        SessionDataChunk = 0x08,
        ResourceCheck = 0x09,
        ResourceAck = 0x0A,
        WorkerReady = 0x0B,
        Error = 0xFF
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketHeader
    {
        public uint Magic;
        public PacketType Type;
        public long Timestamp;
        public int PayloadLength;
        public uint Sequence;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct DiscoveryPacket
    {
        public fixed byte MachineName[64];
        public int CpuUsage;
        public long AvailableMemory;
        public int Role;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AudioChunkHeader
    {
        public long StartSample;
        public int SampleCount;
        public int Channels;
        public int SampleRate;
        public uint TaskId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TaskRequestPacket
    {
        public long StartSample;
        public int SampleCount;
        public uint TaskId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SessionInitPacket
    {
        public Guid SessionId;
        public int TotalDataSize;
        public int TotalChunks;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SessionChunkPacket
    {
        public Guid SessionId;
        public int ChunkIndex;
        public int DataSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct ResourceCheckPacket
    {
        public fixed byte Hash[64];
        public int Type;
    }

    public static class PacketConstants
    {
        public const uint Magic = 0x4D494449;
        public const int HeaderSize = 21;
        public const int MaxPayloadSize = 60000;
    }
}