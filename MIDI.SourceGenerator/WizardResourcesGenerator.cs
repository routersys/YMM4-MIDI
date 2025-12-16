using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#pragma warning disable RS1035

namespace MIDI.SourceGenerator
{
    [Generator(LanguageNames.CSharp)]
    public class WizardResourcesGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
                "WizardResourceAttribute.g.cs",
                @"namespace MIDI.Localization
{
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    internal sealed class WizardResourceAttribute : System.Attribute
    {
    }
}"));

            var attributeSymbolProvider = context.CompilationProvider.Select((compilation, token) =>
            {
                return compilation.GetTypeByMetadataName("MIDI.Localization.WizardResourceAttribute");
            });

            var csvProvider = context.AdditionalTextsProvider
                .Where(text => text.Path.EndsWith("WizardResources.csv", StringComparison.OrdinalIgnoreCase))
                .Select((text, token) => text.GetText(token)?.ToString());

            var classProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: (node, token) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: (ctx, token) =>
                {
                    if (ctx.Node is not ClassDeclarationSyntax classDecl) return default;

                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, token);
                    if (symbol is null) return default;

                    if (!classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))) return default;

                    var filePath = ctx.Node.SyntaxTree.FilePath;

                    return (symbol, classDecl.Modifiers, filePath);
                })
                .Where(x => x != default);

            var provider = classProvider
                .Combine(attributeSymbolProvider)
                .Combine(csvProvider.Collect())
                .Select((x, token) =>
                {
                    var (left, csvs) = x;
                    var (classInfo, attributeSymbol) = left;
                    var (classSymbol, modifiers, filePath) = classInfo;

                    string? csvContent = csvs.FirstOrDefault();

                    return (classSymbol, modifiers, attributeSymbol, filePath, csvContent);
                })
                .Where(x => x.classSymbol != null && x.attributeSymbol != null && !string.IsNullOrEmpty(x.csvContent))
                .Where(x => x.classSymbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, x.attributeSymbol)));

            context.RegisterSourceOutput(provider, (ctx, tuple) =>
            {
                var (symbol, modifiers, attributeSymbol, filePath, csvContent) = tuple;

                var records = ParseCsv(csvContent);

                if (!string.IsNullOrEmpty(filePath))
                {
                    var directory = Path.GetDirectoryName(filePath);
                    var resxFileName = $"{symbol.Name}.resx";
                    var resxPath = Path.Combine(directory, resxFileName);

                    try
                    {
                        using (var writer = new ResXResourceWriter(resxPath))
                        {
                            foreach (var kvp in records)
                            {
                                writer.AddResource(kvp.Key, kvp.Value);
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine("#nullable enable");
                sb.AppendLine($"namespace {symbol.ContainingNamespace.ToDisplayString()}");
                sb.AppendLine("{");
                sb.AppendLine($"    internal partial class {symbol.Name}");
                sb.AppendLine("    {");
                sb.AppendLine("        private static global::System.Resources.ResourceManager? _resourceManager;");
                sb.AppendLine("        private static global::System.Globalization.CultureInfo? _resourceCulture;");
                sb.AppendLine();
                sb.AppendLine("        public static global::System.Resources.ResourceManager ResourceManager {");
                sb.AppendLine("            get {");
                sb.AppendLine("                if (object.ReferenceEquals(_resourceManager, null)) {");
                sb.AppendLine($"                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager(\"{symbol.ContainingNamespace.ToDisplayString()}.{symbol.Name}\", typeof({symbol.Name}).Assembly);");
                sb.AppendLine("                    _resourceManager = temp;");
                sb.AppendLine("                }");
                sb.AppendLine("                return _resourceManager;");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        public static global::System.Globalization.CultureInfo? Culture {");
                sb.AppendLine("            get { return _resourceCulture; }");
                sb.AppendLine("            set { _resourceCulture = value; }");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        public static string GetString(string key)");
                sb.AppendLine("        {");
                sb.AppendLine("            return ResourceManager.GetString(key, Culture) ?? \"\";");
                sb.AppendLine("        }");
                sb.AppendLine();

                foreach (var kvp in records)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(kvp.Key, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                    {
                        sb.AppendLine("        /// <summary>");
                        sb.AppendLine($"        ///   {EscapeXml(kvp.Value)}");
                        sb.AppendLine("        /// </summary>");
                        sb.AppendLine($"        public static string {kvp.Key} => GetString(\"{kvp.Key}\");");
                    }
                }

                sb.AppendLine("    }");
                sb.AppendLine("}");

                ctx.AddSource($"{symbol.Name}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            });
        }

        private Dictionary<string, string> ParseCsv(string? csvContent)
        {
            var records = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(csvContent)) return records;

            using (var reader = new StringReader(csvContent))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(new[] { ',' }, 2);
                    if (parts.Length >= 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        if (key.Equals("Key", StringComparison.OrdinalIgnoreCase) && value.Equals("Value", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (key.StartsWith("\"") && key.EndsWith("\"")) key = key.Substring(1, key.Length - 2).Trim();
                        if (value.StartsWith("\"") && value.EndsWith("\"")) value = value.Substring(1, value.Length - 2).Trim();

                        value = value.Replace("\"\"", "\"");
                        records[key] = value;
                    }
                }
            }
            return records;
        }

        private string EscapeXml(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                        .Replace("\"", "&quot;").Replace("'", "&apos;")
                        .Replace("\r", "").Replace("\n", " ");
        }
    }
}