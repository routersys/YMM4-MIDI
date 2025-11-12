using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Media;

namespace MIDI.UI.ViewModels.MidiEditor.Settings
{
    public interface ISetting : INotifyPropertyChanged
    {
        string Name { get; }
        string? Description { get; }
    }

    public abstract class SettingViewModel<T> : ViewModelBase, ISetting
    {
        private readonly object _target;
        private readonly PropertyInfo _propertyInfo;

        public string Name { get; }
        public string? Description { get; }

        public T Value
        {
            get => (T)_propertyInfo.GetValue(_target)!;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(Value, value))
                {
                    _propertyInfo.SetValue(_target, value);
                    OnPropertyChanged();
                }
            }
        }

        protected SettingViewModel(object target, PropertyInfo propertyInfo, SettingAttribute attribute)
        {
            _target = target;
            _propertyInfo = propertyInfo;
            Name = attribute.Name;
            Description = attribute.Description;
        }
    }

    public class BoolSettingViewModel : SettingViewModel<bool>
    {
        public BoolSettingViewModel(object target, PropertyInfo propertyInfo, SettingAttribute attribute)
            : base(target, propertyInfo, attribute) { }
    }

    public class StringSettingViewModel : SettingViewModel<string>
    {
        public StringSettingViewModel(object target, PropertyInfo propertyInfo, SettingAttribute attribute)
            : base(target, propertyInfo, attribute) { }
    }

    public class IntSettingViewModel : SettingViewModel<int>
    {
        public IntSettingViewModel(object target, PropertyInfo propertyInfo, SettingAttribute attribute)
            : base(target, propertyInfo, attribute) { }
    }

    public class DoubleSettingViewModel : SettingViewModel<double>
    {
        public DoubleSettingViewModel(object target, PropertyInfo propertyInfo, SettingAttribute attribute)
            : base(target, propertyInfo, attribute) { }
    }

    public class ColorSettingViewModel : SettingViewModel<Color>
    {
        public ColorSettingViewModel(object target, PropertyInfo propertyInfo, SettingAttribute attribute)
            : base(target, propertyInfo, attribute) { }
    }

    public class EnumSettingViewModel : SettingViewModel<Enum>
    {
        public IEnumerable<Enum> EnumValues { get; }

        public EnumSettingViewModel(object target, PropertyInfo propertyInfo, SettingAttribute attribute)
            : base(target, propertyInfo, attribute)
        {
            EnumValues = Enum.GetValues(propertyInfo.PropertyType).Cast<Enum>();
        }
    }
    public class ComboBoxSettingViewModel : SettingViewModel<string>
    {
        public List<string> Options { get; }
        public ComboBoxSettingViewModel(object target, PropertyInfo propertyInfo, SettingAttribute attribute, List<string> options)
            : base(target, propertyInfo, attribute)
        {
            Options = options;
        }
    }
}