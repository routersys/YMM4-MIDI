using System;

namespace MIDI.API.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ApiCommandGroupAttribute : Attribute
    {
        public string GroupName { get; }
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; } = 0;

        public ApiCommandGroupAttribute(string groupName)
        {
            GroupName = groupName;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ApiCommandAttribute : Attribute
    {
        public string CommandName { get; }
        public string Description { get; set; } = string.Empty;
        public bool IsDeprecated { get; set; } = false;
        public string DeprecationMessage { get; set; } = string.Empty;
        public bool RequiresMainThread { get; set; } = false;

        public ApiCommandAttribute(string commandName)
        {
            CommandName = commandName;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class ApiParameterAttribute : Attribute
    {
        public string ParameterName { get; }
        public bool IsOptional { get; set; } = false;
        public object? DefaultValue { get; set; }
        public string Description { get; set; } = string.Empty;

        public ApiParameterAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }
    }
}