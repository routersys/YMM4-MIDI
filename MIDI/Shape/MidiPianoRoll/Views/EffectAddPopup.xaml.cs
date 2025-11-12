using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MIDI.Shape.MidiPianoRoll.Effects;
using MIDI.Shape.MidiPianoRoll.Models;

namespace MIDI.Shape.MidiPianoRoll.Views
{
    public partial class EffectAddPopup : UserControl
    {
        public event EventHandler<IEffectPlugin>? EffectSelected;
        private readonly EffectAddViewModel _viewModel;
        private Popup? _parentPopup;

        public EffectAddPopup()
        {
            InitializeComponent();
            _viewModel = new EffectAddViewModel();
            DataContext = _viewModel;
        }

        private void EffectListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is EffectPluginViewModel vm)
            {
                EffectSelected?.Invoke(this, vm.Plugin);
            }
        }

        public void SaveConfig()
        {
            PluginConfigManager.EffectAddPopupWidth = this.Width;
            PluginConfigManager.EffectAddPopupHeight = this.Height;
            PluginConfigManager.EffectAddPopupSplitterPosition = GroupColumn.Width.Value;
            PluginConfigManager.Save();
        }

        private void EffectAddPopup_Loaded(object sender, RoutedEventArgs e)
        {
            _parentPopup = this.Parent as Popup;
            PluginConfigManager.Load();
            this.Width = PluginConfigManager.EffectAddPopupWidth;
            this.Height = PluginConfigManager.EffectAddPopupHeight;
            GroupColumn.Width = new GridLength(PluginConfigManager.EffectAddPopupSplitterPosition, GridUnitType.Pixel);
        }

        private void ThumbTopLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = Math.Max(this.MinWidth, this.Width - e.HorizontalChange);
            double newHeight = Math.Max(this.MinHeight, this.Height - e.VerticalChange);
            if (_parentPopup != null)
            {
                _parentPopup.HorizontalOffset += (this.Width - newWidth);
                _parentPopup.VerticalOffset += (this.Height - newHeight);
            }
            this.Width = newWidth;
            this.Height = newHeight;
        }

        private void ThumbTop_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newHeight = Math.Max(this.MinHeight, this.Height - e.VerticalChange);
            if (_parentPopup != null)
            {
                _parentPopup.VerticalOffset += (this.Height - newHeight);
            }
            this.Height = newHeight;
        }

        private void ThumbTopRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            this.Width = Math.Max(this.MinWidth, this.Width + e.HorizontalChange);
            double newHeight = Math.Max(this.MinHeight, this.Height - e.VerticalChange);
            if (_parentPopup != null)
            {
                _parentPopup.VerticalOffset += (this.Height - newHeight);
            }
            this.Height = newHeight;
        }

        private void ThumbLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = Math.Max(this.MinWidth, this.Width - e.HorizontalChange);
            if (_parentPopup != null)
            {
                _parentPopup.HorizontalOffset += (this.Width - newWidth);
            }
            this.Width = newWidth;
        }

        private void ThumbRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            this.Width = Math.Max(this.MinWidth, this.Width + e.HorizontalChange);
        }

        private void ThumbBottomLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = Math.Max(this.MinWidth, this.Width - e.HorizontalChange);
            if (_parentPopup != null)
            {
                _parentPopup.HorizontalOffset += (this.Width - newWidth);
            }
            this.Width = newWidth;
            this.Height = Math.Max(this.MinHeight, this.Height + e.VerticalChange);
        }

        private void ThumbBottom_DragDelta(object sender, DragDeltaEventArgs e)
        {
            this.Height = Math.Max(this.MinHeight, this.Height + e.VerticalChange);
        }

        private void ThumbBottomRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            this.Width = Math.Max(this.MinWidth, this.Width + e.HorizontalChange);
            this.Height = Math.Max(this.MinHeight, this.Height + e.VerticalChange);
        }
    }
}