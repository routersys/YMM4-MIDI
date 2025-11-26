using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public enum DistributedRole
    {
        Master,
        Worker
    }

    public enum NetworkCompression
    {
        None,
        Lz4,
        Zstd
    }

    public class DistributedProcessingSettings : INotifyPropertyChanged
    {
        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set => SetField(ref _isEnabled, value); }

        private DistributedRole _role = DistributedRole.Worker;
        public DistributedRole Role { get => _role; set => SetField(ref _role, value); }

        private int _discoveryPort = 50000;
        public int DiscoveryPort { get => _discoveryPort; set => SetField(ref _discoveryPort, value); }

        private int _dataPort = 50001;
        public int DataPort { get => _dataPort; set => SetField(ref _dataPort, value); }

        private string _multicastAddress = "239.0.0.1";
        public string MulticastAddress { get => _multicastAddress; set => SetField(ref _multicastAddress, value); }

        private string _networkInterfaceIp = "0.0.0.0";
        public string NetworkInterfaceIp { get => _networkInterfaceIp; set => SetField(ref _networkInterfaceIp, value); }

        private string _workerName = System.Environment.MachineName;
        public string WorkerName { get => _workerName; set => SetField(ref _workerName, value); }

        private int _chunkSizeFrames = 1024;
        public int ChunkSizeFrames { get => _chunkSizeFrames; set => SetField(ref _chunkSizeFrames, value); }

        private int _redundancyLevel = 2;
        public int RedundancyLevel { get => _redundancyLevel; set => SetField(ref _redundancyLevel, value); }

        private bool _enableZeroCopy = true;
        public bool EnableZeroCopy { get => _enableZeroCopy; set => SetField(ref _enableZeroCopy, value); }

        private bool _stopGcDuringRender = true;
        public bool StopGcDuringRender { get => _stopGcDuringRender; set => SetField(ref _stopGcDuringRender, value); }

        private NetworkCompression _compression = NetworkCompression.None;
        public NetworkCompression Compression { get => _compression; set => SetField(ref _compression, value); }

        private int _connectionTimeoutMs = 5000;
        public int ConnectionTimeoutMs { get => _connectionTimeoutMs; set => SetField(ref _connectionTimeoutMs, value); }

        private int _syncIntervalMs = 1000;
        public int SyncIntervalMs { get => _syncIntervalMs; set => SetField(ref _syncIntervalMs, value); }

        private int _maxLatencyMs = 100;
        public int MaxLatencyMs { get => _maxLatencyMs; set => SetField(ref _maxLatencyMs, value); }

        private int _cpuUsageLimitPercent = 90;
        public int CpuUsageLimitPercent { get => _cpuUsageLimitPercent; set => SetField(ref _cpuUsageLimitPercent, value); }

        private int _memoryUsageLimitMb = 4096;
        public int MemoryUsageLimitMb { get => _memoryUsageLimitMb; set => SetField(ref _memoryUsageLimitMb, value); }

        private bool _enableEncryption;
        public bool EnableEncryption { get => _enableEncryption; set => SetField(ref _enableEncryption, value); }

        private string _encryptionKey = "YukkuriMidiSecret";
        public string EncryptionKey { get => _encryptionKey; set => SetField(ref _encryptionKey, value); }

        private int _socketSendBufferSize = 1024 * 1024;
        public int SocketSendBufferSize { get => _socketSendBufferSize; set => SetField(ref _socketSendBufferSize, value); }

        private int _socketReceiveBufferSize = 1024 * 1024;
        public int SocketReceiveBufferSize { get => _socketReceiveBufferSize; set => SetField(ref _socketReceiveBufferSize, value); }

        private bool _autoDiscovery = true;
        public bool AutoDiscovery { get => _autoDiscovery; set => SetField(ref _autoDiscovery, value); }

        public void CopyFrom(DistributedProcessingSettings source)
        {
            IsEnabled = source.IsEnabled;
            Role = source.Role;
            DiscoveryPort = source.DiscoveryPort;
            DataPort = source.DataPort;
            MulticastAddress = source.MulticastAddress;
            NetworkInterfaceIp = source.NetworkInterfaceIp;
            WorkerName = source.WorkerName;
            ChunkSizeFrames = source.ChunkSizeFrames;
            RedundancyLevel = source.RedundancyLevel;
            EnableZeroCopy = source.EnableZeroCopy;
            StopGcDuringRender = source.StopGcDuringRender;
            Compression = source.Compression;
            ConnectionTimeoutMs = source.ConnectionTimeoutMs;
            SyncIntervalMs = source.SyncIntervalMs;
            MaxLatencyMs = source.MaxLatencyMs;
            CpuUsageLimitPercent = source.CpuUsageLimitPercent;
            MemoryUsageLimitMb = source.MemoryUsageLimitMb;
            EnableEncryption = source.EnableEncryption;
            EncryptionKey = source.EncryptionKey;
            SocketSendBufferSize = source.SocketSendBufferSize;
            SocketReceiveBufferSize = source.SocketReceiveBufferSize;
            AutoDiscovery = source.AutoDiscovery;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}