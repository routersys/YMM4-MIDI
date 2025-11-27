using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MIDI.API.Attributes;
using MIDI.API.Context;

namespace MIDI.API
{
    public static class ApiDispatcher
    {
        private static readonly Dictionary<string, MethodInfo> _commandMethods = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);

        static ApiDispatcher()
        {
            DiscoverCommands();
        }

        private static void DiscoverCommands()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<ApiCommandGroupAttribute>() != null);

            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<ApiCommandAttribute>() != null);

                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<ApiCommandAttribute>();
                    if (attr != null)
                    {
                        if (_commandMethods.ContainsKey(attr.CommandName))
                        {
                            continue;
                        }
                        _commandMethods[attr.CommandName] = method;
                    }
                }
            }
        }

        public static async Task<object?> DispatchAsync(string commandName, JsonNode? parameters, ApiContext context)
        {
            if (!_commandMethods.TryGetValue(commandName, out var method))
            {
                throw new ArgumentException($"Unknown command: {commandName}");
            }

            var handlerType = method.DeclaringType;
            if (handlerType == null) throw new InvalidOperationException("Method declaring type is null.");

            object handlerInstance;
            try
            {
                var ctor = handlerType.GetConstructor(new[] { typeof(ApiContext) });
                if (ctor != null)
                {
                    handlerInstance = ctor.Invoke(new object[] { context });
                }
                else
                {
                    handlerInstance = Activator.CreateInstance(handlerType) ?? throw new InvalidOperationException($"Could not create instance of {handlerType.Name}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to instantiate handler {handlerType.Name}: {ex.Message}", ex);
            }

            var methodParams = method.GetParameters();
            var invokeArgs = new object?[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                var param = methodParams[i];
                var paramAttr = param.GetCustomAttribute<ApiParameterAttribute>();
                var jsonParamName = paramAttr?.ParameterName ?? param.Name ?? string.Empty;

                JsonNode? jsonValue = parameters?[jsonParamName];

                if (jsonValue == null)
                {
                    if (paramAttr?.IsOptional == true || param.HasDefaultValue)
                    {
                        invokeArgs[i] = param.HasDefaultValue ? param.DefaultValue : paramAttr?.DefaultValue;
                        continue;
                    }
                    if (param.ParameterType.IsValueType && Nullable.GetUnderlyingType(param.ParameterType) == null)
                    {
                        invokeArgs[i] = Activator.CreateInstance(param.ParameterType);
                    }
                    else
                    {
                        invokeArgs[i] = null;
                    }
                }
                else
                {
                    invokeArgs[i] = ConvertJsonNode(jsonValue, param.ParameterType);
                }
            }

            object? result;
            if (method.ReturnType == typeof(Task) || (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
            {
                var task = (Task?)method.Invoke(handlerInstance, invokeArgs);
                if (task != null)
                {
                    await task.ConfigureAwait(false);
                    if (task.GetType().IsGenericType)
                    {
                        result = ((dynamic)task).Result;
                    }
                    else
                    {
                        result = null;
                    }
                }
                else
                {
                    result = null;
                }
            }
            else
            {
                result = method.Invoke(handlerInstance, invokeArgs);
            }

            return result;
        }

        private static object? ConvertJsonNode(JsonNode node, Type targetType)
        {
            if (targetType == typeof(JsonNode)) return node;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return node.Deserialize(targetType, options);
        }
    }
}