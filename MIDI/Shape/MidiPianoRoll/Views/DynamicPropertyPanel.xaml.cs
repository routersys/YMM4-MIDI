using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MIDI.Shape.MidiPianoRoll.Controls;

namespace MIDI.Shape.MidiPianoRoll.Views
{
    public partial class DynamicPropertyPanel : UserControl
    {
        public DynamicPropertyPanel()
        {
            InitializeComponent();
        }

        public object ItemsSource
        {
            get { return (object)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(object),
                typeof(DynamicPropertyPanel),
                new PropertyMetadata(null, OnItemsSourceChanged));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DynamicPropertyPanel panel)
            {
                panel.RegenerateProperties();
            }
        }

        private void RegenerateProperties()
        {
            PropertyItemsControl.ItemsSource = null;
            if (ItemsSource == null) return;

            var viewModelList = new List<PropertyViewModel>();
            var properties = ItemsSource.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
                var editorAttr = prop.GetCustomAttribute<MidiPropertyEditorAttribute>();

                if (displayAttr != null && editorAttr != null)
                {
                    var viewModel = new PropertyViewModel(ItemsSource, prop, displayAttr, editorAttr);
                    viewModelList.Add(viewModel);
                }
            }

            PropertyItemsControl.ItemsSource = viewModelList;
        }
    }
}