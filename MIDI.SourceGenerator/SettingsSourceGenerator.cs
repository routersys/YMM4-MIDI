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
    public class SettingsSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not SyntaxReceiver receiver)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Collections.ObjectModel;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using MIDI.UI.ViewModels.MidiEditor.Settings;");
            sb.AppendLine("using System.Windows.Media;");

            sb.AppendLine("namespace MIDI.UI.ViewModels.MidiEditor.Settings");
            sb.AppendLine("{");
            sb.AppendLine("    public static class GeneratedSettingsLoader");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void Load(object settingsRoot, ObservableCollection<MajorSettingGroupViewModel> majorGroups)");
            sb.AppendLine("        {");

            foreach (var candidate in receiver.CandidateClasses)
            {
                var semanticModel = context.Compilation.GetSemanticModel(candidate.SyntaxTree);
                if (semanticModel.GetDeclaredSymbol(candidate) is not INamedTypeSymbol typeSymbol) continue;

                var majorAttr = typeSymbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "MajorSettingGroupAttribute");
                if (majorAttr == null) continue;

                var majorGroupName = majorAttr.ConstructorArguments[0].Value?.ToString();

                if (typeSymbol.IsStatic)
                {
                    sb.AppendLine("            {");
                    sb.AppendLine($"                var majorGroup = new MajorSettingGroupViewModel(\"{majorGroupName}\");");

                    var nestedGroupTypes = typeSymbol.GetTypeMembers()
                        .Where(t => t.GetAttributes().Any(ad => ad.AttributeClass?.Name == "SettingGroupAttribute"));

                    foreach (var groupType in nestedGroupTypes)
                    {
                        var groupAttr = groupType.GetAttributes().First(ad => ad.AttributeClass?.Name == "SettingGroupAttribute");
                        var groupName = groupAttr.ConstructorArguments[0].Value?.ToString();

                        sb.AppendLine($"                var group_{groupType.Name} = new SettingGroupViewModel(\"{groupName}\");");

                        sb.AppendLine($"                object target_{groupType.Name} = null!;");

                        var settingProperties = groupType.GetMembers().OfType<IPropertySymbol>()
                            .Where(p => p.GetAttributes().Any(ad => ad.AttributeClass?.Name == "SettingAttribute"));

                        GenerateSettingsForGroup(sb, groupType, groupType.Name, settingProperties);
                    }

                    sb.AppendLine("                if (majorGroup.Groups.Any())");
                    sb.AppendLine("                {");
                    sb.AppendLine("                    majorGroups.Add(majorGroup);");
                    sb.AppendLine("                }");
                    sb.AppendLine("            }");
                }
                else
                {
                    sb.AppendLine($"            if (settingsRoot is {typeSymbol.ToDisplayString()} root_{typeSymbol.Name})");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                var majorGroup = new MajorSettingGroupViewModel(\"{majorGroupName}\");");

                    var groupProperties = typeSymbol.GetMembers().OfType<IPropertySymbol>()
                        .Where(p => p.GetAttributes().Any(ad => ad.AttributeClass?.Name == "SettingGroupAttribute"));

                    foreach (var groupProp in groupProperties)
                    {
                        var groupAttr = groupProp.GetAttributes().First(ad => ad.AttributeClass?.Name == "SettingGroupAttribute");
                        var groupName = groupAttr.ConstructorArguments[0].Value?.ToString();
                        var groupType = groupProp.Type;

                        sb.AppendLine($"                var group_{groupProp.Name} = new SettingGroupViewModel(\"{groupName}\");");
                        sb.AppendLine($"                var target_{groupProp.Name} = root_{typeSymbol.Name}.{groupProp.Name};");
                        sb.AppendLine($"                if (target_{groupProp.Name} != null)");
                        sb.AppendLine("                {");

                        var settingProperties = groupType.GetMembers().OfType<IPropertySymbol>()
                            .Where(p => p.GetAttributes().Any(ad => ad.AttributeClass?.Name == "SettingAttribute"));

                        GenerateSettingsForGroup(sb, groupType, groupProp.Name, settingProperties);

                        sb.AppendLine("                }");
                    }

                    sb.AppendLine("                if (majorGroup.Groups.Any())");
                    sb.AppendLine("                {");
                    sb.AppendLine("                    majorGroups.Add(majorGroup);");
                    sb.AppendLine("                }");
                    sb.AppendLine("            }");
                }
            }

            sb.AppendLine("        }");

            sb.AppendLine("        public static void Reset(object settingsRoot, SettingGroupViewModel groupToReset)");
            sb.AppendLine("        {");

            foreach (var candidate in receiver.CandidateClasses)
            {
                var semanticModel = context.Compilation.GetSemanticModel(candidate.SyntaxTree);
                if (semanticModel.GetDeclaredSymbol(candidate) is not INamedTypeSymbol typeSymbol) continue;

                if (typeSymbol.IsStatic)
                {
                    continue;
                }

                sb.AppendLine($"            if (settingsRoot is {typeSymbol.ToDisplayString()} root_{typeSymbol.Name})");
                sb.AppendLine("            {");

                var groupProperties = typeSymbol.GetMembers().OfType<IPropertySymbol>()
                    .Where(p => p.GetAttributes().Any(ad => ad.AttributeClass?.Name == "SettingGroupAttribute"));

                foreach (var groupProp in groupProperties)
                {
                    var groupAttr = groupProp.GetAttributes().First(ad => ad.AttributeClass?.Name == "SettingGroupAttribute");
                    var groupName = groupAttr.ConstructorArguments[0].Value?.ToString();
                    var groupType = groupProp.Type;

                    var copyFromMethod = groupType.GetMembers("CopyFrom").OfType<IMethodSymbol>().FirstOrDefault();

                    if (copyFromMethod != null)
                    {
                        sb.AppendLine($"                if (groupToReset.Name == \"{groupName}\")");
                        sb.AppendLine("                {");
                        sb.AppendLine($"                    var defaultInstance = new {groupType.ToDisplayString()}();");
                        sb.AppendLine($"                    root_{typeSymbol.Name}.{groupProp.Name}.CopyFrom(defaultInstance);");
                        sb.AppendLine("                    return;");
                        sb.AppendLine("                }");
                    }
                }
                sb.AppendLine("            }");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("GeneratedSettingsLoader.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private void GenerateSettingsForGroup(StringBuilder sb, ITypeSymbol groupType, string groupIdentifier, IEnumerable<IPropertySymbol> settingProperties)
        {
            foreach (var settingProp in settingProperties)
            {
                var settingAttr = settingProp.GetAttributes().First(ad => ad.AttributeClass?.Name == "SettingAttribute");
                var settingName = settingAttr.ConstructorArguments[0].Value?.ToString();

                string? description = null;
                var descArg = settingAttr.NamedArguments.FirstOrDefault(na => na.Key == "Description");
                if (!descArg.Equals(default(KeyValuePair<string, TypedConstant>)))
                {
                    description = descArg.Value.Value?.ToString();
                }

                var propType = settingProp.Type;
                string viewModelCreation = "null";
                string targetVar = $"target_{groupIdentifier}";

                string propertyAccess = $"typeof({groupType.ToDisplayString()}).GetProperty(\"{settingProp.Name}\")!";

                if (settingProp.Name == "GridQuantizeValue")
                {
                    viewModelCreation = $"new ComboBoxSettingViewModel({targetVar}, {propertyAccess}, attr, new List<string> {{ \"1/4\", \"1/8\", \"1/16\", \"1/32\", \"1/4T\", \"1/8T\", \"1/16T\", \"1/32T\" }})";
                }
                else if (propType.SpecialType == SpecialType.System_Boolean)
                {
                    viewModelCreation = $"new BoolSettingViewModel({targetVar}, {propertyAccess}, attr)";
                }
                else if (propType.SpecialType == SpecialType.System_String)
                {
                    viewModelCreation = $"new StringSettingViewModel({targetVar}, {propertyAccess}, attr)";
                }
                else if (propType.SpecialType == SpecialType.System_Int32)
                {
                    viewModelCreation = $"new IntSettingViewModel({targetVar}, {propertyAccess}, attr)";
                }
                else if (propType.SpecialType == SpecialType.System_Double)
                {
                    viewModelCreation = $"new DoubleSettingViewModel({targetVar}, {propertyAccess}, attr)";
                }
                else if (propType.ToDisplayString() == "System.Windows.Media.Color")
                {
                    viewModelCreation = $"new ColorSettingViewModel({targetVar}, {propertyAccess}, attr)";
                }
                else if (propType.TypeKind == TypeKind.Enum)
                {
                    viewModelCreation = $"new EnumSettingViewModel({targetVar}, {propertyAccess}, attr)";
                }

                if (viewModelCreation != "null")
                {
                    sb.AppendLine("                    {");
                    sb.AppendLine($"                        var attr = new SettingAttribute(\"{settingName}\");");
                    if (description != null)
                    {
                        sb.AppendLine($"                        attr.Description = \"{description}\";");
                    }
                    sb.AppendLine($"                        group_{groupIdentifier}.Settings.Add({viewModelCreation});");
                    sb.AppendLine("                    }");
                }
            }

            sb.AppendLine($"                    if (group_{groupIdentifier}.Settings.Any())");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        majorGroup.Groups.Add(group_{groupIdentifier});");
            sb.AppendLine("                    }");
        }

        class SyntaxReceiver : ISyntaxContextReceiver
        {
            public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is ClassDeclarationSyntax classDeclaration &&
                    classDeclaration.AttributeLists.Count > 0)
                {
                    var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
                    if (symbol != null && symbol.GetAttributes().Any(ad => ad.AttributeClass?.Name == "MajorSettingGroupAttribute"))
                    {
                        CandidateClasses.Add(classDeclaration);
                    }
                }
            }
        }
    }
}