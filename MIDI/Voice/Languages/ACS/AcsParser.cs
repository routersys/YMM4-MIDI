using MIDI.Utils;
using MIDI.Voice.Languages.ACS;
using MIDI.Voice.Languages.Core;
using MIDI.Voice.Languages.Interface;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MIDI.Voice.Languages.ACS
{
    public class AcsParser : ILanguageParser
    {
        public string LanguageName => "ACS";

        private static readonly Regex NoteRegex = new Regex(
            @"^(?<Note>[A-Ga-g]#?[0-9])" +
            @"(?:[Dd](?<Duration>[0-9]+(?:\.[0-9]+)?))?" +
            @"(?:[Vv](?<Volume>[0-9]+(?:\.[0-9]+)?))?" +
            @"(?:[Ll](?<LegatoOrLength>[0-9]+(?:\.[0-9]+)?))?" +
            @"(?:PB(?<PitchBend>-?\d+(?:\.\d+)?))?" +
            @"(?:[Mm](?<Modulation>\d+))?" +
            @"(?:[Ee](?<Expression>\d+))?" +
            @"(?:[Pp](?<Pan>\d+))?" +
            @"(?:CP(?<ChannelPressure>\d+))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RestRegex = new Regex(
            @"^R(?:[DL](?<Duration>[0-9]+(?:\.[0-9]+)?))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AcsCmdRegex = new Regex(
            @"^<(?<Type>CC|Program|PitchBend|ChannelPressure)=(?<Data1>-?\d+)(?:\sV=(?<Data2>-?\d+))?\s*>$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MidiCmdRegex = new Regex(
            @"CMD:(?<Type>[A-Za-z]+)\((?<Params>[^)]*)\)",
            RegexOptions.Compiled);

        private static readonly Regex HeaderRegex = new Regex(@"^#!Track=\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CommentRegex = new Regex(@"^\s*//", RegexOptions.Compiled);
        private static readonly Regex LanguageHeaderRegex = new Regex(@"^\s*#!(EMEL|SUSL)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public int CheckConfidence(string inputText)
        {
            if (string.IsNullOrWhiteSpace(inputText))
            {
                return 0;
            }

            var lines = inputText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (!lines.Any())
            {
                return 0;
            }

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                if (LanguageHeaderRegex.IsMatch(trimmedLine))
                {
                    return 0;
                }
            }

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim().Trim('\'');
                if (string.IsNullOrEmpty(trimmedLine) || CommentRegex.IsMatch(trimmedLine) || HeaderRegex.IsMatch(trimmedLine))
                {
                    continue;
                }

                if (NoteRegex.IsMatch(trimmedLine) || RestRegex.IsMatch(trimmedLine) || AcsCmdRegex.IsMatch(trimmedLine) || MidiCmdRegex.IsMatch(trimmedLine))
                {
                    return 90;
                }
            }

            return 10;
        }

        public IParseResult Parse(string text)
        {
            var events = new List<object>();
            var errors = new List<IParseError>();
            double defaultLength = 0.5;
            float defaultVolume = 1.0f;

            var channelCurrentTime = new Dictionary<int, double>();
            int currentChannel = 1;

            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            int lineNum = 0;

            foreach (var line in lines)
            {
                lineNum++;
                var trimmedLine = line.Trim().Trim('\'');
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("//"))
                    continue;

                if (trimmedLine.StartsWith("#!Track="))
                {
                    try
                    {
                        currentChannel = int.Parse(trimmedLine.Substring(8));
                        if (currentChannel < 1) currentChannel = 1;
                        if (!channelCurrentTime.ContainsKey(currentChannel))
                        {
                            channelCurrentTime[currentChannel] = 0.0;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("トラック番号の解析に失敗しました。", ex);
                        errors.Add(new ParseError("ACS_InvalidTrackHeader", 'A', lineNum, 1, trimmedLine.Length, ex.Message));
                        currentChannel = 1;
                    }
                    continue;
                }

                if (!channelCurrentTime.ContainsKey(currentChannel))
                {
                    channelCurrentTime[currentChannel] = 0.0;
                }
                double currentTime = channelCurrentTime[currentChannel];

                var noteMatch = NoteRegex.Match(trimmedLine);
                if (noteMatch.Success)
                {
                    try
                    {
                        var noteName = noteMatch.Groups["Note"].Value;
                        var durationStr = noteMatch.Groups["Duration"].Value;
                        var lengthStr = noteMatch.Groups["LegatoOrLength"].Value;
                        var volumeStr = noteMatch.Groups["Volume"].Value;
                        var pbStr = noteMatch.Groups["PitchBend"].Value;
                        var modStr = noteMatch.Groups["Modulation"].Value;
                        var expStr = noteMatch.Groups["Expression"].Value;
                        var panStr = noteMatch.Groups["Pan"].Value;
                        var cpStr = noteMatch.Groups["ChannelPressure"].Value;

                        double duration = defaultLength;
                        if (!string.IsNullOrEmpty(durationStr))
                        {
                            duration = double.Parse(durationStr, CultureInfo.InvariantCulture);
                        }
                        else if (!string.IsNullOrEmpty(lengthStr))
                        {
                            duration = double.Parse(lengthStr, CultureInfo.InvariantCulture);
                        }

                        int channel = (currentChannel - 1) % 16 + 1;

                        var noteData = new NoteData
                        {
                            AbsoluteTimeSeconds = currentTime,
                            DurationSeconds = (float)duration,
                            Volume = string.IsNullOrEmpty(volumeStr) ? defaultVolume : (float)(double.Parse(volumeStr, CultureInfo.InvariantCulture) / 127.0),
                            MidiNoteNumber = NoteNameToMidi(noteName),
                            Channel = channel,
                            Track = currentChannel,
                            PitchBend = string.IsNullOrEmpty(pbStr) ? 8192 : (int)double.Parse(pbStr, CultureInfo.InvariantCulture),
                            Modulation = string.IsNullOrEmpty(modStr) ? 0 : int.Parse(modStr),
                            Expression = string.IsNullOrEmpty(expStr) ? 127 : int.Parse(expStr),
                            Pan = string.IsNullOrEmpty(panStr) ? 64 : int.Parse(panStr),
                            ChannelPressure = string.IsNullOrEmpty(cpStr) ? 0 : int.Parse(cpStr)
                        };

                        events.Add(noteData);
                        channelCurrentTime[currentChannel] = currentTime + noteData.DurationSeconds;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("表記の解析エラーが発生しました。", ex);
                        errors.Add(new ParseError("ACS_NoteParseError", 'A', lineNum, 1, trimmedLine.Length, ex.Message));
                        events.Add(NoteData.CreateErrorNote(currentChannel, currentTime, (float)defaultLength));
                        channelCurrentTime[currentChannel] = currentTime + defaultLength;
                    }
                }
                else
                {
                    var restMatch = RestRegex.Match(trimmedLine);
                    if (restMatch.Success)
                    {
                        try
                        {
                            var durationStr = restMatch.Groups["Duration"].Value;
                            double duration = string.IsNullOrEmpty(durationStr) ? defaultLength : double.Parse(durationStr, CultureInfo.InvariantCulture);
                            channelCurrentTime[currentChannel] = currentTime + duration;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("休符の解析エラーが発生しました。", ex);
                            errors.Add(new ParseError("ACS_RestParseError", 'A', lineNum, 1, trimmedLine.Length, ex.Message));
                            events.Add(NoteData.CreateErrorNote(currentChannel, currentTime, (float)defaultLength));
                            channelCurrentTime[currentChannel] = currentTime + defaultLength;
                        }
                    }
                    else
                    {
                        var acsCmdMatch = AcsCmdRegex.Match(trimmedLine);
                        if (acsCmdMatch.Success)
                        {
                            try
                            {
                                var cmdTypeStr = acsCmdMatch.Groups["Type"].Value;
                                var data1Str = acsCmdMatch.Groups["Data1"].Value;
                                var data2Str = acsCmdMatch.Groups["Data2"].Value;

                                int channel = (currentChannel - 1) % 16 + 1;
                                var cmd = new MidiCommandData
                                {
                                    AbsoluteTimeSeconds = currentTime,
                                    Channel = channel,
                                    Track = currentChannel,
                                    Data1 = int.Parse(data1Str),
                                    Data2 = string.IsNullOrEmpty(data2Str) ? 0 : int.Parse(data2Str)
                                };

                                if (cmdTypeStr.Equals("CC", StringComparison.OrdinalIgnoreCase))
                                {
                                    cmd.Type = CommandType.ControlChange;
                                }
                                else if (cmdTypeStr.Equals("Program", StringComparison.OrdinalIgnoreCase))
                                {
                                    cmd.Type = CommandType.ProgramChange;
                                }
                                else if (cmdTypeStr.Equals("PitchBend", StringComparison.OrdinalIgnoreCase))
                                {
                                    cmd.Type = CommandType.PitchBend;
                                    int pbVal = cmd.Data1;
                                    cmd.Data1 = pbVal & 0x7F;
                                    cmd.Data2 = (pbVal >> 7) & 0x7F;
                                }
                                else if (cmdTypeStr.Equals("ChannelPressure", StringComparison.OrdinalIgnoreCase))
                                {
                                    cmd.Type = CommandType.ChannelPressure;
                                }
                                else
                                {
                                    throw new Exception($"Unknown ACS command type: {cmdTypeStr}");
                                }

                                events.Add(cmd);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("ACSコマンドの解析エラーが発生しました。", ex);
                                errors.Add(new ParseError("ACS_CommandParseError", 'A', lineNum, 1, trimmedLine.Length, ex.Message));
                                events.Add(NoteData.CreateErrorNote(currentChannel, currentTime, 0.01f));
                            }
                        }
                        else
                        {
                            var cmdMatch = MidiCmdRegex.Match(trimmedLine);
                            if (cmdMatch.Success)
                            {
                                try
                                {
                                    var cmdTypeStr = cmdMatch.Groups["Type"].Value;
                                    var paramStr = cmdMatch.Groups["Params"].Value;
                                    var paramParts = paramStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                                    int channel = (currentChannel - 1) % 16 + 1;

                                    if (Enum.TryParse<CommandType>(cmdTypeStr, true, out var cmdType) && paramParts.Length >= 2)
                                    {
                                        var cmd = new MidiCommandData
                                        {
                                            AbsoluteTimeSeconds = currentTime,
                                            Channel = paramParts.Length > 2 ? int.Parse(paramParts[2]) : channel,
                                            Track = currentChannel,
                                            Type = cmdType,
                                            Data1 = int.Parse(paramParts[0]),
                                            Data2 = int.Parse(paramParts[1]),
                                        };
                                        events.Add(cmd);
                                    }
                                    else if (cmdTypeStr.Equals("Tempo", StringComparison.OrdinalIgnoreCase) && paramParts.Length >= 1)
                                    {
                                    }
                                    else if (cmdTypeStr.Equals("Length", StringComparison.OrdinalIgnoreCase) && paramParts.Length >= 1)
                                    {
                                        defaultLength = double.Parse(paramParts[0], CultureInfo.InvariantCulture);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error("MIDIコマンドの解析エラーが発生しました。", ex);
                                    errors.Add(new ParseError("ACS_CommandParseError", 'A', lineNum, 1, trimmedLine.Length, ex.Message));
                                    events.Add(NoteData.CreateErrorNote(currentChannel, currentTime, 0.01f));
                                }
                            }
                            else
                            {
                                Logger.Warn($"不明な表記です: {trimmedLine}", null!);
                                errors.Add(new ParseError("ACS_UnknownSyntax", 'A', lineNum, 1, trimmedLine.Length, trimmedLine));
                                events.Add(NoteData.CreateErrorNote(currentChannel, currentTime, (float)defaultLength));
                                channelCurrentTime[currentChannel] = currentTime + defaultLength;
                            }
                        }
                    }
                }
            }

            events.Sort((a, b) =>
            {
                double timeA = 0.0;
                double timeB = 0.0;

                if (a is NoteData ndA) timeA = ndA.AbsoluteTimeSeconds;
                else if (a is MidiCommandData cdA) timeA = cdA.AbsoluteTimeSeconds;

                if (b is NoteData ndB) timeB = ndB.AbsoluteTimeSeconds;
                else if (b is MidiCommandData cdB) timeB = cdB.AbsoluteTimeSeconds;

                return timeA.CompareTo(timeB);
            });

            return new ParseResult(events, LanguageName, errors);
        }

        public IReadOnlyDictionary<long, string> GetErrorDefinitions()
        {
            return new Dictionary<long, string>();
        }

        private static readonly Dictionary<string, int> NoteBaseValues = new Dictionary<string, int>
        {
            {"C", 0}, {"C#", 1}, {"D", 2}, {"D#", 3}, {"E", 4}, {"F", 5},
            {"F#", 6}, {"G", 7}, {"G#", 8}, {"A", 9}, {"A#", 10}, {"B", 11}
        };

        private int NoteNameToMidi(string noteName)
        {
            var match = Regex.Match(noteName.ToUpper(), @"([A-G]#?)([0-9])");
            if (!match.Success) throw new ArgumentException("Invalid note name format: " + noteName);

            string key = match.Groups[1].Value;
            int octave = int.Parse(match.Groups[2].Value);

            return NoteBaseValues[key] + (octave + 1) * 12;
        }
    }
}