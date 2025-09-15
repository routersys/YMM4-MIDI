using YukkuriMovieMaker.Plugin.FileSource;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace MIDI
{
    public class MidiAudioSourcePlugin : IAudioFileSourcePlugin
    {
        public string Name => "MIDI読み込み";

        private readonly HashSet<string> supportedExtensions;
        private MidiConfiguration configuration => MidiConfiguration.Default;

        public MidiAudioSourcePlugin()
        {
            MidiConfiguration.Default.Initialize();
            supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mid", ".midi", ".kar", ".rmi"
            };
        }

        public IAudioFileSource? CreateAudioFileSource(string filePath, int audioTrackIndex)
        {
            try
            {
                var extension = Path.GetExtension(filePath)?.ToLower();
                if (string.IsNullOrEmpty(extension) || !supportedExtensions.Contains(extension))
                {
                    return null;
                }

                if (!File.Exists(filePath))
                {
                    return null;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    return null;
                }

                var dynamicConfig = CreateDynamicConfiguration(fileInfo);
                return new MidiAudioSource(filePath, dynamicConfig);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (OutOfMemoryException)
            {
                try
                {
                    var lowMemoryConfig = CreateLowMemoryConfiguration();
                    return new MidiAudioSource(filePath, lowMemoryConfig);
                }
                catch
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogError($"ファイル読み込みエラー ({filePath}): {ex.Message}");
                return null;
            }
        }

        private MidiConfiguration CreateDynamicConfiguration(FileInfo fileInfo)
        {
            var config = MidiConfiguration.Default;

            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);

            if (fileSizeMB > 10)
            {
                config.Performance.MaxPolyphony = Math.Min(config.Performance.MaxPolyphony, 64);
                config.Performance.BufferSize = Math.Max(config.Performance.BufferSize, 2048);
                config.Audio.SampleRate = Math.Min(config.Audio.SampleRate, 22050);
                config.Effects.EnableEffects = false;
            }
            else if (fileSizeMB > 5)
            {
                config.Performance.MaxPolyphony = Math.Min(config.Performance.MaxPolyphony, 128);
                config.Performance.BufferSize = Math.Max(config.Performance.BufferSize, 1024);
            }
            else if (fileSizeMB < 0.1)
            {
                config.Performance.MaxPolyphony = Math.Max(config.Performance.MaxPolyphony, 512);
                config.Performance.BufferSize = Math.Min(config.Performance.BufferSize, 512);
                if (config.Audio.SampleRate < 44100)
                {
                    config.Audio.SampleRate = 44100;
                }
            }

            return config;
        }

        private MidiConfiguration CreateLowMemoryConfiguration()
        {
            var config = MidiConfiguration.Default;
            config.Audio.SampleRate = 22050;
            config.Performance.MaxPolyphony = 32;
            config.Performance.BufferSize = 2048;
            config.Performance.EnableParallelProcessing = false;
            config.Effects.EnableEffects = false;
            config.SoundFont.EnableSoundFont = false;
            return config;
        }

        public bool CanHandle(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath)?.ToLower();
                if (string.IsNullOrEmpty(extension))
                {
                    return false;
                }

                if (!supportedExtensions.Contains(extension))
                {
                    return false;
                }

                if (!File.Exists(filePath))
                {
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0 || fileInfo.Length > 100 * 1024 * 1024)
                {
                    return false;
                }

                using var fileStream = File.OpenRead(filePath);
                var header = new byte[4];
                if (fileStream.Read(header, 0, 4) != 4)
                {
                    return false;
                }

                var headerString = System.Text.Encoding.ASCII.GetString(header);
                return headerString == "MThd";
            }
            catch
            {
                return false;
            }
        }

        public string[] GetSupportedExtensions()
        {
            return supportedExtensions.ToArray();
        }

        public MidiFileInfo? GetFileInfo(string filePath)
        {
            try
            {
                if (!CanHandle(filePath))
                {
                    return null;
                }

                using var fileStream = File.OpenRead(filePath);
                var buffer = new byte[14];
                if (fileStream.Read(buffer, 0, 14) != 14)
                {
                    return null;
                }

                if (System.Text.Encoding.ASCII.GetString(buffer, 0, 4) != "MThd")
                {
                    return null;
                }

                var headerLength = (buffer[4] << 24) | (buffer[5] << 16) | (buffer[6] << 8) | buffer[7];
                var format = (buffer[8] << 8) | buffer[9];
                var trackCount = (buffer[10] << 8) | buffer[11];
                var division = (buffer[12] << 8) | buffer[13];

                var fileInfo = new FileInfo(filePath);
                var recommendedSettings = AnalyzeFileAndRecommendSettings(fileInfo, format, trackCount);

                return new MidiFileInfo
                {
                    FilePath = filePath,
                    Format = format,
                    TrackCount = trackCount,
                    Division = division,
                    FileSize = fileInfo.Length,
                    RecommendedSettings = recommendedSettings,
                    SupportsSoundFont = HasSoundFontSupport(),
                    EstimatedComplexity = CalculateComplexity(fileInfo.Length, trackCount),
                    ConfigurationStatus = GetConfigurationStatus()
                };
            }
            catch
            {
                return null;
            }
        }

        private RecommendedSettings AnalyzeFileAndRecommendSettings(FileInfo fileInfo, int format, int trackCount)
        {
            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
            var complexity = CalculateComplexity(fileInfo.Length, trackCount);

            return new RecommendedSettings
            {
                SampleRate = complexity switch
                {
                    <= 2 => 48000,
                    <= 5 => 44100,
                    <= 8 => 44100,
                    _ => 22050
                },
                MaxPolyphony = complexity switch
                {
                    <= 2 => 512,
                    <= 5 => 256,
                    <= 8 => 128,
                    _ => 64
                },
                BufferSize = complexity switch
                {
                    <= 2 => 512,
                    <= 5 => 1024,
                    _ => 2048
                },
                EnableEffects = complexity <= 5,
                EnableParallelProcessing = trackCount > 4,
                UseSoundFont = HasSoundFontSupport() && complexity <= 8
            };
        }

        private int CalculateComplexity(long fileSize, int trackCount)
        {
            var sizeScore = (int)(fileSize / (1024 * 1024));
            var trackScore = trackCount / 2;
            return Math.Min(10, sizeScore + trackScore);
        }

        private bool HasSoundFontSupport()
        {
            try
            {
                var assemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (assemblyLocation == null) return false;

                var defaultSf2Path = Path.Combine(assemblyLocation, "GeneralUser-GS.sf2");
                if (File.Exists(defaultSf2Path)) return true;

                var userSf2Directory = Path.Combine(assemblyLocation, configuration.SoundFont.DefaultSoundFontDirectory);
                if (Directory.Exists(userSf2Directory))
                {
                    var userSf2Files = Directory.GetFiles(userSf2Directory, "*.sf2", SearchOption.AllDirectories);
                    if (userSf2Files.Any(f => File.Exists(f) && new FileInfo(f).Length > 0))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private ConfigurationStatus GetConfigurationStatus()
        {
            try
            {
                var assemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (assemblyLocation == null) return ConfigurationStatus.NotFound;

                var configPath = Path.Combine(assemblyLocation, "MidiPluginConfig.json");
                if (!File.Exists(configPath)) return ConfigurationStatus.NotFound;

                var fileInfo = new FileInfo(configPath);
                var isModified = fileInfo.LastWriteTime > fileInfo.CreationTime.AddMinutes(1);

                return isModified ? ConfigurationStatus.Modified : ConfigurationStatus.Default;
            }
            catch
            {
                return ConfigurationStatus.Error;
            }
        }

        private void LogError(string message)
        {
            if (!configuration.Debug.EnableLogging) return;

            try
            {
                var assemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (assemblyLocation == null) return;

                var logPath = Path.Combine(assemblyLocation, configuration.Debug.LogFilePath);
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PLUGIN] {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
            }
        }

        public class MidiFileInfo
        {
            public string FilePath { get; set; } = string.Empty;
            public int Format { get; set; }
            public int TrackCount { get; set; }
            public int Division { get; set; }
            public long FileSize { get; set; }
            public RecommendedSettings RecommendedSettings { get; set; } = new();
            public bool SupportsSoundFont { get; set; }
            public int EstimatedComplexity { get; set; }
            public ConfigurationStatus ConfigurationStatus { get; set; }

            public string GetFormatDescription()
            {
                return Format switch
                {
                    0 => "単一トラック",
                    1 => "マルチトラック（同期）",
                    2 => "マルチトラック（非同期）",
                    _ => "不明な形式"
                };
            }

            public string GetComplexityDescription()
            {
                return EstimatedComplexity switch
                {
                    <= 2 => "低（高品質推奨）",
                    <= 5 => "中（標準品質）",
                    <= 8 => "高（軽量設定推奨）",
                    _ => "超高（最軽量設定推奨）"
                };
            }

            public string GetConfigurationStatusDescription()
            {
                return ConfigurationStatus switch
                {
                    ConfigurationStatus.Default => "デフォルト設定",
                    ConfigurationStatus.Modified => "カスタム設定",
                    ConfigurationStatus.NotFound => "設定ファイル未検出",
                    ConfigurationStatus.Error => "設定ファイルエラー",
                    _ => "不明"
                };
            }
        }

        public class RecommendedSettings
        {
            public int SampleRate { get; set; }
            public int MaxPolyphony { get; set; }
            public int BufferSize { get; set; }
            public bool EnableEffects { get; set; }
            public bool EnableParallelProcessing { get; set; }
            public bool UseSoundFont { get; set; }
        }

        public enum ConfigurationStatus
        {
            Default,
            Modified,
            NotFound,
            Error
        }
    }
}