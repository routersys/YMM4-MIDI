using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using System.Text;

namespace MidiPlugin.Services
{
    public class PresetService
    {
        private readonly ConfigService _configService;

        public PresetService()
        {
            _configService = new ConfigService();
        }

        public string InstallPreset(string sourceFilePath)
        {
            var prioritizedPaths = _configService.LoadPrioritizedExePaths();

            if (!prioritizedPaths.Any())
            {
                throw new InvalidOperationException("インストール先が設定されていません。設定画面で優先度を確認してください。");
            }

            var exceptions = new List<Exception>();

            foreach (var exePath in prioritizedPaths)
            {
                try
                {
                    var exeDir = Path.GetDirectoryName(exePath);
                    if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(exeDir))
                    {
                        continue;
                    }

                    var destDir = Path.Combine(exeDir, Models.AppConfig.PresetsFolderName);

                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    var destFilePath = Path.Combine(destDir, Path.GetFileName(sourceFilePath));
                    File.Copy(sourceFilePath, destFilePath, true);

                    return destFilePath;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            var aggregatedExceptionMessage = new StringBuilder();
            aggregatedExceptionMessage.AppendLine("すべてのインストール先でエラーが発生しました:");
            foreach (var ex in exceptions)
            {
                aggregatedExceptionMessage.AppendLine($"- {ex.Message}");
            }
            throw new AggregateException(aggregatedExceptionMessage.ToString(), exceptions);
        }
    }
}