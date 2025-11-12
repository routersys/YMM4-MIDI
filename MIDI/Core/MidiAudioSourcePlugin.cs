using MessagePack;
using MIDI.API;
using MIDI.Core;
using MIDI.UI.ViewModels;
using MIDI.UI.ViewModels.MidiEditor.Modals;
using MIDI.UI.Views;
using MIDI.UI.Views.MidiEditor.Modals;
using MIDI.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.FileSource;
using MIDI.Configuration.Models;
using System.Threading;

namespace MIDI
{
    [PluginDetails(
        AuthorName = "routersys",
        ContentId = ""
    )]
    public class MidiAudioSourcePlugin : IAudioFileSourcePlugin, IDisposable
    {
        public string Name => Translate.PluginName;

        private readonly HashSet<string> supportedExtensions;
        private MidiConfiguration configuration => MidiConfiguration.Default;
        private MidiEditorSettings editorSettings => MidiEditorSettings.Default;
        private static NamedPipeServer? namedPipeServer;
        private static readonly object serverLock = new object();
        private static bool isGpuInitialized = false;
        private static readonly object gpuInitLock = new object();

        private static MidiInfoWindow? midiInfoWindowInstance;
        private static MidiEditorWindow? midiEditorWindowInstance;
        private static readonly HashSet<string> sessionShownInfoFilePaths = new HashSet<string>();
        private static readonly HashSet<string> sessionShownEditorFilePaths = new HashSet<string>();
        private bool disposedValue;


        public MidiAudioSourcePlugin()
        {
            MidiConfiguration.Default.Initialize();
            MidiEditorSettings.Default.Initialize();
            Logger.Info(LogMessages.PluginInstantiated);

            if (configuration.Performance.InitializeGpuOnStartup)
            {
                InitializeGpuDevice();
            }

            supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mid", ".midi", ".kar", ".rmi", ".ymidi"
            };

            InitializeDirectories();
            InitializeNamedPipeServer();
        }

        private void InitializeGpuDevice()
        {
            lock (gpuInitLock)
            {
                if (!isGpuInitialized)
                {
                    _ = GpuDeviceProvider.InitializeAsync();
                    isGpuInitialized = true;
                }
            }
        }

        private void InitializeNamedPipeServer()
        {
            lock (serverLock)
            {
                if (configuration.Debug.EnableNamedPipeApi)
                {
                    if (namedPipeServer == null)
                    {
                        Logger.Info(LogMessages.NamedPipeEnabled);
                        var viewModel = new MidiSettingsViewModel();
                        namedPipeServer = new NamedPipeServer(viewModel, configuration);
                        if (!namedPipeServer.TryStartServer())
                        {
                            namedPipeServer.Dispose();
                            namedPipeServer = null;
                        }
                    }
                    else
                    {
                        Logger.Info(LogMessages.NamedPipeAlreadyRunning);
                    }
                }
                else
                {
                    StopNamedPipeServer();
                }
            }
        }

        private void StopNamedPipeServer()
        {
            lock (serverLock)
            {
                if (namedPipeServer != null)
                {
                    Logger.Info(LogMessages.NamedPipeDisabled);
                    namedPipeServer.Dispose();
                    namedPipeServer = null;
                }
            }
        }

        private void InitializeDirectories()
        {
            try
            {
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(assemblyLocation))
                {
                    Logger.Error(LogMessages.AssemblyLocationError, null);
                    return;
                }
                Logger.Info(LogMessages.PluginBaseDirectory, assemblyLocation);

                var sfzPath = Path.Combine(assemblyLocation, configuration.SFZ.SfzSearchPath);
                if (!Directory.Exists(sfzPath))
                {
                    Logger.Info(LogMessages.SfzDirectoryCreating, sfzPath);
                    Directory.CreateDirectory(sfzPath);
                }

                var backupEditorPath = Path.Combine(assemblyLocation, "backup", "editor");
                if (!Directory.Exists(backupEditorPath))
                {
                    Directory.CreateDirectory(backupEditorPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.DirectoryInitError, ex);
            }
        }

        public IAudioFileSource? CreateAudioFileSource(string filePath, int audioTrackIndex)
        {
            Logger.Info(LogMessages.CreateAudioSourceStart, filePath, audioTrackIndex);

            if (!configuration.Performance.InitializeGpuOnStartup)
            {
                InitializeGpuDevice();
            }

            try
            {
                var extension = Path.GetExtension(filePath)?.ToLower();
                if (string.IsNullOrEmpty(extension) || !supportedExtensions.Contains(extension))
                {
                    Logger.Warn(LogMessages.UnsupportedExtension, extension ?? "null");
                    return null;
                }
                if (extension == ".ymidi")
                {
                    if (!sessionShownEditorFilePaths.Contains(filePath))
                    {
                        ShowMidiEditorReliably(filePath);
                        sessionShownEditorFilePaths.Add(filePath);
                    }
                    return new MidiAudioSource(filePath, configuration);
                }


                if (!File.Exists(filePath))
                {
                    Logger.Error(LogMessages.FileNotExist, null, filePath);
                    return null;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    Logger.Warn(LogMessages.FileIsEmpty, filePath);
                    return null;
                }
                Logger.Info(LogMessages.FileSizeLog, fileInfo.Length);

                if (configuration.Debug.EnableMidiEditor && !sessionShownEditorFilePaths.Contains(filePath))
                {
                    sessionShownEditorFilePaths.Add(filePath);
                    ShowMidiEditorReliably(filePath);
                }

                if (configuration.Debug.ShowMidiInfoWindowOnLoad && !sessionShownInfoFilePaths.Contains(filePath))
                {
                    var midiInfo = GetFileInfo(filePath);
                    if (midiInfo != null)
                    {
                        sessionShownInfoFilePaths.Add(filePath);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (midiInfoWindowInstance != null && midiInfoWindowInstance.IsLoaded)
                            {
                                midiInfoWindowInstance.DataContext = new UI.ViewModels.MidiInfoWindowViewModel(midiInfo);
                                midiInfoWindowInstance.Activate();
                            }
                            else
                            {
                                midiInfoWindowInstance = new MidiInfoWindow(midiInfo)
                                {
                                    Owner = Application.Current.MainWindow,
                                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                                };
                                midiInfoWindowInstance.Closed += (s, e) => { midiInfoWindowInstance = null; };
                                midiInfoWindowInstance.Show();
                            }
                        });
                    }
                }

                var dynamicConfig = CreateDynamicConfiguration(fileInfo);
                Logger.Info(LogMessages.ApplyDynamicConfig);
                return new MidiAudioSource(filePath, dynamicConfig);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is DirectoryNotFoundException || ex is FileNotFoundException || ex is IOException)
            {
                Logger.Error(LogMessages.FileAccessError, ex, filePath);
                return null;
            }
            catch (OutOfMemoryException ex)
            {
                Logger.Error(LogMessages.OutOfMemoryRetry, ex, filePath);
                try
                {
                    var lowMemoryConfig = CreateLowMemoryConfiguration();
                    return new MidiAudioSource(filePath, lowMemoryConfig);
                }
                catch (Exception re)
                {
                    Logger.Error(LogMessages.OutOfMemoryRetryFailed, re, filePath);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.CreateAudioSourceError, ex, filePath);
                return null;
            }
        }

        private void ShowMidiEditorReliably(string filePath)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (midiEditorWindowInstance != null && midiEditorWindowInstance.IsLoaded)
                {
                    midiEditorWindowInstance.DataContext = new MidiEditorViewModel(filePath);
                    if (midiEditorWindowInstance.WindowState == WindowState.Minimized)
                    {
                        midiEditorWindowInstance.WindowState = WindowState.Normal;
                    }
                    midiEditorWindowInstance.Activate();
                    return;
                }

                var loadingWindow = new MidiEditorLoadingWindow()
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                loadingWindow.Show();

                Task.Run(async () =>
                {
                    MidiEditorWindow? newEditorWindow = null;
                    var viewModel = await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        newEditorWindow = new MidiEditorWindow(filePath)
                        {
                            Owner = Application.Current.MainWindow,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        };
                        return (MidiEditorViewModel)newEditorWindow.DataContext;
                    });

                    await viewModel.LoadingTask;

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        loadingWindow.Close();
                        midiEditorWindowInstance = newEditorWindow;
                        midiEditorWindowInstance!.Closed += (s, e) => { midiEditorWindowInstance = null; };
                        midiEditorWindowInstance.Show();
                    });
                });
            });
        }


        private MidiConfiguration CreateDynamicConfiguration(FileInfo fileInfo)
        {
            var config = configuration.Clone();
            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
            Logger.Info(LogMessages.DynamicConfigStart, fileSizeMB);

            if (fileSizeMB > 10)
            {
                Logger.Info(LogMessages.LowSpecConfigApplied);
                config.Performance.MaxPolyphony = Math.Min(config.Performance.MaxPolyphony, 64);
                config.Performance.BufferSize = Math.Max(config.Performance.BufferSize, 2048);
                config.Audio.SampleRate = Math.Min(config.Audio.SampleRate, 22050);
                config.Effects.EnableEffects = false;
            }
            else if (fileSizeMB > 5)
            {
                Logger.Info(LogMessages.MidSpecConfigApplied);
                config.Performance.MaxPolyphony = Math.Min(config.Performance.MaxPolyphony, 128);
                config.Performance.BufferSize = Math.Max(config.Performance.BufferSize, 1024);
            }
            else if (fileSizeMB < 0.1)
            {
                Logger.Info(LogMessages.HighSpecConfigApplied);
                config.Performance.MaxPolyphony = Math.Max(config.Performance.MaxPolyphony, 512);
                config.Performance.BufferSize = Math.Min(config.Performance.BufferSize, 512);
                if (config.Audio.SampleRate < 44100)
                {
                    config.Audio.SampleRate = 44100;
                }
            }
            else
            {
                Logger.Info(LogMessages.DefaultSpecConfigApplied);
            }

            return config;
        }

        private MidiConfiguration CreateLowMemoryConfiguration()
        {
            Logger.Warn(LogMessages.LowMemoryConfigCreated);
            var config = configuration.Clone();
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

                if (extension == ".ymidi") return true;

                using var fileStream = File.OpenRead(filePath);
                var header = new byte[4];
                if (fileStream.Read(header, 0, 4) != 4)
                {
                    return false;
                }

                var headerString = System.Text.Encoding.ASCII.GetString(header);
                bool canHandle = headerString == "MThd";
                Logger.Info(LogMessages.CanHandleLog, filePath, canHandle, headerString);
                return canHandle;
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.CanHandleError, ex, filePath);
                return false;
            }
        }

        public string[] GetSupportedExtensions()
        {
            return supportedExtensions.ToArray();
        }

        public MidiFileInfo? GetFileInfo(string filePath)
        {
            Logger.Info(LogMessages.GetFileInfoStart, filePath);
            try
            {
                if (!CanHandle(filePath))
                {
                    Logger.Warn(LogMessages.GetFileInfoNotSupported, filePath);
                    return null;
                }

                using var fileStream = File.OpenRead(filePath);
                var buffer = new byte[14];
                if (fileStream.Read(buffer, 0, 14) != 14)
                {
                    Logger.Error(LogMessages.GetFileInfoReadHeaderFailed, null, filePath);
                    return null;
                }

                if (System.Text.Encoding.ASCII.GetString(buffer, 0, 4) != "MThd")
                {
                    Logger.Warn(LogMessages.GetFileInfoInvalid, filePath);
                    return null;
                }

                var headerLength = (buffer[4] << 24) | (buffer[5] << 16) | (buffer[6] << 8) | buffer[7];
                var format = (buffer[8] << 8) | buffer[9];
                var trackCount = (buffer[10] << 8) | buffer[11];
                var division = (buffer[12] << 8) | buffer[13];

                var fileInfo = new FileInfo(filePath);
                var recommendedSettings = AnalyzeFileAndRecommendSettings(fileInfo, format, trackCount);

                Logger.Info(LogMessages.GetFileInfoSuccess, filePath, format, trackCount);
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
            catch (Exception ex)
            {
                Logger.Error(LogMessages.GetFileInfoError, ex, filePath);
                return null;
            }
        }

        private RecommendedSettings AnalyzeFileAndRecommendSettings(FileInfo fileInfo, int format, int trackCount)
        {
            var complexity = CalculateComplexity(fileInfo.Length, trackCount);
            Logger.Info(LogMessages.FileComplexity, complexity);

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
            var sizeScore = (int)(fileSize / (512 * 1024));
            var trackScore = trackCount / 4;
            return Math.Min(10, sizeScore + trackScore);
        }

        private bool HasSoundFontSupport()
        {
            try
            {
                var assemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (assemblyLocation == null) return false;

                var defaultSf2Path = Path.Combine(assemblyLocation, "GeneralUser-GS.sf2");
                if (File.Exists(defaultSf2Path))
                {
                    Logger.Info(LogMessages.DefaultSoundFontFound);
                    return true;
                }

                var userSf2Directory = Path.Combine(assemblyLocation, configuration.SoundFont.DefaultSoundFontDirectory);
                if (Directory.Exists(userSf2Directory))
                {
                    var userSf2Files = Directory.GetFiles(userSf2Directory, "*.sf2", SearchOption.AllDirectories);
                    if (userSf2Files.Any(f => File.Exists(f) && new FileInfo(f).Length > 0))
                    {
                        Logger.Info(LogMessages.UserSoundFontFound);
                        return true;
                    }
                }

                Logger.Info(LogMessages.SoundFontNotFound);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.SoundFontCheckError, ex);
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
            catch (Exception ex)
            {
                Logger.Error(LogMessages.ConfigStatusCheckError, ex);
                return ConfigurationStatus.Error;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    StopNamedPipeServer();
                    Logger.Stop();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
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