using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MIDI.Shape.MidiPianoRoll.Effects;
using YukkuriMovieMaker.Commons;

namespace MIDI.Shape.MidiPianoRoll.Views
{
    public partial class EffectSelector : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private EffectAddPopup? _effectAddPopup;

        public EffectSelector()
        {
            InitializeComponent();
        }

        public ObservableCollection<EffectParameterBase> ItemsSource
        {
            get { return (ObservableCollection<EffectParameterBase>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(ObservableCollection<EffectParameterBase>),
                typeof(EffectSelector),
                new FrameworkPropertyMetadata(null));

        public EffectParameterBase SelectedItem
        {
            get { return (EffectParameterBase)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(EffectParameterBase),
                typeof(EffectSelector),
                new PropertyMetadata(null));

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            _effectAddPopup = new EffectAddPopup();
            _effectAddPopup.EffectSelected += EffectAddPopup_EffectSelected;

            AddEffectPopupContainer.Child = _effectAddPopup;
            AddEffectPopupContainer.IsOpen = true;
        }

        private void EffectAddPopup_EffectSelected(object? sender, IEffectPlugin plugin)
        {
            _effectAddPopup?.SaveConfig();
            AddEffectPopupContainer.IsOpen = false;

            if (ItemsSource == null)
                ItemsSource = new ObservableCollection<EffectParameterBase>();

            var newEffect = plugin.CreateParameter();
            BeginEdit?.Invoke(this, EventArgs.Empty);
            ItemsSource.Add(newEffect);
            SelectedItem = newEffect;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void AddEffectPopupContainer_Closed(object? sender, EventArgs e)
        {
            _effectAddPopup?.SaveConfig();
            AddEffectPopupContainer.Child = null;
            _effectAddPopup = null;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsSource != null && SelectedItem != null)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                ItemsSource.Remove(SelectedItem);
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }


        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsSource != null && SelectedItem != null)
            {
                int index = ItemsSource.IndexOf(SelectedItem);
                if (index > 0)
                {
                    BeginEdit?.Invoke(this, EventArgs.Empty);
                    ItemsSource.Move(index, index - 1);
                    EndEdit?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsSource != null && SelectedItem != null)
            {
                int index = ItemsSource.IndexOf(SelectedItem);
                if (index < ItemsSource.Count - 1)
                {
                    BeginEdit?.Invoke(this, EventArgs.Empty);
                    ItemsSource.Move(index, index + 1);
                    EndEdit?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            EffectRegistry.ReloadPlugins();
        }
    }
}