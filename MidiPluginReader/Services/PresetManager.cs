using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MidiPlugin.Models;

namespace MidiPlugin.Services
{
    public class PresetManager
    {
        private const string CertificateStartDelimiter = "---BEGIN PRESET CERTIFICATE---";
        private const string CertificateEndDelimiter = "---END PRESET CERTIFICATE---";

        public PresetDetails ParsePreset(string filePath)
        {
            var content = File.ReadAllText(filePath);

            var certStartIndex = content.IndexOf(CertificateStartDelimiter);
            var certEndIndex = content.IndexOf(CertificateEndDelimiter);

            PresetCertificate certificate = null;
            string settingsJson;

            if (certStartIndex != -1 && certEndIndex != -1)
            {
                var certContent = content.Substring(certStartIndex + CertificateStartDelimiter.Length, certEndIndex - (certStartIndex + CertificateStartDelimiter.Length)).Trim();
                settingsJson = content.Substring(certEndIndex + CertificateEndDelimiter.Length).Trim();
                certificate = ParseCertificate(certContent);
            }
            else
            {
                settingsJson = content;
            }

            try
            {
                var changedCategories = ParseSettings(settingsJson);

                return new PresetDetails
                {
                    Certificate = certificate,
                    ChangedCategories = changedCategories,
                    ChangedItems = changedCategories.SelectMany(kvp => kvp.Value).ToList()
                };
            }
            catch
            {
                return new PresetDetails { Certificate = certificate };
            }
        }

        private PresetCertificate ParseCertificate(string certContent)
        {
            var certificate = new PresetCertificate();
            var lines = certContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ':' }, 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim().Trim('"');
                var value = parts[1].Trim().Trim(',').Trim('"');

                switch (key)
                {
                    case "Issuer": certificate.Issuer = value; break;
                    case "ComputerName": certificate.ComputerName = value; break;
                    case "UserName": certificate.UserName = value; break;
                    case "CreatedAt": certificate.CreatedAt = value; break;
                }
            }
            return certificate;
        }

        private Dictionary<string, List<string>> ParseSettings(string settingsContent)
        {
            var categories = new Dictionary<string, List<string>>();

            settingsContent = Regex.Replace(settingsContent, @"//.*", "");

            var categoryRegex = new Regex(@"""(?<name>\w+)"":\s*\{(?<content>[\s\S]*?)\}(?=[,\r\n\s]*""\w+""|[\r\n\s]*\}$)", RegexOptions.Multiline);

            var arrayCategoryRegex = new Regex(@"""(?<name>\w+)"":\s*\[(?<content>[\s\S]*?)\](?=[,\r\n\s]*""\w+""|[\r\n\s]*\}$)", RegexOptions.Multiline);

            var matches = categoryRegex.Matches(settingsContent);
            foreach (Match match in matches)
            {
                var categoryName = match.Groups["name"].Value;
                var content = match.Groups["content"].Value;
                var items = new List<string>();

                var itemRegex = new Regex(@"""(\w+)"":\s*([^,{[\n\r]+)");
                var itemMatches = itemRegex.Matches(content);

                foreach (Match itemMatch in itemMatches)
                {
                    var key = itemMatch.Groups[1].Value;
                    var value = itemMatch.Groups[2].Value.Trim().TrimEnd(',');
                    items.Add($"{key}: {value}");
                }

                if (items.Any())
                {
                    categories[categoryName] = items;
                }
            }

            var arrayMatches = arrayCategoryRegex.Matches(settingsContent);
            foreach (Match match in arrayMatches)
            {
                var categoryName = match.Groups["name"].Value;
                var content = match.Groups["content"].Value.Trim();

                if (!string.IsNullOrEmpty(content))
                {
                    categories[categoryName] = new List<string> { "[項目あり]" };
                }
            }

            return categories;
        }
    }
}