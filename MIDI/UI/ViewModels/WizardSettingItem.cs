using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using MIDI.Localization;
using MIDI.UI.ViewModels.MidiEditor;

namespace MIDI.UI.ViewModels
{
    public enum SettingType
    {
        Boolean,
        String,
        Int,
        Double,
        Color,
        Enum,
    }

    public class WizardSettingItem : ViewModelBase, INotifyDataErrorInfo
    {
        private readonly object _target;
        private readonly PropertyInfo _propertyInfo;
        private object _originalValue;

        public string Name { get; }
        public string? Description { get; }
        public string DetailedHelp { get; }
        public SettingType Type { get; }
        public IEnumerable? EnumValues { get; }
        public Type? EnumType { get; }

        private bool _hasErrors;
        private string? _errorMessage;

        public bool HasErrors => _hasErrors;
        public string? ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (SetField(ref _errorMessage, value))
                {
                    _hasErrors = !string.IsNullOrEmpty(value);
                    OnPropertyChanged(nameof(HasErrors));
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Value)));
                }
            }
        }

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public object Value
        {
            get => _propertyInfo.GetValue(_target)!;
            set
            {
                try
                {
                    Validate(value);
                    if (HasErrors) return;

                    var convertedValue = ConvertValue(value, _propertyInfo.PropertyType);

                    if (convertedValue != null && !EqualityComparer<object>.Default.Equals(Value, convertedValue))
                    {
                        _propertyInfo.SetValue(_target, convertedValue);
                        OnPropertyChanged();
                    }
                    else if (convertedValue == null && Value != null)
                    {
                        _propertyInfo.SetValue(_target, null);
                        OnPropertyChanged();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting value for {Name}: {ex.Message}");
                    ErrorMessage = "無効な値です。";
                }
            }
        }

        public WizardSettingItem(object target, PropertyInfo propertyInfo, string name, string? description)
        {
            _target = target;
            _propertyInfo = propertyInfo;
            _originalValue = _propertyInfo.GetValue(target)!;

            Name = name;
            Description = description;

            var helpKey = "Help_" + propertyInfo.Name;
            var helpText = WizardStringResources.GetString(helpKey);

            if (!string.IsNullOrEmpty(helpText))
            {
                DetailedHelp = helpText;
            }
            else
            {
                DetailedHelp = description ?? "";
            }

            var propertyType = propertyInfo.PropertyType;

            if (propertyType == typeof(bool)) Type = SettingType.Boolean;
            else if (propertyType == typeof(string)) Type = SettingType.String;
            else if (propertyType == typeof(int)) Type = SettingType.Int;
            else if (propertyType == typeof(double) || propertyType == typeof(float)) Type = SettingType.Double;
            else if (propertyType == typeof(Color)) Type = SettingType.Color;
            else if (propertyType.IsEnum)
            {
                Type = SettingType.Enum;
                EnumValues = Enum.GetValues(propertyType);
                EnumType = propertyType;
            }
            else Type = SettingType.String;
        }

        public void ResetValue()
        {
            Value = _originalValue;
            ErrorMessage = null;
        }

        public IEnumerable GetErrors(string? propertyName)
        {
            if (propertyName == nameof(Value) && HasErrors)
            {
                yield return ErrorMessage;
            }
        }

        private void Validate(object? value)
        {
            if (value == null) return;

            string strVal = value.ToString() ?? "";

            if (Type == SettingType.Int || Type == SettingType.Double)
            {
                double numVal = 0;
                bool isNum = double.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out numVal);

                if (!isNum && value is IConvertible conv)
                {
                    try { numVal = conv.ToDouble(CultureInfo.InvariantCulture); isNum = true; } catch { }
                }

                if (isNum)
                {
                    if (Name.Contains("Latency") || Name.Contains("Buffer") || Name.Contains("Size") || Name.Contains("Count") || Name.Contains("Rate"))
                    {
                        if (numVal < 0)
                        {
                            ErrorMessage = "0以上の値を入力してください。";
                            return;
                        }
                    }
                    if (Name.Contains("Volume") || Name.Contains("Mix"))
                    {
                        if (numVal < 0)
                        {
                            ErrorMessage = "負の値は指定できません。";
                            return;
                        }
                    }
                }
                else if (Type != SettingType.String)
                {
                    if (string.IsNullOrWhiteSpace(strVal))
                    {
                        ErrorMessage = "値を入力してください。";
                        return;
                    }
                }
            }

            ErrorMessage = null;
        }

        private object? ConvertValue(object? value, Type targetType)
        {
            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            if (targetType == value.GetType()) return value;

            if (targetType == typeof(float) && value is double d_float) return (float)d_float;
            if (targetType == typeof(double) && value is float f_double) return (double)f_double;

            if (value is string s)
            {
                if (targetType == typeof(int) && int.TryParse(s, out int i)) return i;
                if (targetType == typeof(double) && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double dbl)) return dbl;
                if (targetType == typeof(float) && float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out float flt)) return flt;
                if (targetType == typeof(Color))
                {
                    try { return (Color)ColorConverter.ConvertFromString(s); } catch { return Colors.White; }
                }
                if (targetType.IsEnum)
                {
                    try { return Enum.Parse(targetType, s); } catch { return Enum.GetValues(targetType).GetValue(0)!; }
                }
            }

            if (targetType.IsEnum && value != null && targetType.IsInstanceOfType(value))
            {
                return value;
            }

            try
            {
                return System.Convert.ChangeType(value, targetType);
            }
            catch
            {
                if (targetType == typeof(string)) return value?.ToString() ?? "";
                if (targetType == typeof(int)) return 0;
                if (targetType == typeof(double)) return 0.0;
                if (targetType == typeof(float)) return 0.0f;
                if (targetType == typeof(bool)) return false;
                if (targetType == typeof(Color)) return Colors.White;
                if (targetType.IsEnum) return Enum.GetValues(targetType).GetValue(0)!;

                try { return Activator.CreateInstance(targetType); } catch { return null; }
            }
        }
    }
}