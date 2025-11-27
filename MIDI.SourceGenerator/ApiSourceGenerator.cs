using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MIDI.SourceGenerator
{
    [Generator]
    public class ApiSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
                return;

            var sourceBuilder = new StringBuilder();

            sourceBuilder.AppendLine("#nullable enable");
            sourceBuilder.AppendLine("using System;");
            sourceBuilder.AppendLine("using System.Collections.Generic;");
            sourceBuilder.AppendLine("using System.Threading.Tasks;");
            sourceBuilder.AppendLine("using System.Text.Json.Nodes;");
            sourceBuilder.AppendLine("using MIDI.API.Context;");
            sourceBuilder.AppendLine("using MIDI.API.Commands;");

            sourceBuilder.AppendLine("namespace MIDI.API");
            sourceBuilder.AppendLine("{");
            sourceBuilder.AppendLine("    public static class GeneratedApiDispatcher");
            sourceBuilder.AppendLine("    {");
            sourceBuilder.AppendLine("        public static async Task<object?> DispatchAsync(string commandName, JsonNode? parameters, ApiContext context)");
            sourceBuilder.AppendLine("        {");
            sourceBuilder.AppendLine("            switch (commandName.ToLower())");
            sourceBuilder.AppendLine("            {");

            foreach (var method in receiver.ApiMethods)
            {
                var commandAttr = method.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "ApiCommandAttribute");
                if (commandAttr == null || commandAttr.ConstructorArguments.Length == 0) continue;

                var commandNameObj = commandAttr.ConstructorArguments[0].Value;
                if (commandNameObj == null) continue;

                var commandName = commandNameObj.ToString();
                if (string.IsNullOrEmpty(commandName)) continue;

                var className = method.ContainingType.Name;
                var methodName = method.Name;
                var isAsync = method.ReturnType.Name.StartsWith("Task") || method.ReturnType.ToString().Contains("Task");

                sourceBuilder.AppendLine($"                case \"{commandName!.ToLower()}\":");
                sourceBuilder.AppendLine("                {");
                sourceBuilder.AppendLine($"                    var handler = new {className}(context);");

                var methodParams = method.Parameters;
                var paramCall = new List<string>();

                foreach (var param in methodParams)
                {
                    var paramAttr = param.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "ApiParameterAttribute");

                    string jsonParamName = param.Name;
                    if (paramAttr != null && paramAttr.ConstructorArguments.Length > 0)
                    {
                        var attrVal = paramAttr.ConstructorArguments[0].Value;
                        if (attrVal != null)
                        {
                            jsonParamName = attrVal.ToString();
                        }
                    }

                    var paramType = param.Type.ToDisplayString();
                    var safeParamName = param.Name;

                    sourceBuilder.AppendLine($"                    var {safeParamName}Node = parameters?[\"{jsonParamName}\"];");

                    if (paramType == "string")
                    {
                        sourceBuilder.AppendLine($"                    string {safeParamName} = {safeParamName}Node?.GetValue<string>() ?? string.Empty;");
                    }
                    else if (paramType == "System.Text.Json.Nodes.JsonNode" || paramType.Contains("JsonNode"))
                    {
                        sourceBuilder.AppendLine($"                    JsonNode {safeParamName} = {safeParamName}Node!;");
                    }
                    else if (paramType == "int" || paramType == "System.Int32")
                    {
                        sourceBuilder.AppendLine($"                    int {safeParamName} = {safeParamName}Node?.GetValue<int>() ?? 0;");
                    }
                    else if (paramType == "bool" || paramType == "System.Boolean")
                    {
                        sourceBuilder.AppendLine($"                    bool {safeParamName} = {safeParamName}Node?.GetValue<bool>() ?? false;");
                    }
                    else if (paramType == "double" || paramType == "System.Double")
                    {
                        sourceBuilder.AppendLine($"                    double {safeParamName} = {safeParamName}Node?.GetValue<double>() ?? 0.0;");
                    }
                    else
                    {
                        sourceBuilder.AppendLine($"                    {paramType} {safeParamName} = default!;");
                    }

                    paramCall.Add(safeParamName);
                }

                var awaitKeyword = isAsync ? "await " : "";
                sourceBuilder.AppendLine($"                    return {awaitKeyword}handler.{methodName}({string.Join(", ", paramCall)});");
                sourceBuilder.AppendLine("                }");
            }

            sourceBuilder.AppendLine("                default:");
            sourceBuilder.AppendLine("                    throw new ArgumentException($\"Unknown command: {commandName}\");");
            sourceBuilder.AppendLine("            }");
            sourceBuilder.AppendLine("        }");
            sourceBuilder.AppendLine("    }");
            sourceBuilder.AppendLine("}");

            context.AddSource("GeneratedApiDispatcher.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
        }

        class SyntaxReceiver : ISyntaxContextReceiver
        {
            public List<IMethodSymbol> ApiMethods { get; } = new List<IMethodSymbol>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is MethodDeclarationSyntax methodDeclaration)
                {
                    if (methodDeclaration.AttributeLists.Count > 0)
                    {
                        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;
                        if (methodSymbol != null)
                        {
                            var attributes = methodSymbol.GetAttributes();
                            if (attributes.Any(ad => ad.AttributeClass != null && ad.AttributeClass.Name == "ApiCommandAttribute"))
                            {
                                ApiMethods.Add(methodSymbol);
                            }
                        }
                    }
                }
            }
        }
    }
}