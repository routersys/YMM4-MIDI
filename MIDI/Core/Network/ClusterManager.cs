using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MIDI.Configuration.Models;

namespace MIDI.Core.Network
{
    public class WorkerNodeInfo
    {
        public IPEndPoint EndPoint { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; }
        public int CpuUsage { get; set; }
        public long AvailableMemory { get; set; }
        public bool IsActive => (DateTime.UtcNow - LastSeen).TotalSeconds < 5;
    }

    public class ClusterManager : IDisposable
    {
        private readonly DistributedProcessingSettings _settings;
        private readonly NetworkTransport _transport;
        private readonly Dictionary<string, WorkerNodeInfo> _workers = new();
        private readonly CancellationTokenSource _cts = new();

        public event Action<List<WorkerNodeInfo>>? WorkerListUpdated;

        public ClusterManager(DistributedProcessingSettings settings, NetworkTransport transport)
        {
            _settings = settings;
            _transport = transport;
            _transport.DataReceived += OnDataReceived;
            Task.Run(DiscoveryLoop);
        }

        private async Task DiscoveryLoop()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                if (_settings.IsEnabled)
                {
                    BroadcastPresence();
                    CleanupStaleWorkers();
                }
                await Task.Delay(1000, _cts.Token);
            }
        }

        private unsafe void BroadcastPresence()
        {
            var packet = new DiscoveryPacket
            {
                CpuUsage = (int)(System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds),
                AvailableMemory = System.GC.GetTotalMemory(false),
                Role = (int)_settings.Role
            };

            var nameBytes = Encoding.UTF8.GetBytes(_settings.WorkerName);

            byte* dest = packet.MachineName;
            int len = Math.Min(nameBytes.Length, 63);
            Marshal.Copy(nameBytes, 0, (IntPtr)dest, len);
            dest[len] = 0;

            var header = new PacketHeader
            {
                Magic = PacketConstants.Magic,
                Type = PacketType.Discovery,
                Timestamp = DateTime.UtcNow.Ticks,
                PayloadLength = sizeof(DiscoveryPacket),
                Sequence = 0
            };

            int totalSize = sizeof(PacketHeader) + sizeof(DiscoveryPacket);
            byte[] bufferArray = new byte[totalSize];
            Span<byte> buffer = bufferArray;

            MemoryMarshal.Write(buffer, in header);
            MemoryMarshal.Write(buffer.Slice(sizeof(PacketHeader)), in packet);

            _transport.SendMulticast(buffer, _settings.DiscoveryPort);
        }

        private unsafe void OnDataReceived(IPEndPoint sender, ReadOnlyMemory<byte> data)
        {
            if (data.Length < sizeof(PacketHeader)) return;

            var header = MemoryMarshal.Read<PacketHeader>(data.Span);
            if (header.Magic != PacketConstants.Magic) return;

            if (header.Type == PacketType.Discovery)
            {
                if (data.Length >= sizeof(PacketHeader) + sizeof(DiscoveryPacket))
                {
                    var payload = MemoryMarshal.Read<DiscoveryPacket>(data.Span.Slice(sizeof(PacketHeader)));
                    UpdateWorker(sender, payload);
                }
            }
        }

        private unsafe void UpdateWorker(IPEndPoint sender, DiscoveryPacket packet)
        {
            string machineName;
            byte* pName = packet.MachineName;
            machineName = Marshal.PtrToStringUTF8((IntPtr)pName) ?? "Unknown";

            lock (_workers)
            {
                var key = sender.ToString();
                if (!_workers.TryGetValue(key, out var worker))
                {
                    worker = new WorkerNodeInfo { EndPoint = sender };
                    _workers[key] = worker;
                }
                worker.Name = machineName;
                worker.LastSeen = DateTime.UtcNow;
                worker.CpuUsage = packet.CpuUsage;
                worker.AvailableMemory = packet.AvailableMemory;
            }
            NotifyUpdate();
        }

        private void CleanupStaleWorkers()
        {
            lock (_workers)
            {
                var staleKeys = _workers.Where(kv => !kv.Value.IsActive).Select(kv => kv.Key).ToList();
                foreach (var key in staleKeys)
                {
                    _workers.Remove(key);
                }
                if (staleKeys.Any()) NotifyUpdate();
            }
        }

        private void NotifyUpdate()
        {
            lock (_workers)
            {
                WorkerListUpdated?.Invoke(_workers.Values.ToList());
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _transport.DataReceived -= OnDataReceived;
        }
    }
}