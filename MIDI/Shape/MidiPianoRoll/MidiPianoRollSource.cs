using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MIDI.Shape.MidiPianoRoll.Effects;
using MIDI.Shape.MidiPianoRoll.Models;
using MIDI.Shape.MidiPianoRoll.Rendering;
using NAudio.Midi;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using Color = Vortice.Mathematics.Color;
using Color4 = Vortice.Mathematics.Color4;
using D2D = Vortice.Direct2D1;
using DW = Vortice.DirectWrite;

namespace MIDI.Shape.MidiPianoRoll
{
    public class MidiPianoRollSource : IShapeSource2
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private readonly MidiPianoRollParameter _parameter;
        private readonly DisposeCollector _disposer = new();
        private readonly Queue<ID2D1CommandList> _pendingDisposeQueue = new Queue<ID2D1CommandList>();

        private readonly MidiDataManager _midiDataManager;
        private readonly Direct2DResourceProvider _resourceProvider;
        private readonly KeyRenderer _keyRenderer;
        private readonly PianoRollRenderer _pianoRollRenderer;

        private readonly Dictionary<EffectParameterBase, IEffect> _effectInstances = new();
        private readonly List<EffectParameterBase> _activeParameters = new();

        private string _loadedResourcePackPath = "";
        private bool _resourcesDirty = true;
        private PianoRollOrientation _lastOrientation = PianoRollOrientation.Horizontal;

        private readonly ID2D1Effect _transformEffect;
        private readonly ID2D1Image _outputImage;
        private ID2D1CommandList? _commandList;

        public IEnumerable<VideoController> Controllers { get; } = Enumerable.Empty<VideoController>();
        public ID2D1Image Output => _outputImage;

        public MidiPianoRollSource(IGraphicsDevicesAndContext devices, MidiPianoRollParameter parameter)
        {
            _devices = devices;
            _parameter = parameter;
            _parameter.PropertyChanged += Parameter_PropertyChanged;
            _parameter.Effects.CollectionChanged += Effects_CollectionChanged;

            _midiDataManager = parameter.MidiDataManager;
            _resourceProvider = new Direct2DResourceProvider(devices);
            _disposer.Collect(_resourceProvider);
            _keyRenderer = new KeyRenderer(parameter, _resourceProvider, _midiDataManager);
            _pianoRollRenderer = new PianoRollRenderer(parameter, _resourceProvider, _midiDataManager);

            _transformEffect = (ID2D1Effect)devices.DeviceContext.CreateEffect(EffectGuids.Transform3D);
            _transformEffect.SetInput(0, null, true);
            _outputImage = _transformEffect.Output;
            _disposer.Collect(_transformEffect);
            _disposer.Collect(_outputImage);

            _resourcesDirty = true;
            UpdateEffectInstances();
        }

        private void Effects_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateEffectInstances();
        }

        private void UpdateEffectInstances()
        {
            var parameters = _parameter.Effects;
            if (_activeParameters.SequenceEqual(parameters)) return;

            var newParams = parameters.Except(_activeParameters).ToList();
            var oldParams = _activeParameters.Except(parameters).ToList();

            foreach (var param in oldParams)
            {
                if (_effectInstances.TryGetValue(param, out var effect))
                {
                    effect.Dispose();
                    _effectInstances.Remove(param);
                }
            }

            foreach (var param in newParams)
            {
                var plugin = EffectRegistry.GetPlugin(param.GetType());
                if (plugin != null)
                {
                    var effect = plugin.CreateEffect(_devices);
                    _effectInstances[param] = effect;
                    _disposer.Collect(effect);
                }
            }

            _activeParameters.Clear();
            _activeParameters.AddRange(parameters);
        }

        private void Parameter_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MidiPianoRollParameter.ResourcePackPath))
            {
                _loadedResourcePackPath = "";
            }
            else if (e.PropertyName == nameof(MidiPianoRollParameter.ResourcePack))
            {
                _resourcesDirty = true;
            }
            else if (e.PropertyName == nameof(MidiPianoRollParameter.Orientation))
            {
                _resourcesDirty = true;
            }
        }

        public void Update(TimelineItemSourceDescription desc)
        {
            while (_pendingDisposeQueue.Count > 0)
            {
                _pendingDisposeQueue.Dequeue()?.Dispose();
            }

            UpdateEffectInstances();

            if (_loadedResourcePackPath != _parameter.ResourcePackPath)
            {
                _ = _parameter.LoadResourcePackAsync();
                _loadedResourcePackPath = _parameter.ResourcePackPath;
                _resourcesDirty = true;
            }

            if (_lastOrientation != _parameter.Orientation)
            {
                _lastOrientation = _parameter.Orientation;
                _resourcesDirty = true;
            }

            if (_resourcesDirty)
            {
                _resourceProvider.UpdateResources(_parameter.ResourcePack, _parameter.Orientation);
                _resourcesDirty = false;
            }

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var baseCurrentTime = desc.ItemPosition.Time;
            var timeShift = TimeSpan.FromSeconds(_parameter.TimeShift);
            var playbackSpeed = _parameter.PlaybackSpeed.GetValue(frame, length, fps);
            if (playbackSpeed <= 0) playbackSpeed = 1.0;

            var currentTime = TimeSpan.FromTicks((long)(baseCurrentTime.Ticks * playbackSpeed)) + timeShift;
            if (currentTime < TimeSpan.Zero) currentTime = TimeSpan.Zero;

            var displayDurationSecs = _parameter.DisplayDuration.GetValue(frame, length, fps);
            var displayDuration = TimeSpan.FromSeconds(displayDurationSecs);

            var renderBufferScale = _parameter.RenderBufferScale.GetValue(frame, length, fps);
            var totalRenderDurationSecs = displayDurationSecs * renderBufferScale;
            var extraDurationSecs = totalRenderDurationSecs - displayDurationSecs;
            var bufferBefore = TimeSpan.FromSeconds(extraDurationSecs / 2.0);
            var bufferAfter = TimeSpan.FromSeconds(extraDurationSecs / 2.0);

            var startTime = currentTime;
            var endTime = currentTime + displayDuration;

            var renderStartTime = startTime - bufferBefore;
            var renderEndTime = endTime + bufferAfter;

            var newCommandList = _devices.DeviceContext.CreateCommandList();
            var dc = _devices.DeviceContext;

            float totalWidth = desc.ScreenSize.Width;
            float totalHeight = desc.ScreenSize.Height;

            dc.Target = newCommandList;
            dc.BeginDraw();
            dc.Clear(null);

            float keyAreaSize;
            float rollAreaSize;

            int minNote = (int)_parameter.MinNote.GetValue(frame, length, fps);
            int maxNote = (int)_parameter.MaxNote.GetValue(frame, length, fps);
            int noteRange = maxNote - minNote + 1;
            if (noteRange <= 0) noteRange = 1;

            float keySizeValue;

            if (_parameter.Orientation == PianoRollOrientation.Horizontal)
            {
                keyAreaSize = totalWidth * (float)(_parameter.KeySize.GetValue(frame, length, fps) / 100.0);
                rollAreaSize = totalWidth - keyAreaSize;
                keySizeValue = totalHeight / noteRange;

                _pianoRollRenderer.DrawPianoRollHorizontal(dc, totalWidth, totalHeight, keyAreaSize, rollAreaSize, currentTime, startTime, endTime, displayDuration, minNote, maxNote, keySizeValue, renderStartTime, renderEndTime, frame, length, fps);
                _keyRenderer.DrawKeysHorizontal(dc, totalWidth, totalHeight, keyAreaSize, currentTime, minNote, maxNote, keySizeValue, frame, length, fps);
            }
            else
            {
                keyAreaSize = totalHeight * (float)(_parameter.KeySize.GetValue(frame, length, fps) / 100.0);
                rollAreaSize = totalHeight - keyAreaSize;
                keySizeValue = totalWidth / noteRange;

                _pianoRollRenderer.DrawPianoRollVertical(dc, totalWidth, totalHeight, keyAreaSize, rollAreaSize, currentTime, startTime, endTime, displayDuration, minNote, maxNote, keySizeValue, renderStartTime, renderEndTime, frame, length, fps);
                _keyRenderer.DrawKeysVertical(dc, totalWidth, totalHeight, keyAreaSize, rollAreaSize, currentTime, minNote, maxNote, keySizeValue, frame, length, fps);
            }

            foreach (var param in _activeParameters)
            {
                if (param.IsEnabled && _effectInstances.TryGetValue(param, out var effect))
                {
                    effect.Draw(dc, desc, currentTime, _parameter, _midiDataManager, _resourceProvider, param);
                }
            }

            dc.EndDraw();
            dc.Target = null;
            newCommandList.Close();

            var rotY = (float)(_parameter.PerspectiveYRotation.GetValue(frame, length, fps) * Math.PI / 180.0);
            var rotX = (float)(_parameter.PerspectiveXRotation.GetValue(frame, length, fps) * Math.PI / 180.0);
            var rotZ = (float)(_parameter.PerspectiveZRotation.GetValue(frame, length, fps) * Math.PI / 180.0);
            var depth = (float)_parameter.PerspectiveDepth.GetValue(frame, length, fps);

            var origin = new Vector2(totalWidth / 2, totalHeight / 2);

            var T1 = Matrix4x4.CreateTranslation(-origin.X, -origin.Y, 0);
            var R = Matrix4x4.CreateRotationX(rotX) * Matrix4x4.CreateRotationY(rotY) * Matrix4x4.CreateRotationZ(rotZ);
            var P = Matrix4x4.Identity;
            if (depth != 0)
            {
                P.M34 = -1.0f / depth;
            }
            var T2 = Matrix4x4.CreateTranslation(origin.X, origin.Y, 0);

            var matrixTransform = T1 * R * P * T2;
            var matrixFinalCenter = T1;
            var matrix = matrixTransform * matrixFinalCenter;

            _transformEffect.SetValue((int)Transform3DProperties.TransformMatrix, matrix);
            _transformEffect.SetInput(0, newCommandList, true);

            if (_commandList != null)
            {
                _pendingDisposeQueue.Enqueue(_commandList);
            }
            _commandList = newCommandList;
        }

        public void Dispose()
        {
            _parameter.PropertyChanged -= Parameter_PropertyChanged;
            _parameter.Effects.CollectionChanged -= Effects_CollectionChanged;
            _transformEffect?.SetInput(0, null, true);
            _commandList?.Dispose();
            while (_pendingDisposeQueue.Count > 0)
            {
                _pendingDisposeQueue.Dequeue()?.Dispose();
            }
            _disposer.DisposeAndClear();
            GC.SuppressFinalize(this);
        }
    }
}