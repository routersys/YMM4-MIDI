using Microsoft.Win32;
using MIDI.Configuration.Models;
using MIDI.Core;
using MIDI.Core.Audio;
using MIDI.UI.Core;
using MIDI.UI.ViewModels.MidiEditor.Modals;
using MIDI.UI.Views.MidiEditor.Modals;
using NAudio.Midi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NAudioMidi = NAudio.Midi;

namespace MIDI.UI.ViewModels.MidiEditor.Services
{
    public class MidiFileIOService
    {
        private readonly MidiEditorViewModel _vm;
        private CancellationTokenSource _loadCts = new CancellationTokenSource();

        public MidiFileIOService(MidiEditorViewModel vm)
        {
            _vm = vm;
        }

        public void CreateNewFile()
        {
            var newMidiFileWindow = new NewMidiFileWindow
            {
                Owner = Application.Current.MainWindow
            };

            if (newMidiFileWindow.ShowDialog() == true && newMidiFileWindow.ViewModel.ResultMidiEvents != null)
            {
                var vm = newMidiFileWindow.ViewModel;
                var newEvents = vm.ResultMidiEvents;

                const int defaultTempo = 120;
                const int defaultNumerator = 4;
                const int defaultDenominator = 4;

                _vm.TimeSignatureNumerator = defaultNumerator;
                _vm.TimeSignatureDenominator = defaultDenominator;

                long ticksPerBar = (long)(_vm.TimeSignatureNumerator * newEvents.DeltaTicksPerQuarterNote * (4.0 / _vm.TimeSignatureDenominator));
                _vm.LengthInBars = ticksPerBar > 0 ? (int)Math.Ceiling((double)vm.CalculatedTotalTicks / ticksPerBar) : 0;
                if (_vm.LengthInBars == 0) _vm.LengthInBars = 1;

                string tempFile = Path.GetTempFileName();
                try
                {
                    NAudioMidi.MidiFile.Export(tempFile, newEvents);
                    _vm.MidiFile = new NAudioMidi.MidiFile(tempFile, false);
                    _vm.OriginalMidiFile = new NAudioMidi.MidiFile(tempFile, false);
                }
                finally
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                }

                _vm.FilePath = "新しいMIDIファイル.mid";
                _vm.AllNotes.Clear();
                _vm.SelectionManager.ClearSelections();
                _vm.TempoEvents.Clear();
                _vm.ControlChangeEvents.Clear();
                _vm.ProgramChangeEvents.Clear();
                _vm.Flags.Clear();

                var tempoEvents = _vm.MidiFile.Events[0].OfType<NAudioMidi.TempoEvent>().Select(e => new TempoEventViewModel(e)).ToList();
                foreach (var te in tempoEvents)
                {
                    te.PlaybackPropertyChanged += _vm.OnPlaybackPropertyChanged;
                    _vm.TempoEvents.Add(te);
                }

                _vm.Metronome.UpdateMetronome(_vm.TimeSignatureNumerator, _vm.TimeSignatureDenominator, defaultTempo);
                _vm.UpdateTempoStatus();
                _vm.StatusText = "新規ファイル作成完了";

                _vm.UpdatePlaybackMidiData();
                _vm.ViewManager.UpdatePianoRollSize();
                _vm.ViewManager.UpdateTimeRuler();
                _vm.ViewManager.RenderThumbnail();
                _vm.IsMidiFileLoaded = true;
                _vm.RaiseNotesLoaded();
                _vm.RequestRedraw(true);
                _vm.RaiseCanExecuteChanged();
            }
        }

        public void LoadMidiFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "MIDI Files (*.mid;*.midi;*.kar;*.rmi)|*.mid;*.midi;*.kar;*.rmi|All files (*.*)|*.*",
                Title = "MIDIファイルを開く"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _vm.FilePath = openFileDialog.FileName;
                _vm.ProjectPath = string.Empty;
                _vm.IsNewFile = false;
                _vm.LoadingTask = LoadMidiDataAsync();
                _vm.RaiseCanExecuteChanged();
            }
        }

        public async Task LoadMidiDataAsync(string? newPath = null)
        {
            await _loadCts.CancelAsync();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            _vm.PlaybackService.Stop();

            var loadingViewModel = new ProjectLoadingViewModel();
            var loadingWindow = new ProjectLoadingWindow(loadingViewModel)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            loadingWindow.Show();

            try
            {
                var path = newPath ?? _vm.FilePath;
                if (string.IsNullOrEmpty(path) || path == "ファイルが選択されていません")
                {
                    loadingWindow.Close();
                    _vm.IsMidiFileLoaded = false;
                    return;
                }

                var oldDuration = _vm.MaxTime;
                var oldCurrentTime = _vm.CurrentTime;

                var result = await Task.Run(async () =>
                {
                    token.ThrowIfCancellationRequested();

                    var extension = Path.GetExtension(path)?.ToLower();
                    ProjectFile? loadedProject = null;
                    string midiPathToLoad = path;
                    bool isNewProject = false;

                    if (extension == ".ymidi")
                    {
                        loadingViewModel.StatusMessage = "プロジェクトファイルを解析中...";
                        try
                        {
                            loadedProject = await ProjectService.LoadProjectAsync(path);
                        }
                        catch
                        {
                            loadedProject = null;
                        }

                        if (loadedProject != null)
                        {
                            if (loadedProject.IsNewFile)
                            {
                                isNewProject = true;
                                var tempPath = Path.GetTempFileName();
                                var blankMidi = new MidiEventCollection(1, 480);
                                blankMidi.AddTrack();
                                blankMidi[0].Add(new TimeSignatureEvent(0, 4, 2, 24, 8));
                                blankMidi[0].Add(new TempoEvent(500000, 0));
                                blankMidi[0].Add(new MetaEvent(MetaEventType.EndTrack, 0, 0));
                                NAudioMidi.MidiFile.Export(tempPath, blankMidi);
                                midiPathToLoad = tempPath;
                            }
                            else
                            {
                                midiPathToLoad = loadedProject.MidiFilePath;
                                if (!File.Exists(midiPathToLoad))
                                {
                                    bool found = false;
                                    await Application.Current.Dispatcher.InvokeAsync(() => {
                                        var missingFileDialog = new MissingMidiFileDialog(new MissingMidiFileViewModel(midiPathToLoad)) { Owner = Application.Current.MainWindow };
                                        if (missingFileDialog.ShowDialog() == true)
                                        {
                                            midiPathToLoad = missingFileDialog.ViewModel.NewPath;
                                            loadedProject.MidiFilePath = midiPathToLoad;
                                            found = true;
                                        }
                                    });
                                    if (!found) throw new FileNotFoundException("プロジェクトに関連付けられたMIDIファイルが見つかりません。", midiPathToLoad);
                                }
                            }
                        }
                    }

                    if (!File.Exists(midiPathToLoad)) throw new FileNotFoundException("MIDIファイルが見つかりません。", midiPathToLoad);

                    loadingViewModel.StatusMessage = "MIDIデータを読み込み中...";

                    NAudioMidi.MidiFile midiFile;
                    NAudioMidi.MidiFile originalMidi;

                    try
                    {
                        midiFile = new NAudioMidi.MidiFile(midiPathToLoad, false);
                        originalMidi = new NAudioMidi.MidiFile(midiPathToLoad, false);
                    }
                    catch
                    {
                        var tempPath = Path.GetTempFileName();
                        var blankMidi = new MidiEventCollection(1, 480);
                        blankMidi.AddTrack();
                        blankMidi[0].Add(new TimeSignatureEvent(0, 4, 2, 24, 8));
                        blankMidi[0].Add(new TempoEvent(500000, 0));
                        blankMidi[0].Add(new MetaEvent(MetaEventType.EndTrack, 0, 0));
                        NAudioMidi.MidiFile.Export(tempPath, blankMidi);

                        midiFile = new NAudioMidi.MidiFile(tempPath, false);
                        originalMidi = new NAudioMidi.MidiFile(tempPath, false);
                    }

                    if (midiFile.Events == null)
                    {
                        var tempPath = Path.GetTempFileName();
                        var blankMidi = new MidiEventCollection(1, 480);
                        blankMidi.AddTrack();
                        blankMidi[0].Add(new MetaEvent(MetaEventType.EndTrack, 0, 0));
                        NAudioMidi.MidiFile.Export(tempPath, blankMidi);
                        midiFile = new NAudioMidi.MidiFile(tempPath, false);
                        originalMidi = new NAudioMidi.MidiFile(tempPath, false);
                    }

                    if (midiFile.Events.Tracks == 0)
                    {
                        midiFile.Events.AddTrack();
                        midiFile.Events[0].Add(new MetaEvent(MetaEventType.EndTrack, 0, 0));
                    }

                    if (loadedProject != null)
                    {
                        loadingViewModel.StatusMessage = "差分を適用中...";
                        ProjectService.ApplyProjectToMidi(midiFile, loadedProject);

                        ApplyTempoChanges(midiFile, loadedProject.TempoChanges);
                        ApplyControlChanges(midiFile, loadedProject.ControlChangeOperations);
                    }

                    int num = 4;
                    int den = 4;
                    try
                    {
                        if (midiFile.Events.Tracks > 0)
                        {
                            var timeSig = midiFile.Events[0].OfType<NAudioMidi.TimeSignatureEvent>().FirstOrDefault();
                            if (timeSig != null)
                            {
                                num = timeSig.Numerator;
                                den = timeSig.Denominator;
                            }
                        }
                    }
                    catch { }

                    var tempoMap = MidiProcessor.ExtractTempoMap(midiFile, MidiConfiguration.Default);
                    if (tempoMap == null) tempoMap = new List<NAudioMidi.TempoEvent>();

                    var tempoEvent = tempoMap.FirstOrDefault();
                    double tempoValue = tempoEvent != null ? 60000000.0 / tempoEvent.MicrosecondsPerQuarterNote : 120.0;

                    var tempos = new List<TempoEventViewModel>();
                    try
                    {
                        if (midiFile.Events.Tracks > 0)
                            tempos = midiFile.Events[0].OfType<NAudioMidi.TempoEvent>().Select(e => new TempoEventViewModel(e)).ToList();
                    }
                    catch { }

                    var ccs = new List<ControlChangeEventViewModel>();
                    try
                    {
                        if (midiFile.Events != null)
                            ccs = midiFile.Events.SelectMany(track => track).OfType<NAudioMidi.ControlChangeEvent>().Select(e => new ControlChangeEventViewModel(e)).ToList();
                    }
                    catch { }

                    var pcs = new List<ProgramChangeEventViewModel>();
                    try
                    {
                        if (midiFile.Events != null)
                            pcs = midiFile.Events.SelectMany(track => track).OfType<NAudioMidi.PatchChangeEvent>().Select(e => new ProgramChangeEventViewModel(e)).ToList();
                    }
                    catch { }

                    var centOffsetEvents = new Dictionary<(long, int, int), int>();
                    try
                    {
                        if (midiFile.Events != null)
                        {
                            centOffsetEvents = midiFile.Events
                                .SelectMany(track => track.OfType<TextEvent>())
                                .Where(e => e != null && !string.IsNullOrEmpty(e.Text) && e.Text.StartsWith("CENT_OFFSET:"))
                                .ToDictionary(e => {
                                    try
                                    {
                                        var parts = e.Text.Split(':')[1].Split(',');
                                        return (e.AbsoluteTime, e.Channel, int.Parse(parts[1]));
                                    }
                                    catch { return (0L, 0, 0); }
                                }, e => {
                                    try
                                    {
                                        return int.Parse(e.Text.Split(':')[1].Split(',')[2]);
                                    }
                                    catch { return 0; }
                                });
                        }
                    }
                    catch { }

                    var notes = new List<NoteViewModel>();
                    try
                    {
                        if (midiFile.Events != null)
                        {
                            var noteOnEvents = midiFile.Events
                                .SelectMany((track, trackIndex) => track.OfType<NAudioMidi.NoteOnEvent>()
                                .Select(noteOn => new { noteOn, trackIndex }))
                                .Where(item => item.noteOn != null && item.noteOn.OffEvent != null)
                                .OrderBy(item => item.noteOn.AbsoluteTime)
                                .ToList();

                            notes = noteOnEvents.Select(item => {
                                try
                                {
                                    var noteVm = new NoteViewModel(item.noteOn, midiFile.DeltaTicksPerQuarterNote, tempoMap, _vm);
                                    if (centOffsetEvents.TryGetValue((item.noteOn.AbsoluteTime, item.noteOn.Channel, item.noteOn.NoteNumber), out var offset))
                                    {
                                        noteVm.CentOffset = offset;
                                    }
                                    return noteVm;
                                }
                                catch { return null; }
                            }).Where(n => n != null).Select(n => n!).ToList();
                        }
                    }
                    catch { }

                    return (notes, midiFile, originalMidi, num, den, tempoValue, tempos, ccs, pcs, loadedProject, midiPathToLoad, isNewProject);
                }, token);

                if (token.IsCancellationRequested) return;

                _vm.FilePath = path;
                _vm.OriginalMidiFile = result.originalMidi;
                _vm.IsNewFile = result.isNewProject;

                if (result.loadedProject != null)
                {
                    if (result.isNewProject) _vm.FilePath = newPath ?? path;
                    else _vm.FilePath = result.midiPathToLoad;
                    _vm.ProjectPath = newPath ?? path;
                    _vm.RaiseCanExecuteChanged();
                }
                else
                {
                    _vm.ProjectPath = string.Empty;
                    _vm.RaiseCanExecuteChanged();
                }

                _vm.MidiFile = result.midiFile;
                _vm.TimeSignatureNumerator = result.num;
                _vm.TimeSignatureDenominator = result.den;
                _vm.Metronome.UpdateMetronome(_vm.TimeSignatureNumerator, _vm.TimeSignatureDenominator, result.tempoValue);
                _vm.UpdateTempoStatus();
                _vm.StatusText = "準備完了";

                _vm.AllNotes.Clear();
                foreach (var note in result.notes) _vm.AllNotes.Add(note);

                _vm.TempoEvents.Clear();
                foreach (var te in result.tempos)
                {
                    te.PlaybackPropertyChanged += _vm.OnPlaybackPropertyChanged;
                    _vm.TempoEvents.Add(te);
                }

                _vm.ControlChangeEvents.Clear();
                foreach (var cc in result.ccs)
                {
                    cc.PlaybackPropertyChanged += _vm.OnPlaybackPropertyChanged;
                    _vm.ControlChangeEvents.Add(cc);
                }

                _vm.ProgramChangeEvents.Clear();
                foreach (var pc in result.pcs)
                {
                    pc.PlaybackPropertyChanged += _vm.OnPlaybackPropertyChanged;
                    _vm.ProgramChangeEvents.Add(pc);
                }

                _vm.Flags.Clear();
                if (result.loadedProject != null)
                {
                    foreach (var flagOp in result.loadedProject.FlagOperations)
                    {
                        if (flagOp.IsAdded)
                        {
                            _vm.Flags.Add(new FlagViewModel(_vm, flagOp.NewTime, flagOp.NewName ?? ""));
                        }
                    }
                }

                _vm.OnPropertyChanged(nameof(_vm.FileName));
                _vm.ViewManager.UpdatePianoRollSize();
                _vm.OnPropertyChanged(nameof(_vm.MaxTime));
                _vm.ViewManager.UpdateTimeRuler();
                _vm.ViewManager.RenderThumbnail();

                var newDuration = _vm.MaxTime;
                TimeSpan targetTime = oldCurrentTime;
                if (Math.Abs((oldDuration - newDuration).TotalSeconds) > 0.5)
                {
                    targetTime = TimeSpan.Zero;
                }
                _vm.CurrentTime = targetTime;

                _vm.PlaybackService.InitializePlayback(_vm.SelectedSoundFont);
                _vm.UpdatePlaybackMidiData();

                _vm.IsMidiFileLoaded = true;
                _vm.RaiseNotesLoaded();
                _vm.RequestRedraw(true);
                _vm.RaiseCanExecuteChanged();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの読み込み中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                CloseMidiFile();
            }
            finally
            {
                loadingWindow.Close();
            }
        }

        private void ApplyTempoChanges(MidiFile midiFile, List<TempoChange> changes)
        {
            if (midiFile.Events.Tracks == 0) midiFile.Events.AddTrack();
            var tempoTrack = midiFile.Events[0];
            foreach (var change in changes)
            {
                if (change.IsDeleted)
                {
                    var toRemove = tempoTrack.OfType<NAudioMidi.TempoEvent>()
                        .FirstOrDefault(t => t.AbsoluteTime == change.OriginalAbsoluteTime &&
                                             Math.Abs((60000000.0 / t.MicrosecondsPerQuarterNote) - change.OriginalBpm) < 0.01);
                    if (toRemove != null) tempoTrack.Remove(toRemove);
                }
                else if (change.IsAdded)
                {
                    if (change.NewAbsoluteTime.HasValue && change.NewBpm.HasValue)
                    {
                        tempoTrack.Add(new NAudioMidi.TempoEvent((int)(60000000 / change.NewBpm.Value), change.NewAbsoluteTime.Value));
                    }
                }
                else
                {
                    var toUpdate = tempoTrack.OfType<NAudioMidi.TempoEvent>()
                        .FirstOrDefault(t => t.AbsoluteTime == change.OriginalAbsoluteTime &&
                                             Math.Abs((60000000.0 / t.MicrosecondsPerQuarterNote) - change.OriginalBpm) < 0.01);
                    if (toUpdate != null)
                    {
                        if (change.NewAbsoluteTime.HasValue) toUpdate.AbsoluteTime = change.NewAbsoluteTime.Value;
                        if (change.NewBpm.HasValue) toUpdate.MicrosecondsPerQuarterNote = (int)(60000000 / change.NewBpm.Value);
                    }
                }
            }
        }

        private void ApplyControlChanges(MidiFile midiFile, List<ControlChangeOperation> operations)
        {
            foreach (var op in operations)
            {
                if (midiFile.Events.Tracks == 0) midiFile.Events.AddTrack();
                var track = midiFile.Events.Tracks > 1 ? midiFile.Events[1] : midiFile.Events[0];
                if (op.IsDeleted)
                {
                    var toRemove = track.OfType<NAudioMidi.ControlChangeEvent>()
                        .FirstOrDefault(c => c.AbsoluteTime == op.AbsoluteTime && c.Channel == op.Channel && (int)c.Controller == op.Controller && c.ControllerValue == op.OriginalValue);
                    if (toRemove != null) track.Remove(toRemove);
                }
                else if (op.IsAdded)
                {
                    track.Add(new NAudioMidi.ControlChangeEvent(op.AbsoluteTime, op.Channel, (NAudioMidi.MidiController)op.Controller, op.NewValue));
                }
                else
                {
                    var toUpdate = track.OfType<NAudioMidi.ControlChangeEvent>()
                       .FirstOrDefault(c => c.AbsoluteTime == op.AbsoluteTime && c.Channel == op.Channel && (int)c.Controller == op.Controller && c.ControllerValue == op.OriginalValue);
                    if (toUpdate != null)
                    {
                        toUpdate.ControllerValue = op.NewValue;
                    }
                }
            }
        }

        public async Task LoadProjectAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "YukkuriMIDI Project (*.ymidi)|*.ymidi",
                Title = "プロジェクトファイルを開く"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _vm.ProjectPath = openFileDialog.FileName;
                await LoadMidiDataAsync(_vm.ProjectPath);
                _vm.OnPropertyChanged(nameof(_vm.FileName));
            }
        }

        public void CloseMidiFile()
        {
            _vm.PlaybackService.Stop();
            _vm.MidiFile = null;
            _vm.OriginalMidiFile = null;
            _vm.AllNotes.Clear();
            _vm.SelectionManager.ClearSelections();
            _vm.TimeRuler.Clear();
            _vm.Flags.Clear();
            _vm.TempoEvents.Clear();
            _vm.ControlChangeEvents.Clear();
            _vm.ProgramChangeEvents.Clear();
            _vm.FilePath = "ファイルが選択されていません";
            _vm.ProjectPath = string.Empty;
            _vm.IsMidiFileLoaded = false;
            _vm.PianoRollBitmap = null;
            _vm.OnPropertyChanged(nameof(_vm.FileName));
            _vm.RaiseCanExecuteChanged();
        }

        public void SaveFile()
        {
            if (_vm.MidiFile is null || string.IsNullOrEmpty(_vm.FilePath) || _vm.FilePath == "ファイルが選択されていません")
            {
                SaveFileAs();
                return;
            }

            try
            {
                var events = new NAudioMidi.MidiEventCollection(_vm.MidiFile.FileFormat, _vm.MidiFile.DeltaTicksPerQuarterNote);
                for (int i = 0; i < _vm.MidiFile.Tracks; i++)
                {
                    events.AddTrack();
                    foreach (var ev in _vm.MidiFile.Events[i])
                    {
                        try
                        {
                            if (ev is NAudioMidi.NoteOnEvent noteOn && noteOn.OffEvent == null) continue;
                            if (ev is NAudioMidi.TextEvent te && te.Text != null && te.Text.StartsWith("CENT_OFFSET:")) continue;
                            events[i].Add(ev.Clone());
                        }
                        catch { }
                    }
                }

                foreach (var note in _vm.AllNotes)
                {
                    if (note.CentOffset != 0 && note.NoteOnEvent.OffEvent != null)
                    {
                        string text = $"CENT_OFFSET:{note.NoteOnEvent.Channel},{note.NoteNumber},{note.CentOffset}";
                        byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
                        events[0].Add(new NAudioMidi.MetaEvent(NAudioMidi.MetaEventType.TextEvent, data.Length, note.StartTicks));
                    }
                }

                foreach (var track in events)
                {
                    var sortedEvents = track.OrderBy(e => e.AbsoluteTime).ToList();
                    track.Clear();
                    foreach (var e in sortedEvents) track.Add(e);
                }

                NAudioMidi.MidiFile.Export(_vm.FilePath, events);
                _vm.PlaybackService.LoadMidiFile(_vm.FilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの保存中にエラーが発生しました: {ex.Message}");
            }
        }

        public void SaveFileAs()
        {
            if (_vm.MidiFile is null) return;
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "MIDI File (*.mid;*.midi)|*.mid;*.midi",
                FileName = _vm.FileName
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                _vm.FilePath = saveFileDialog.FileName;
                _vm.ProjectPath = string.Empty;
                _vm.RaiseCanExecuteChanged();
                SaveFile();
                _vm.OnPropertyChanged(nameof(_vm.FileName));
            }
        }

        public async Task SaveProjectAsync(string? path = null, bool isBackup = false)
        {
            if (_vm.MidiFile == null || _vm.OriginalMidiFile == null) return;

            string targetPath = path ?? _vm.ProjectPath;

            if (isBackup && string.IsNullOrEmpty(targetPath))
            {
                return;
            }

            if (string.IsNullOrEmpty(targetPath)) return;

            var validNotes = _vm.AllNotes.Where(n => n.NoteOnEvent != null && n.NoteOnEvent.OffEvent != null).ToList();
            var project = await ProjectService.CreateProjectFileAsync(_vm.FilePath, _vm.OriginalMidiFile, _vm.MidiFile, validNotes, _vm.Flags, false, _vm.IsNewFile);

            GenerateTempoChanges(project);
            GenerateControlChangeOperations(project);

            await ProjectService.SaveProjectAsync(project, targetPath, false);
            if (!isBackup)
            {
                _vm.StatusText = "プロジェクトを保存しました。";
            }
        }

        private void GenerateTempoChanges(ProjectFile project)
        {
            var originalTempos = _vm.OriginalMidiFile!.Events[0].OfType<NAudioMidi.TempoEvent>().ToList();
            var currentTempos = _vm.TempoEvents.Select(tvm => tvm.TempoEvent).ToList();

            foreach (var current in currentTempos)
            {
                if (!originalTempos.Any(o => o.AbsoluteTime == current.AbsoluteTime && o.MicrosecondsPerQuarterNote == current.MicrosecondsPerQuarterNote))
                {
                    project.TempoChanges.Add(new TempoChange
                    {
                        IsAdded = true,
                        NewAbsoluteTime = current.AbsoluteTime,
                        NewBpm = 60000000.0 / current.MicrosecondsPerQuarterNote
                    });
                }
            }

            foreach (var original in originalTempos)
            {
                if (!currentTempos.Any(c => c.AbsoluteTime == original.AbsoluteTime && c.MicrosecondsPerQuarterNote == original.MicrosecondsPerQuarterNote))
                {
                    project.TempoChanges.Add(new TempoChange
                    {
                        IsDeleted = true,
                        OriginalAbsoluteTime = original.AbsoluteTime,
                        OriginalBpm = 60000000.0 / original.MicrosecondsPerQuarterNote
                    });
                }
            }
        }

        private void GenerateControlChangeOperations(ProjectFile project)
        {
            var originalCCs = _vm.OriginalMidiFile!.Events.SelectMany(t => t).OfType<NAudioMidi.ControlChangeEvent>().ToList();
            var currentCCs = _vm.ControlChangeEvents.Select(cvm => cvm.ControlChangeEvent).ToList();

            foreach (var current in currentCCs)
            {
                if (!originalCCs.Any(o => o.AbsoluteTime == current.AbsoluteTime && o.Channel == current.Channel && o.Controller == current.Controller && o.ControllerValue == current.ControllerValue))
                {
                    project.ControlChangeOperations.Add(new ControlChangeOperation
                    {
                        IsAdded = true,
                        AbsoluteTime = current.AbsoluteTime,
                        Channel = current.Channel,
                        Controller = (int)current.Controller,
                        NewValue = current.ControllerValue
                    });
                }
            }

            foreach (var original in originalCCs)
            {
                if (!currentCCs.Any(c => c.AbsoluteTime == original.AbsoluteTime && c.Channel == original.Channel && c.Controller == original.Controller && c.ControllerValue == original.ControllerValue))
                {
                    project.ControlChangeOperations.Add(new ControlChangeOperation
                    {
                        IsDeleted = true,
                        AbsoluteTime = original.AbsoluteTime,
                        Channel = original.Channel,
                        Controller = (int)original.Controller,
                        OriginalValue = original.ControllerValue
                    });
                }
            }
        }

        public async Task SaveProjectAsAsync()
        {
            if (_vm.MidiFile == null || _vm.OriginalMidiFile == null) return;

            var saveFileDialog = new ProjectSaveFileDialog(Path.ChangeExtension(_vm.FileName, ".ymidi"));

            if (saveFileDialog.ShowDialog() == true && !string.IsNullOrEmpty(saveFileDialog.FilePath))
            {
                _vm.ProjectPath = saveFileDialog.FilePath;
                var validNotes = _vm.AllNotes.Where(n => n.NoteOnEvent != null && n.NoteOnEvent.OffEvent != null).ToList();
                var project = await ProjectService.CreateProjectFileAsync(_vm.FilePath, _vm.OriginalMidiFile, _vm.MidiFile, validNotes, _vm.Flags, saveFileDialog.SaveAllData, _vm.IsNewFile);

                GenerateTempoChanges(project);
                GenerateControlChangeOperations(project);

                await ProjectService.SaveProjectAsync(project, _vm.ProjectPath, saveFileDialog.CompressProject);
                _vm.StatusText = "プロジェクトを名前を付けて保存しました。";
                _vm.OnPropertyChanged(nameof(_vm.FileName));
                _vm.RaiseCanExecuteChanged();
            }
        }

        public async Task ExportAudioAsync()
        {
            if (_vm.MidiFile == null) return;

            var customSaveDialog = new AudioSaveFileDialog(Path.ChangeExtension(_vm.FileName, ".wav"));

            if (customSaveDialog.ShowDialog() != true) return;

            var filePath = customSaveDialog.FilePath;
            if (string.IsNullOrEmpty(filePath)) return;

            var progressViewModel = new ExportProgressViewModel
            {
                FileName = Path.GetFileName(filePath),
                StatusMessage = "レンダリング準備中...",
                IsIndeterminate = true
            };

            var progressWindow = new ExportProgressWindow(progressViewModel)
            {
                Owner = Application.Current.MainWindow
            };
            progressWindow.Show();

            string tempMidiPath = Path.GetTempFileName();

            try
            {
                var events = new NAudioMidi.MidiEventCollection(_vm.MidiFile.FileFormat, _vm.MidiFile.DeltaTicksPerQuarterNote);
                for (int i = 0; i < _vm.MidiFile.Tracks; i++)
                {
                    events.AddTrack();
                    foreach (var ev in _vm.MidiFile.Events[i])
                    {
                        try
                        {
                            if (ev is NAudioMidi.NoteOnEvent noteOn && noteOn.OffEvent == null) continue;
                            if (ev is NAudioMidi.TextEvent te && te.Text.StartsWith("CENT_OFFSET:")) continue;
                            events[i].Add(ev.Clone());
                        }
                        catch { }
                    }
                }

                foreach (var note in _vm.AllNotes)
                {
                    if (note.NoteOnEvent.OffEvent == null) continue;

                    if (note.CentOffset != 0)
                    {
                        string text = $"CENT_OFFSET:{note.NoteOnEvent.Channel},{note.NoteNumber},{note.CentOffset}";
                        byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
                        events[0].Add(new NAudioMidi.MetaEvent(NAudioMidi.MetaEventType.TextEvent, data.Length, note.StartTicks));
                    }
                }

                foreach (var track in events)
                {
                    var sortedEvents = track.OrderBy(e => e.AbsoluteTime).ToList();
                    track.Clear();
                    foreach (var e in sortedEvents) track.Add(e);
                }

                NAudioMidi.MidiFile.Export(tempMidiPath, events);

                progressViewModel.StatusMessage = "オーディオをレンダリング中...";
                using var audioSource = new MidiAudioSource(tempMidiPath, MidiConfiguration.Default);
                var audioBuffer = await audioSource.ReadAllAsync();

                progressViewModel.IsIndeterminate = false;
                progressViewModel.StatusMessage = "ファイルに書き出し中...";

                if (customSaveDialog.SelectedFormat == "WAV")
                {
                    await AudioExporter.ExportToWavAsync(filePath, audioBuffer, audioSource.Hz, progressViewModel, customSaveDialog.SelectedBitDepth, customSaveDialog.SelectedSampleRate, customSaveDialog.SelectedChannels, customSaveDialog.NormalizationType, customSaveDialog.DitheringType, customSaveDialog.FadeLength, customSaveDialog.PreventClipping, customSaveDialog.TrimSilence);
                }
                else if (customSaveDialog.SelectedFormat == "MP3")
                {
                    await AudioExporter.ExportToMp3Async(filePath, audioBuffer, audioSource.Hz, progressViewModel, customSaveDialog.SelectedBitrate, customSaveDialog.SelectedEncodeQuality, customSaveDialog.SelectedVbrMode, customSaveDialog.Title, customSaveDialog.Artist, customSaveDialog.Album, customSaveDialog.Mp3ChannelMode, customSaveDialog.Mp3LowPassFilter);
                }

                progressViewModel.StatusMessage = "完了";
                progressViewModel.IsComplete = true;
            }
            catch (Exception ex)
            {
                progressViewModel.IsIndeterminate = false;
                progressViewModel.StatusMessage = $"エラー: {ex.Message}";
                progressViewModel.IsComplete = true;
                MessageBox.Show($"書き出し中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (File.Exists(tempMidiPath))
                {
                    try { File.Delete(tempMidiPath); } catch { }
                }
            }
        }
    }
}