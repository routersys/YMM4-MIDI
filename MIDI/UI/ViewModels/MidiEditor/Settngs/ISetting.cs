using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Media;

namespace MIDI.UI.ViewModels.MidiEditor.Settings
{
    public interface ISetting
    {
        string Name { get; }
        string? Description { get; }
    }

    public abstract class SettingViewModelBase<T> : ViewModelBase, ISetting
    {
        protected readonly object _target;
        protected readonly PropertyInfo _property;
        protected readonly SettingAttribute _attribute;

        public string Name => _attribute.Name;
        public string? Description => _attribute.Description;

        public virtual T Value
        {
            get => (T)_property.GetValue(_target)!;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(Value, value))
                {
                    _property.SetValue(_target, value);
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        protected SettingViewModelBase(object target, PropertyInfo property, SettingAttribute attribute)
        {
            _target = target;
            _property = property;
            _attribute = attribute;
        }
    }

    public class BoolSettingViewModel : SettingViewModelBase<bool>
    {
        public BoolSettingViewModel(object target, PropertyInfo property, SettingAttribute attribute)
            : base(target, property, attribute) { }
    }

    public class IntSettingViewModel : SettingViewModelBase<int>
    {
        public IntSettingViewModel(object target, PropertyInfo property, SettingAttribute attribute)
            : base(target, property, attribute) { }
    }

    public class DoubleSettingViewModel : SettingViewModelBase<double>
    {
        public DoubleSettingViewModel(object target, PropertyInfo property, SettingAttribute attribute)
            : base(target, property, attribute) { }
    }

    public class StringSettingViewModel : SettingViewModelBase<string>
    {
        public StringSettingViewModel(object target, PropertyInfo property, SettingAttribute attribute)
            : base(target, property, attribute) { }
    }

    public class ColorSettingViewModel : SettingViewModelBase<Color>
    {
        public ColorSettingViewModel(object target, PropertyInfo property, SettingAttribute attribute)
            : base(target, property, attribute) { }
    }

    public class EnumValueViewModel
    {
        public object Value { get; }
        public string Description { get; }

        public EnumValueViewModel(object value, string description)
        {
            Value = value;
            Description = description;
        }
    }

    public class EnumSettingViewModel : SettingViewModelBase<object>
    {
        public ObservableCollection<EnumValueViewModel> EnumValues { get; }

        public EnumSettingViewModel(object target, PropertyInfo property, SettingAttribute attribute)
            : base(target, property, attribute)
        {
            EnumValues = new ObservableCollection<EnumValueViewModel>();
            var enumType = property.PropertyType;

            foreach (var value in Enum.GetValues(enumType))
            {
                string description = value.ToString()!;
                var fieldInfo = enumType.GetField(value.ToString()!);
                if (fieldInfo != null)
                {
                    var descAttr = fieldInfo.GetCustomAttribute<DescriptionAttribute>();
                    if (descAttr != null)
                    {
                        description = descAttr.Description;
                    }
                }
                EnumValues.Add(new EnumValueViewModel(value, description));
            }
        }
    }

    public class ComboBoxSettingViewModel : SettingViewModelBase<string>
    {
        public List<string> Items { get; }

        public ComboBoxSettingViewModel(object target, PropertyInfo property, SettingAttribute attribute, List<string> items)
            : base(target, property, attribute)
        {
            Items = items;
        }
    }
}