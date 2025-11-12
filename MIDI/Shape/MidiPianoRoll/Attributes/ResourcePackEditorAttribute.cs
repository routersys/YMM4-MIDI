using System;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using MIDI.Shape.MidiPianoRoll.Views;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace MIDI.Shape.MidiPianoRoll.Attributes
{
    internal class ResourcePackEditorAttribute : PropertyEditorAttribute2
    {
        private ItemProperty[]? _itemProperties;
        private EventHandler? _reloadHandler;

        public override FrameworkElement Create()
        {
            return new FileSelector
            {
                Filter = "JSON Files (*.json)|*.json"
            };
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not FileSelector editor) return;
            _itemProperties = itemProperties;

            var resourcePackPathProp = itemProperties.FirstOrDefault(p => p.PropertyInfo.Name == nameof(MidiPianoRollParameter.ResourcePackPath));
            if (resourcePackPathProp != null)
                editor.SetBinding(FileSelector.FilePathProperty, ItemPropertiesBinding.Create2(new[] { resourcePackPathProp }));

            _reloadHandler = (s, e) =>
            {
                if (_itemProperties != null)
                {
                    var paramInstance = _itemProperties.FirstOrDefault()?.PropertyOwner;
                    if (paramInstance is MidiPianoRollParameter param)
                    {
                        _ = param.LoadResourcePackAsync();
                    }
                }
            };
            editor.ReloadRequested += _reloadHandler;
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not FileSelector editor) return;
            BindingOperations.ClearBinding(editor, FileSelector.FilePathProperty);

            if (_reloadHandler != null)
            {
                editor.ReloadRequested -= _reloadHandler;
                _reloadHandler = null;
            }
            _itemProperties = null;
        }
    }
}