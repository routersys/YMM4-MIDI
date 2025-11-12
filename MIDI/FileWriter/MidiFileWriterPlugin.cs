using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.FileWriter;
using YukkuriMovieMaker.Project;
using MIDI.Utils;

namespace MIDI.FileWriter
{
    public class MidiFileWriterPlugin : IVideoFileWriterPlugin
    {
        public string Name => Translate.PluginWriterName;
        public VideoFileWriterOutputPath OutputPathMode => VideoFileWriterOutputPath.File;

        private MidiFileWriterConfigViewModel? _sharedViewModel;
        private readonly object _viewModelLock = new object();

        public IVideoFileWriter CreateVideoFileWriter(string path, VideoInfo videoInfo)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Logger.Error("Invalid file path provided to CreateVideoFileWriter", new ArgumentException(nameof(path)));
                throw new ArgumentException("File path cannot be null or empty.", nameof(path));
            }

            if (videoInfo == null)
            {
                Logger.Error("VideoInfo is null in CreateVideoFileWriter", new ArgumentNullException(nameof(videoInfo)));
                throw new ArgumentNullException(nameof(videoInfo));
            }

            try
            {
                MidiFileWriterConfigViewModel viewModel;

                lock (_viewModelLock)
                {
                    if (_sharedViewModel == null)
                    {
                        _sharedViewModel = new MidiFileWriterConfigViewModel();
                        Logger.Info("Created new shared view model configuration.", 5);
                    }
                    viewModel = _sharedViewModel;
                }

                if (!viewModel.ValidateConfiguration())
                {
                    Logger.Warn("Invalid configuration detected, resetting to defaults.", 3);
                    viewModel.ResetToDefaults();
                }

                var writer = new MidiFileWriter(path, videoInfo, viewModel);
                Logger.Info($"Created MidiFileWriter for path: {path}", 4);
                return writer;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create MidiFileWriter for path: {path}", ex);
                throw;
            }
        }

        public Task DownloadResources(ProgressMessage progress, CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public string GetFileExtention()
        {
            return ".mid";
        }

        public UIElement GetVideoConfigView(string projectName, VideoInfo videoInfo, int length)
        {
            try
            {
                MidiFileWriterConfigViewModel viewModel;

                lock (_viewModelLock)
                {
                    if (_sharedViewModel == null)
                    {
                        _sharedViewModel = new MidiFileWriterConfigViewModel();
                        Logger.Info("Created new view model for config view.", 5);
                    }
                    viewModel = _sharedViewModel;
                }

                var view = new MidiFileWriterConfigView
                {
                    DataContext = viewModel
                };

                Logger.Info("Created configuration view.", 5);
                return view;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create configuration view", ex);

                var errorText = new System.Windows.Controls.TextBlock
                {
                    Text = string.Format(Translate.MidiWriterConfigLoadError, ex.Message),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10)
                };
                return errorText;
            }
        }

        public bool NeedDownloadResources()
        {
            return false;
        }
    }
}