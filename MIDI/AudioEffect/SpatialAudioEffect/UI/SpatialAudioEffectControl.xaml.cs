using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;

namespace MIDI.AudioEffect.SpatialAudioEffect.UI
{
    public partial class SpatialAudioEffectControl : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private ItemProperty[] itemProperties = [];
        private SpatialAudioEffectViewModel? viewModel;
        private readonly DispatcherTimer timer;

        public SpatialAudioEffectControl()
        {
            InitializeComponent();
            timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            timer.Tick += Timer_Tick;
            Loaded += (s, e) => timer.Start();
            Unloaded += (s, e) => timer.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            viewModel?.UpdateLevels();
        }

        public void SetItemProperties(ItemProperty[] properties)
        {
            itemProperties = properties;
            if (itemProperties.Length > 0 && itemProperties[0].PropertyOwner is SpatialAudioEffect owner)
            {
                viewModel = new SpatialAudioEffectViewModel
                {
                    EffectItem = owner
                };
                viewModel.BeginEdit += OnBeginEdit;
                viewModel.EndEdit += OnEndEdit;
                DataContext = viewModel;
            }
        }

        public void ClearItemProperties()
        {
            if (viewModel != null)
            {
                viewModel.BeginEdit -= OnBeginEdit;
                viewModel.EndEdit -= OnEndEdit;
                viewModel.Dispose();
                viewModel = null;
            }
            DataContext = null;
            itemProperties = [];
        }

        private void OnBeginEdit(object? sender, EventArgs e)
        {
            BeginEdit?.Invoke(this, e);
        }

        private void OnEndEdit(object? sender, EventArgs e)
        {
            EndEdit?.Invoke(this, e);
        }

        private void Slider_BeginEdit(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
        }

        private void Slider_EndEdit(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }
}