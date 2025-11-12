using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using MIDI.AudioEffect.SpatialAudioEffect.Services;

namespace MIDI.AudioEffect.SpatialAudioEffect.UI
{
    internal class IrStatusDisplayAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new IrStatusDisplayControl();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is IrStatusDisplayControl statusControl)
            {
                statusControl.SetItemProperties(itemProperties);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is IrStatusDisplayControl statusControl)
            {
                statusControl.ClearItemProperties();
            }
        }
    }

    internal class IrStatusDisplayControl : UserControl, IPropertyEditorControl
    {
#pragma warning disable 67
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;
#pragma warning restore 67

        private readonly TextBlock statusText;
        private ItemProperty[] itemProperties = [];
        private INotifyPropertyChanged? itemOwner = null;
        private string irFileLeftPath = "";
        private string irFileRightPath = "";
        private readonly IrStatusService statusService;

        public IrStatusDisplayControl()
        {
            statusService = new IrStatusService();
            statusText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4)
            };
            Content = statusText;
            UpdateStatus();
        }

        public void SetItemProperties(ItemProperty[] properties)
        {
            itemProperties = properties;
            if (itemProperties.Length > 0)
            {
                itemOwner = itemProperties[0].PropertyOwner as INotifyPropertyChanged;
                if (itemOwner != null)
                {
                    itemOwner.PropertyChanged += ItemOwner_PropertyChanged;
                }
            }
            UpdateFilePaths();
            UpdateStatus();
        }

        public void ClearItemProperties()
        {
            if (itemOwner != null)
            {
                itemOwner.PropertyChanged -= ItemOwner_PropertyChanged;
            }
            itemOwner = null;
            itemProperties = [];
            UpdateFilePaths();
            UpdateStatus();
        }

        private void ItemOwner_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SpatialAudioEffect.IrFileLeft) ||
                e.PropertyName == nameof(SpatialAudioEffect.IrFileRight))
            {
                UpdateFilePaths();
                UpdateStatus();
            }
        }

        private void UpdateFilePaths()
        {
            if (itemOwner is SpatialAudioEffect owner)
            {
                irFileLeftPath = owner.IrFileLeft ?? "";
                irFileRightPath = owner.IrFileRight ?? "";
            }
            else
            {
                irFileLeftPath = "";
                irFileRightPath = "";
            }
        }

        private void UpdateStatus()
        {
            var status = statusService.ValidateFiles(irFileLeftPath, irFileRightPath);

            statusText.Text = status.Message;

            if (status.IsValid)
            {
                statusText.SetResourceReference(TextBlock.ForegroundProperty, SystemColors.ControlTextBrushKey);
            }
            else
            {
                statusText.SetResourceReference(TextBlock.ForegroundProperty, SystemColors.GrayTextBrushKey);
            }
        }
    }
}