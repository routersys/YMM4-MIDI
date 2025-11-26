using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using MIDI.Configuration.Models;
using MIDI.Renderers;

namespace MIDI.Core.Network
{
    [MessagePackObject]
    public class DistributedSessionData
    {
        [Key(0)]
        public string ConfigurationJson { get; set; } = string.Empty;
        [Key(1)]
        public byte[] MidiFileContent { get; set; } = Array.Empty<byte>();
        [Key(2)]
        public Dictionary<string, string> SoundFontHashes { get; set; } = new Dictionary<string, string>();
    }

    public class DistributedAudioService : IDisposable
    {
        private static DistributedAudioService? _instance;
        public static DistributedAudioService Instance => _instance ?? throw new InvalidOperationException("Not initialized");

        private readonly MidiConfiguration _config;
        private NetworkTransport? _transport;
        private ClusterManager? _clusterManager;
        public List<WorkerNodeInfo> ActiveWorkers { get; private set; } = new();
        public event Action? WorkersChanged;

        private readonly ConcurrentDictionary<uint, ConcurrentDictionary<long, float[]>> _taskResults = new();
        private uint _currentSequence = 0;

        private IAudioRenderer? _workerRenderer;
        private MidiConfiguration? _workerConfig;
        private string? _workerTempMidiPath;
        private readonly Dictionary<Guid, Dictionary<int, byte[]>> _incomingSessionChunks = new();
        private readonly Dictionary<Guid, SessionInitPacket> _incomingSessions = new();

        private readonly ConcurrentDictionary<string, string> _localSoundFontMap = new();
        private bool _isInitializedWorker = false;

        public DistributedAudioService(MidiConfiguration config)
        {
            _instance = this;
            _config = config;
            _config.Performance.Distributed.PropertyChanged += OnSettingsChanged;
            if (_config.Performance.Distributed.IsEnabled)
            {
                Initialize();
            }
        }

        private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DistributedProcessingSettings.IsEnabled))
            {
                if (_config.Performance.Distributed.IsEnabled) Initialize();
                else Shutdown();
            }
        }

        private void Initialize()
        {
            Shutdown();
            var settings = _config.Performance.Distributed;
            _transport = new NetworkTransport(settings);
            _transport.DataReceived += HandleDataReceived;
            _transport.Start(settings.DiscoveryPort);
            _clusterManager = new ClusterManager(settings, _transport);
            _clusterManager.WorkerListUpdated += OnWorkerListUpdated;

            if (settings.Role == DistributedRole.Worker)
            {
                BuildSoundFontCache();
            }
        }

        private void BuildSoundFontCache()
        {
            Task.Run(() =>
            {
                var searchPaths = new List<string>
                {
                    _config.SoundFont.DefaultSoundFontDirectory
                };

                var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    searchPaths.Add(Path.Combine(assemblyPath, _config.SoundFont.DefaultSoundFontDirectory));
                }

                foreach (var dir in searchPaths)
                {
                    if (Directory.Exists(dir))
                    {
                        foreach (var file in Directory.GetFiles(dir, "*.sf2", SearchOption.AllDirectories))
                        {
                            try
                            {
                                using var md5 = MD5.Create();
                                using var stream = File.OpenRead(file);
                                var hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                                _localSoundFontMap[hash] = file;
                            }
                            catch { }
                        }
                    }
                }
            });
        }

        private void OnWorkerListUpdated(List<WorkerNodeInfo> workers)
        {
            ActiveWorkers = workers;
            WorkersChanged?.Invoke();
        }

        public void InitializeWorkers(string midiFilePath)
        {
            if (_config.Performance.Distributed.Role != DistributedRole.Master) return;

            Task.Run(async () =>
            {
                var sfHashes = new Dictionary<string, string>();
                if (_config.SoundFont.EnableSoundFont)
                {
                    var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                    var sfDir = Path.Combine(assemblyPath, _config.SoundFont.DefaultSoundFontDirectory);

                    foreach (var layer in _config.SoundFont.Layers)
                    {
                        var path = Path.Combine(sfDir, layer.SoundFontFile);
                        if (File.Exists(path))
                        {
                            try
                            {
                                using var md5 = MD5.Create();
                                using var stream = File.OpenRead(path);
                                var hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                                sfHashes[layer.SoundFontFile] = hash;
                            }
                            catch { }
                        }
                    }
                }

                var data = new DistributedSessionData
                {
                    ConfigurationJson = JsonSerializer.Serialize(_config),
                    MidiFileContent = await File.ReadAllBytesAsync(midiFilePath),
                    SoundFontHashes = sfHashes
                };

                var sessionDataBytes = MessagePackSerializer.Serialize(data);
                var sessionId = Guid.NewGuid();

                BroadcastSessionData(sessionId, sessionDataBytes);
            });
        }

        private unsafe void BroadcastSessionData(Guid sessionId, byte[] data)
        {
            if (_transport == null) return;

            var initPacket = new SessionInitPacket
            {
                SessionId = sessionId,
                TotalDataSize = data.Length,
                TotalChunks = (int)Math.Ceiling((double)data.Length / 40000)
            };

            SendControlPacket(PacketType.SessionInit, initPacket);

            int chunkSize = 40000;
            int offset = 0;
            int chunkIndex = 0;

            while (offset < data.Length)
            {
                int size = Math.Min(chunkSize, data.Length - offset);
                var chunkSpan = new ReadOnlySpan<byte>(data, offset, size);

                var chunkHeader = new SessionChunkPacket
                {
                    SessionId = sessionId,
                    ChunkIndex = chunkIndex,
                    DataSize = size
                };

                var header = new PacketHeader
                {
                    Magic = PacketConstants.Magic,
                    Type = PacketType.SessionDataChunk,
                    Timestamp = DateTime.UtcNow.Ticks,
                    PayloadLength = sizeof(SessionChunkPacket) + size,
                    Sequence = _currentSequence++
                };

                int packetSize = PacketConstants.HeaderSize + sizeof(SessionChunkPacket) + size;
                var packetBuffer = new byte[packetSize];
                var span = packetBuffer.AsSpan();

                MemoryMarshal.Write(span, in header);
                MemoryMarshal.Write(span.Slice(PacketConstants.HeaderSize), in chunkHeader);
                chunkSpan.CopyTo(span.Slice(PacketConstants.HeaderSize + sizeof(SessionChunkPacket)));

                _transport.SendMulticast(span, _config.Performance.Distributed.DiscoveryPort);

                offset += size;
                chunkIndex++;
                Thread.Sleep(5);
            }
        }

        private unsafe void SendControlPacket<T>(PacketType type, T payload) where T : unmanaged
        {
            if (_transport == null) return;

            var header = new PacketHeader
            {
                Magic = PacketConstants.Magic,
                Type = type,
                Timestamp = DateTime.UtcNow.Ticks,
                PayloadLength = sizeof(T),
                Sequence = _currentSequence++
            };

            int size = PacketConstants.HeaderSize + sizeof(T);
            var buffer = new byte[size];
            var span = buffer.AsSpan();

            MemoryMarshal.Write(span, in header);
            MemoryMarshal.Write(span.Slice(PacketConstants.HeaderSize), in payload);

            _transport.SendMulticast(span, _config.Performance.Distributed.DiscoveryPort);
        }

        private void HandleDataReceived(IPEndPoint sender, ReadOnlyMemory<byte> data)
        {
            if (data.Length < PacketConstants.HeaderSize) return;

            var header = MemoryMarshal.Read<PacketHeader>(data.Span);

            switch (header.Type)
            {
                case PacketType.TaskRequest:
                    if (_config.Performance.Distributed.Role == DistributedRole.Worker)
                    {
                        HandleTaskRequest(sender, data.Span.Slice(PacketConstants.HeaderSize));
                    }
                    break;
                case PacketType.AudioChunk:
                    if (_config.Performance.Distributed.Role == DistributedRole.Master)
                    {
                        HandleAudioChunk(data.Span.Slice(PacketConstants.HeaderSize));
                    }
                    break;
                case PacketType.SessionInit:
                    if (_config.Performance.Distributed.Role == DistributedRole.Worker)
                    {
                        HandleSessionInit(data.Span.Slice(PacketConstants.HeaderSize));
                    }
                    break;
                case PacketType.SessionDataChunk:
                    if (_config.Performance.Distributed.Role == DistributedRole.Worker)
                    {
                        HandleSessionChunk(data.Span.Slice(PacketConstants.HeaderSize));
                    }
                    break;
            }
        }

        private unsafe void HandleSessionInit(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < sizeof(SessionInitPacket)) return;
            var packet = MemoryMarshal.Read<SessionInitPacket>(payload);

            if (_incomingSessions.ContainsKey(packet.SessionId)) return;

            _incomingSessions[packet.SessionId] = packet;
            _incomingSessionChunks[packet.SessionId] = new Dictionary<int, byte[]>();
        }

        private unsafe void HandleSessionChunk(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < sizeof(SessionChunkPacket)) return;
            var header = MemoryMarshal.Read<SessionChunkPacket>(payload);
            var data = payload.Slice(sizeof(SessionChunkPacket)).ToArray();

            if (_incomingSessionChunks.TryGetValue(header.SessionId, out var chunks))
            {
                chunks[header.ChunkIndex] = data;

                if (_incomingSessions.TryGetValue(header.SessionId, out var initPacket))
                {
                    if (chunks.Count == initPacket.TotalChunks)
                    {
                        ReconstructSession(header.SessionId, initPacket.TotalDataSize, chunks);
                    }
                }
            }
        }

        private void ReconstructSession(Guid sessionId, int totalSize, Dictionary<int, byte[]> chunks)
        {
            var fullData = new byte[totalSize];
            int offset = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks.TryGetValue(i, out var chunk))
                {
                    Array.Copy(chunk, 0, fullData, offset, chunk.Length);
                    offset += chunk.Length;
                }
                else
                {
                    return;
                }
            }

            try
            {
                var sessionData = MessagePackSerializer.Deserialize<DistributedSessionData>(fullData);
                SetupWorkerRenderer(sessionData);
                _incomingSessions.Remove(sessionId);
                _incomingSessionChunks.Remove(sessionId);
                _isInitializedWorker = true;
            }
            catch { }
        }

        private void SetupWorkerRenderer(DistributedSessionData data)
        {
            try
            {
                _workerConfig = JsonSerializer.Deserialize<MidiConfiguration>(data.ConfigurationJson);
                if (_workerConfig == null) return;

                if (_workerConfig.SoundFont.EnableSoundFont)
                {
                    foreach (var layer in _workerConfig.SoundFont.Layers)
                    {
                        if (data.SoundFontHashes.TryGetValue(layer.SoundFontFile, out var hash))
                        {
                            if (_localSoundFontMap.TryGetValue(hash, out var localPath))
                            {
                                layer.SoundFontFile = localPath;
                            }
                        }
                    }
                }

                _workerTempMidiPath = Path.GetTempFileName();
                File.WriteAllBytes(_workerTempMidiPath, data.MidiFileContent);

                if (_workerRenderer != null) _workerRenderer.Dispose();

                if (_workerConfig.SFZ.EnableSfz)
                {
                    _workerRenderer = new SfzRenderer(_workerTempMidiPath, _workerConfig, _config.Audio.SampleRate);
                }
                else if (_workerConfig.SoundFont.EnableSoundFont)
                {
                    var sfPaths = _workerConfig.SoundFont.Layers.Select(l => l.SoundFontFile).ToList();
                    _workerRenderer = new SoundFontRenderer(_workerTempMidiPath, _workerConfig, _config.Audio.SampleRate, sfPaths);
                }
                else
                {
                    _workerRenderer = new SynthesisRenderer(_workerTempMidiPath, _workerConfig, _config.Audio.SampleRate);
                }
            }
            catch { }
        }

        private unsafe void HandleTaskRequest(IPEndPoint sender, ReadOnlySpan<byte> payload)
        {
            if (payload.Length < sizeof(TaskRequestPacket)) return;
            var request = MemoryMarshal.Read<TaskRequestPacket>(payload);

            if (_workerRenderer != null && _isInitializedWorker)
            {
                _workerRenderer.Seek(request.StartSample);
                var buffer = new float[request.SampleCount * 2];
                int read = _workerRenderer.Read(buffer, request.StartSample * 2);
                if (read < buffer.Length)
                {
                    Array.Clear(buffer, read, buffer.Length - read);
                }
                SendAudioChunk(sender, request.StartSample, buffer, request.TaskId);
            }
            else
            {
                var empty = new float[request.SampleCount * 2];
                SendAudioChunk(sender, request.StartSample, empty, request.TaskId);
            }
        }

        public unsafe void SendAudioChunk(IPEndPoint target, long startSample, float[] buffer, uint taskId)
        {
            if (_transport == null) return;

            var chunkHeader = new AudioChunkHeader
            {
                StartSample = startSample,
                SampleCount = buffer.Length / 2,
                Channels = 2,
                SampleRate = _config.Audio.SampleRate,
                TaskId = taskId
            };

            var header = new PacketHeader
            {
                Magic = PacketConstants.Magic,
                Type = PacketType.AudioChunk,
                Timestamp = DateTime.UtcNow.Ticks,
                PayloadLength = sizeof(AudioChunkHeader) + buffer.Length * sizeof(float),
                Sequence = _currentSequence++
            };

            int totalSize = PacketConstants.HeaderSize + sizeof(AudioChunkHeader) + buffer.Length * sizeof(float);
            byte[] packetBytes = new byte[totalSize];
            Span<byte> packetSpan = packetBytes;

            MemoryMarshal.Write(packetSpan, in header);
            MemoryMarshal.Write(packetSpan.Slice(PacketConstants.HeaderSize), in chunkHeader);

            fixed (float* ptr = buffer)
            {
                Marshal.Copy((IntPtr)ptr, packetBytes, PacketConstants.HeaderSize + sizeof(AudioChunkHeader), buffer.Length * sizeof(float));
            }

            _transport.Send(target, packetSpan, true);
        }

        private unsafe void HandleAudioChunk(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < sizeof(AudioChunkHeader)) return;

            var chunkHeader = MemoryMarshal.Read<AudioChunkHeader>(payload);
            var audioDataSize = chunkHeader.SampleCount * chunkHeader.Channels * sizeof(float);

            if (payload.Length < sizeof(AudioChunkHeader) + audioDataSize) return;

            var floatData = MemoryMarshal.Cast<byte, float>(payload.Slice(sizeof(AudioChunkHeader), audioDataSize));
            var buffer = floatData.ToArray();

            var chunks = _taskResults.GetOrAdd(chunkHeader.TaskId, _ => new ConcurrentDictionary<long, float[]>());
            chunks[chunkHeader.StartSample] = buffer;
        }

        public unsafe void DistributeTask(WorkerNodeInfo worker, long startSample, int sampleCount, uint taskId)
        {
            if (_transport == null) return;

            var request = new TaskRequestPacket
            {
                StartSample = startSample,
                SampleCount = sampleCount,
                TaskId = taskId
            };

            var header = new PacketHeader
            {
                Magic = PacketConstants.Magic,
                Type = PacketType.TaskRequest,
                Timestamp = DateTime.UtcNow.Ticks,
                PayloadLength = sizeof(TaskRequestPacket),
                Sequence = _currentSequence++
            };

            int totalSize = PacketConstants.HeaderSize + sizeof(TaskRequestPacket);
            byte[] packetBytes = new byte[totalSize];
            Span<byte> packetSpan = packetBytes;

            MemoryMarshal.Write(packetSpan, in header);
            MemoryMarshal.Write(packetSpan.Slice(PacketConstants.HeaderSize), in request);

            _transport.Send(worker.EndPoint, packetSpan, true);
        }

        public float[]? GetReceivedChunk(uint taskId, long startSample)
        {
            if (_taskResults.TryGetValue(taskId, out var chunks))
            {
                if (chunks.TryRemove(startSample, out var buffer))
                {
                    return buffer;
                }
            }
            return null;
        }

        public void BeginProcessing()
        {
            if (_config.Performance.Distributed.IsEnabled && _config.Performance.Distributed.StopGcDuringRender)
            {
                try
                {
                    GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                }
                catch { }
            }
        }

        public void EndProcessing()
        {
            GCSettings.LatencyMode = GCLatencyMode.Interactive;
        }

        private void Shutdown()
        {
            if (_clusterManager != null)
            {
                _clusterManager.WorkerListUpdated -= OnWorkerListUpdated;
                _clusterManager.Dispose();
                _clusterManager = null;
            }
            if (_transport != null)
            {
                _transport.DataReceived -= HandleDataReceived;
                _transport.Dispose();
                _transport = null;
            }

            if (_workerRenderer != null)
            {
                _workerRenderer.Dispose();
                _workerRenderer = null;
            }

            if (_workerTempMidiPath != null && File.Exists(_workerTempMidiPath))
            {
                try { File.Delete(_workerTempMidiPath); } catch { }
                _workerTempMidiPath = null;
            }
            _isInitializedWorker = false;
        }

        public void Dispose()
        {
            Shutdown();
            _config.Performance.Distributed.PropertyChanged -= OnSettingsChanged;
        }
    }
}