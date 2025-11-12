using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MIDI.Voice.Models;
using MIDI.Utils;

namespace MIDI.Voice.Services
{
    public class UtauVoiceService
    {
        public async Task<List<UtauVoiceInfo>> LoadUtauVoicesAsync(IEnumerable<string> baseFolders)
        {
            var voiceInfos = new List<UtauVoiceInfo>();
            var tasks = new List<Task<List<UtauVoiceInfo>>>();

            foreach (var baseFolder in baseFolders)
            {
                tasks.Add(Task.Run(() =>
                {
                    var loadedInFolder = new List<UtauVoiceInfo>();
                    try
                    {
                        if (!Directory.Exists(baseFolder))
                        {
                            return loadedInFolder;
                        }

                        var potentialFolders = new List<string>();

                        if (File.Exists(Path.Combine(baseFolder, "character.txt")))
                        {
                            potentialFolders.Add(baseFolder);
                        }
                        else
                        {
                            potentialFolders.AddRange(Directory.GetDirectories(baseFolder, "*", SearchOption.TopDirectoryOnly));
                        }

                        foreach (var folder in potentialFolders)
                        {
                            try
                            {
                                var info = UtauVoiceInfo.Load(folder);
                                if (info != null && !string.IsNullOrEmpty(info.Name))
                                {
                                    loadedInFolder.Add(info);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Failed to load UTAU voice info from folder: {folder}. ", ex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to scan base folder: {baseFolder}.", ex);
                    }
                    return loadedInFolder;
                }));
            }

            var results = await Task.WhenAll(tasks);
            foreach (var result in results)
            {
                voiceInfos.AddRange(result);
            }

            return voiceInfos.OrderBy(v => v.Name).ToList();
        }

        public UtauVoiceInfo? LoadUtauVoice(string folderPath)
        {
            try
            {
                return UtauVoiceInfo.Load(folderPath);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}