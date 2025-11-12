using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MIDI.Voice.EMEL.Execution;

namespace MIDI.Voice.EMEL.Parsing
{
    public class EmelContext
    {
        private readonly Dictionary<string, object?> variables = new Dictionary<string, object?>();
        public EmelContext? Parent { get; }

        public EmelContext(EmelContext? parent = null)
        {
            this.Parent = parent;

            if (parent == null)
            {
                LoadTimeDefinitions();
                variables["__V"] = 100.0;
                variables["__L"] = 0.0;
                variables["__PB"] = 0.0;
                variables["__M"] = 0.0;
                variables["__E"] = 127.0;
                variables["__P"] = 64.0;
                variables["__CP"] = 0.0;
            }
        }

        private void LoadTimeDefinitions()
        {
            variables["w"] = 4.0;
            variables["h"] = 2.0;
            variables["q"] = 1.0;
            variables["e"] = 0.5;
            variables["s"] = 0.25;
            variables["t"] = 0.125;
        }

        public void LoadBuiltins(Dictionary<string, FunctionDefinition> builtins)
        {
            if (Parent != null)
            {
                Parent.LoadBuiltins(builtins);
                return;
            }

            foreach (var builtin in builtins)
            {
                if (!variables.ContainsKey(builtin.Key))
                {
                    variables.Add(builtin.Key, builtin.Value);
                }
            }
        }

        public bool Define(string name, object? value)
        {
            if (variables.ContainsKey(name))
            {
                variables[name] = value;
                return false;
            }
            variables.Add(name, value);
            return true;
        }

        public bool TryAssign(string name, object? value)
        {
            if (variables.ContainsKey(name))
            {
                variables[name] = value;
                return true;
            }
            if (Parent != null)
            {
                return Parent.TryAssign(name, value);
            }
            return false;
        }

        public object? Get(string name)
        {
            if (variables.TryGetValue(name, out object? value))
            {
                return value;
            }
            if (Parent != null)
            {
                return Parent.Get(name);
            }
            throw new Exception($"Undefined variable '{name}'.");
        }

        public bool TryGet(string name, [MaybeNullWhen(false)] out object? value)
        {
            if (variables.TryGetValue(name, out value))
            {
                return true;
            }
            if (Parent != null)
            {
                return Parent.TryGet(name, out value);
            }
            value = null;
            return false;
        }

        public bool IsDefined(string name)
        {
            if (variables.ContainsKey(name))
            {
                return true;
            }
            if (Parent != null)
            {
                return Parent.IsDefined(name);
            }
            return false;
        }

        public EmelContext CreateScope()
        {
            return new EmelContext(this);
        }
    }
}