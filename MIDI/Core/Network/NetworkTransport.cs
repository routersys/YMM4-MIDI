using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MIDI.Configuration.Models;

namespace MIDI.Core.Network
{
    public class NetworkTransport : IDisposable
    {
        private Socket? _udpSocket;
        private readonly DistributedProcessingSettings _settings;
        private bool _disposed;
        private readonly byte[] _receiveBuffer;
        private EndPoint _remoteEndPoint;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private class SenderState
        {
            public uint NextExpectedSequence;
            public bool IsInitialized;
            public readonly SortedDictionary<uint, BufferedPacket> Buffer = new SortedDictionary<uint, BufferedPacket>();
            public DateTime LastPacketTime;
        }

        private struct BufferedPacket
        {
            public byte[] Data;
            public DateTime ReceivedTime;
        }

        private readonly ConcurrentDictionary<string, SenderState> _senderStates = new ConcurrentDictionary<string, SenderState>();
        private const int PacketTimeoutMs = 100;

        public event Action<IPEndPoint, ReadOnlyMemory<byte>>? DataReceived;

        public NetworkTransport(DistributedProcessingSettings settings)
        {
            _settings = settings;
            _receiveBuffer = GC.AllocateUninitializedArray<byte>(settings.SocketReceiveBufferSize, true);
            _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        }

        public void Start(int port)
        {
            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, _settings.SocketReceiveBufferSize);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, _settings.SocketSendBufferSize);

            try
            {
                _udpSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            }
            catch (SocketException)
            {
                _udpSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
            }

            if (_settings.AutoDiscovery)
            {
                try
                {
                    _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse(_settings.MulticastAddress)));
                    _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 10);
                }
                catch { }
            }

            Task.Run(ReceiveLoop, _cts.Token);
            Task.Run(MaintenanceLoop, _cts.Token);
        }

        private void ReceiveLoop()
        {
            while (!_disposed && _udpSocket != null && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    int received = _udpSocket.ReceiveFrom(_receiveBuffer, ref _remoteEndPoint);
                    if (received >= PacketConstants.HeaderSize)
                    {
                        ProcessReceivedData((IPEndPoint)_remoteEndPoint, new ReadOnlyMemory<byte>(_receiveBuffer, 0, received));
                    }
                }
                catch
                {
                    if (_disposed) break;
                }
            }
        }

        private void ProcessReceivedData(IPEndPoint sender, ReadOnlyMemory<byte> data)
        {
            if (data.Length < PacketConstants.HeaderSize) return;

            var header = MemoryMarshal.Read<PacketHeader>(data.Span);

            if (header.Magic != PacketConstants.Magic)
            {
                return;
            }

            string senderKey = sender.ToString();
            SenderState state = _senderStates.GetOrAdd(senderKey, _ => new SenderState());

            lock (state)
            {
                state.LastPacketTime = DateTime.UtcNow;

                if (!state.IsInitialized)
                {
                    state.NextExpectedSequence = header.Sequence;
                    state.IsInitialized = true;
                }

                if (header.Sequence < state.NextExpectedSequence)
                {
                    return;
                }

                if (header.Sequence == state.NextExpectedSequence)
                {
                    DispatchPacket(sender, data);
                    state.NextExpectedSequence++;

                    while (state.Buffer.ContainsKey(state.NextExpectedSequence))
                    {
                        var buffered = state.Buffer[state.NextExpectedSequence];
                        DispatchPacket(sender, new ReadOnlyMemory<byte>(buffered.Data));
                        state.Buffer.Remove(state.NextExpectedSequence);
                        state.NextExpectedSequence++;
                    }
                }
                else
                {
                    if (!state.Buffer.ContainsKey(header.Sequence))
                    {
                        state.Buffer.Add(header.Sequence, new BufferedPacket
                        {
                            Data = data.ToArray(),
                            ReceivedTime = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        private void DispatchPacket(IPEndPoint sender, ReadOnlyMemory<byte> data)
        {
            DataReceived?.Invoke(sender, data);
        }

        private async Task MaintenanceLoop()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(10, _cts.Token);

                foreach (var kvp in _senderStates)
                {
                    var state = kvp.Value;
                    var senderEp = ParseEndPoint(kvp.Key);
                    if (senderEp == null) continue;

                    lock (state)
                    {
                        if (state.Buffer.Count > 0)
                        {
                            var firstSeq = state.Buffer.Keys.First();
                            var packet = state.Buffer[firstSeq];

                            if ((DateTime.UtcNow - packet.ReceivedTime).TotalMilliseconds > PacketTimeoutMs)
                            {
                                state.NextExpectedSequence = firstSeq;

                                while (state.Buffer.ContainsKey(state.NextExpectedSequence))
                                {
                                    var buffered = state.Buffer[state.NextExpectedSequence];
                                    DispatchPacket(senderEp, new ReadOnlyMemory<byte>(buffered.Data));
                                    state.Buffer.Remove(state.NextExpectedSequence);
                                    state.NextExpectedSequence++;
                                }
                            }
                        }

                        if ((DateTime.UtcNow - state.LastPacketTime).TotalSeconds > 30)
                        {
                            _senderStates.TryRemove(kvp.Key, out _);
                        }
                    }
                }
            }
        }

        private IPEndPoint? ParseEndPoint(string input)
        {
            try
            {
                var lastColon = input.LastIndexOf(':');
                if (lastColon > 0)
                {
                    var ip = IPAddress.Parse(input.Substring(0, lastColon));
                    var port = int.Parse(input.Substring(lastColon + 1));
                    return new IPEndPoint(ip, port);
                }
            }
            catch { }
            return null;
        }

        public unsafe void Send(IPEndPoint target, ReadOnlySpan<byte> data, bool isRedundant = false)
        {
            if (_udpSocket == null || _disposed) return;

            fixed (byte* ptr = data)
            {
                var span = new Span<byte>(ptr, data.Length);
                try
                {
                    _udpSocket.SendTo(span, target);

                    if (isRedundant && _settings.RedundancyLevel > 1)
                    {
                        for (int i = 1; i < _settings.RedundancyLevel; i++)
                        {
                            _udpSocket.SendTo(span, target);
                        }
                    }
                }
                catch { }
            }
        }

        public void SendMulticast(ReadOnlySpan<byte> data, int port)
        {
            try
            {
                Send(new IPEndPoint(IPAddress.Parse(_settings.MulticastAddress), port), data);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _udpSocket?.Close();
            _udpSocket?.Dispose();
            _udpSocket = null;
            _cts.Dispose();
        }
    }
}