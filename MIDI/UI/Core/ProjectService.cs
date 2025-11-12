using MessagePack;
using NAudio.Midi;
using MIDI.UI.ViewModels;
using MIDI.UI.ViewModels.MidiEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MIDI.Core
{
    public static class ProjectService
    {
        public static async Task<ProjectFile> CreateProjectFileAsync(string midiFilePath, MidiFile originalMidi, MidiFile currentMidi, ICollection<NoteViewModel> currentNotes, ICollection<FlagViewModel> currentFlags, bool saveAllData)
        {
            var project = new ProjectFile { MidiFilePath = midiFilePath };

            var originalNotes = originalMidi.Events
                .SelectMany(track => track.OfType<NoteOnEvent>())
                .Where(n => n.OffEvent != null)
                .GroupBy(n => (n.AbsoluteTime, n.NoteNumber))
                .ToDictionary(g => g.Key, g => g.First());

            var currentNotesMap = currentNotes
                .GroupBy(n => (n.StartTicks, n.NoteNumber))
                .ToDictionary(g => g.Key, g => g.First());

            if (saveAllData)
            {
                project.NoteChanges = currentNotes.Select(n => new NoteChange
                {
                    IsAdded = true,
                    NewStartTicks = n.StartTicks,
                    NewDurationTicks = n.DurationTicks,
                    NewNoteNumber = n.NoteNumber,
                    NewVelocity = n.Velocity,
                    NewChannel = n.Channel,
                    NewCentOffset = n.CentOffset
                }).ToList();
            }
            else
            {
                foreach (var currentNote in currentNotes)
                {
                    var noteKey = (currentNote.StartTicks, currentNote.NoteNumber);
                    if (originalNotes.TryGetValue(noteKey, out var originalEvent))
                    {
                        var change = new NoteChange
                        {
                            OriginalStartTicks = currentNote.StartTicks,
                            OriginalNoteNumber = currentNote.NoteNumber,
                            IsAdded = false,
                            IsDeleted = false
                        };
                        bool changed = false;
                        if (currentNote.DurationTicks != originalEvent.NoteLength) { change.NewDurationTicks = currentNote.DurationTicks; changed = true; }
                        if (currentNote.Velocity != originalEvent.Velocity) { change.NewVelocity = currentNote.Velocity; changed = true; }
                        if (currentNote.Channel != originalEvent.Channel) { change.NewChannel = currentNote.Channel; changed = true; }
                        if (currentNote.CentOffset != 0) { change.NewCentOffset = currentNote.CentOffset; changed = true; }

                        if (changed)
                        {
                            project.NoteChanges.Add(change);
                        }
                        originalNotes.Remove(noteKey);
                    }
                    else
                    {
                        project.NoteChanges.Add(new NoteChange
                        {
                            IsAdded = true,
                            NewStartTicks = currentNote.StartTicks,
                            NewDurationTicks = currentNote.DurationTicks,
                            NewNoteNumber = currentNote.NoteNumber,
                            NewVelocity = currentNote.Velocity,
                            NewChannel = currentNote.Channel,
                            NewCentOffset = currentNote.CentOffset
                        });
                    }
                }

                foreach (var deletedNote in originalNotes.Values)
                {
                    project.NoteChanges.Add(new NoteChange
                    {
                        OriginalStartTicks = deletedNote.AbsoluteTime,
                        OriginalNoteNumber = deletedNote.NoteNumber,
                        IsDeleted = true
                    });
                }
            }

            project.FlagOperations = currentFlags.Select(f => new FlagOperation
            {
                IsAdded = true,
                NewName = f.Name,
                NewTime = f.Time
            }).ToList();


            return await Task.FromResult(project);
        }

        public static async Task SaveProjectAsync(ProjectFile project, string path, bool compress)
        {
            var options = MessagePackSerializerOptions.Standard;
            if (compress)
            {
                options = options.WithCompression(MessagePackCompression.Lz4Block);
            }

            var bytes = MessagePackSerializer.Serialize(project, options);
            await File.WriteAllBytesAsync(path, bytes);
        }

        public static async Task<ProjectFile> LoadProjectAsync(string path)
        {
            var bytes = await File.ReadAllBytesAsync(path);
            try
            {
                return MessagePackSerializer.Deserialize<ProjectFile>(bytes, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block));
            }
            catch (MessagePackSerializationException)
            {
                return MessagePackSerializer.Deserialize<ProjectFile>(bytes);
            }
        }

        public static void ApplyProjectToMidi(MidiFile midiFile, ProjectFile project)
        {
            var notesToRemove = new List<MidiEvent>();
            var notesToAdd = new List<MidiEvent>();
            var textEventsToAdd = new List<MidiEvent>();

            foreach (var change in project.NoteChanges)
            {
                if (change.IsDeleted)
                {
                    var noteOn = midiFile.Events
                        .SelectMany(track => track)
                        .OfType<NoteOnEvent>()
                        .FirstOrDefault(e => e.AbsoluteTime == change.OriginalStartTicks && e.NoteNumber == change.OriginalNoteNumber);

                    if (noteOn != null)
                    {
                        notesToRemove.Add(noteOn);
                        if (noteOn.OffEvent != null)
                        {
                            notesToRemove.Add(noteOn.OffEvent);
                        }
                    }
                }
                else if (change.IsAdded)
                {
                    var noteOn = new NoteOnEvent(change.NewStartTicks ?? 0, change.NewChannel ?? 1, change.NewNoteNumber ?? 60, change.NewVelocity ?? 100, (int)(change.NewDurationTicks ?? 120));
                    var noteOff = new NoteEvent(noteOn.AbsoluteTime + noteOn.NoteLength, noteOn.Channel, MidiCommandCode.NoteOff, noteOn.NoteNumber, 0);
                    noteOn.OffEvent = noteOff;
                    notesToAdd.Add(noteOn);
                    notesToAdd.Add(noteOff);

                    if (change.NewCentOffset.HasValue && change.NewCentOffset.Value != 0)
                    {
                        string text = $"CENT_OFFSET:{noteOn.Channel},{noteOn.NoteNumber},{change.NewCentOffset.Value}";
                        textEventsToAdd.Add(new TextEvent(text, MetaEventType.TextEvent, noteOn.AbsoluteTime));
                    }
                }
                else
                {
                    var noteOn = midiFile.Events
                        .SelectMany(track => track)
                        .OfType<NoteOnEvent>()
                        .FirstOrDefault(e => e.AbsoluteTime == change.OriginalStartTicks && e.NoteNumber == change.OriginalNoteNumber);

                    if (noteOn != null)
                    {
                        if (change.NewStartTicks.HasValue) noteOn.AbsoluteTime = change.NewStartTicks.Value;
                        if (change.NewDurationTicks.HasValue) noteOn.NoteLength = (int)change.NewDurationTicks.Value;
                        if (change.NewNoteNumber.HasValue) noteOn.NoteNumber = change.NewNoteNumber.Value;
                        if (change.NewVelocity.HasValue) noteOn.Velocity = change.NewVelocity.Value;
                        if (change.NewChannel.HasValue) noteOn.Channel = change.NewChannel.Value;

                        if (noteOn.OffEvent != null)
                        {
                            noteOn.OffEvent.AbsoluteTime = noteOn.AbsoluteTime + noteOn.NoteLength;
                            if (change.NewNoteNumber.HasValue) noteOn.OffEvent.NoteNumber = change.NewNoteNumber.Value;
                            if (change.NewChannel.HasValue) noteOn.OffEvent.Channel = change.NewChannel.Value;
                        }

                        if (change.NewCentOffset.HasValue && change.NewCentOffset.Value != 0)
                        {
                            string text = $"CENT_OFFSET:{noteOn.Channel},{noteOn.NoteNumber},{change.NewCentOffset.Value}";
                            textEventsToAdd.Add(new TextEvent(text, MetaEventType.TextEvent, noteOn.AbsoluteTime));
                        }
                    }
                }
            }

            foreach (var track in midiFile.Events)
            {
                foreach (var toRemove in notesToRemove)
                {
                    track.Remove(toRemove);
                }
            }

            if (midiFile.Events.Tracks == 0) midiFile.Events.AddTrack();
            foreach (var toAdd in notesToAdd)
            {
                midiFile.Events[0].Add(toAdd);
            }
            foreach (var toAdd in textEventsToAdd)
            {
                midiFile.Events[0].Add(toAdd);
            }

            foreach (var track in midiFile.Events)
            {
                var sortedEvents = track.OrderBy(e => e.AbsoluteTime).ToList();
                track.Clear();
                foreach (var e in sortedEvents)
                {
                    track.Add(e);
                }
            }
        }
    }
}