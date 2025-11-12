using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using MIDI.Utils;
using System;

namespace MIDI.Voice.Models
{
    public class UtauVoiceInfo : INotifyPropertyChanged
    {
        private string _basePath = "";
        public string BasePath { get => _basePath; set => SetField(ref _basePath, value); }

        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }

        private string _author = "";
        public string Author { get => _author; set => SetField(ref _author, value); }

        private string _imageFile = "";
        public string ImageFile { get => _imageFile; set => SetField(ref _imageFile, value); }

        private string _sampleFile = "";
        public string SampleFile { get => _sampleFile; set => SetField(ref _sampleFile, value); }

        private string _web = "";
        public string Web { get => _web; set => SetField(ref _web, value); }

        private string _version = "";
        public string Version { get => _version; set => SetField(ref _version, value); }

        public string ImagePath => !string.IsNullOrEmpty(ImageFile) && !string.IsNullOrEmpty(BasePath)
            ? Path.Combine(BasePath, ImageFile)
            : "";

        public string Info
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Author)) parts.Add($"Author: {Author}");
                if (!string.IsNullOrEmpty(Version)) parts.Add($"Ver: {Version}");
                if (!string.IsNullOrEmpty(Web)) parts.Add($"Web: {Web}");
                return string.Join(" | ", parts);
            }
        }

        static UtauVoiceInfo()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to register CodePagesEncodingProvider for UtauVoiceInfo.", ex);
            }
        }

        public static UtauVoiceInfo? Load(string folderPath)
        {
            var charTxtPath = Path.Combine(folderPath, "character.txt");
            if (!File.Exists(charTxtPath)) return null;

            var info = new UtauVoiceInfo { BasePath = folderPath };
            Encoding encoding;

            try
            {
                encoding = Encoding.GetEncoding("Shift_JIS");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to get Shift_JIS encoding, falling back to Default. Error: {ex.Message}");
                encoding = Encoding.Default;
            }

            try
            {
                foreach (var line in File.ReadLines(charTxtPath, encoding))
                {
                    try
                    {
                        var parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length != 2) continue;

                        var key = parts[0].Trim().ToLower();
                        var value = parts[1].Trim();

                        switch (key)
                        {
                            case "name":
                                info.Name = value;
                                break;
                            case "author":
                                info.Author = value;
                                break;
                            case "image":
                                info.ImageFile = value;
                                break;
                            case "sample":
                                info.SampleFile = value;
                                break;
                            case "web":
                                info.Web = value;
                                break;
                            case "version":
                                info.Version = value;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Failed to parse line in {charTxtPath}: {line}. Error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to read {charTxtPath}. Error: {ex.Message}", ex);
            }

            if (string.IsNullOrEmpty(info.Name))
            {
                info.Name = Path.GetFileName(folderPath);
            }

            info.OnPropertyChanged(nameof(ImagePath));
            info.OnPropertyChanged(nameof(Info));

            return info;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
            if (propertyName == nameof(ImageFile) || propertyName == nameof(BasePath))
                OnPropertyChanged(nameof(ImagePath));
            if (propertyName == nameof(Author) || propertyName == nameof(Version) || propertyName == nameof(Web))
                OnPropertyChanged(nameof(Info));
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
        }
    }
}