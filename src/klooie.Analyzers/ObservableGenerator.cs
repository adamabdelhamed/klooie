﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace klooie
{
    [Generator]
    public class ObservableGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find all class declarations
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
                .Where(static c => c != null);

            // Get the compilation
            var compilationProvider = context.CompilationProvider;

            // Combine class declarations with the compilation
            var candidates = classDeclarations
                .Combine(compilationProvider)
                .Select((tuple, _) =>
                {
                    var (classDeclaration, compilation) = tuple;

                    var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                    var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

                    if (classSymbol == null || !classSymbol.IsPartial())
                        return (INamedTypeSymbol?)null;

                    var iObservableObjectSymbol = compilation.GetTypeByMetadataName("klooie.IObservableObject");
                    if (iObservableObjectSymbol == null)
                        return (INamedTypeSymbol?)null;

                    if (!classSymbol.AllInterfaces.Any(i => i.Equals(iObservableObjectSymbol, SymbolEqualityComparer.Default)))
                        return (INamedTypeSymbol?)null;

                    return classSymbol;
                })
                .Where(static s => s != null);

            context.RegisterSourceOutput(candidates, (spc, classSymbol) =>
            {
                if (classSymbol is not INamedTypeSymbol cls)
                    return;
                var iObservableObjectSymbol = cls.AllInterfaces.FirstOrDefault(i => i.Name == "IObservableObject");
                var source = ProcessClass(cls, iObservableObjectSymbol);
                spc.AddSource($"{cls.Name}_{Guid.NewGuid()}_ObservableProperties.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }

        // Replaces the old ProcessClass, now static
        private static string ProcessClass(INamedTypeSymbol classSymbol, INamedTypeSymbol iObservableObjectSymbol)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using klooie;");

            var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace ? null : classSymbol.ContainingNamespace.ToDisplayString();
            var classHierarchy = new Stack<INamedTypeSymbol>();
            var currentType = classSymbol;

            while (currentType != null)
            {
                classHierarchy.Push(currentType);
                currentType = currentType.ContainingType;
            }

            var indent = 0;
            var standardIndent = 4;
            // Open namespace if needed
            if (namespaceName != null)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
                indent += standardIndent;
            }

            string typeParameters = null;
            // Open nested classes
            foreach (var currentClassSymbol in classHierarchy)
            {
                var baseType = currentClassSymbol.BaseType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? string.Empty;
                var interfaces = currentClassSymbol.Interfaces.Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                typeParameters = currentClassSymbol.TypeParameters.Any()
                    ? $"<{string.Join(", ", currentClassSymbol.TypeParameters.Select(tp => tp.Name))}>"
                    : string.Empty;
                var constraints = currentClassSymbol.TypeParameters
                    .Where(tp => tp.ConstraintTypes.Any())
                    .Select(tp =>
                    {
                        var constraintList = string.Join(", ", tp.ConstraintTypes.Select(ct => ct.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                        return $"where {tp.Name} : {constraintList}";
                    });

                // Build base types clause (includes base type and interfaces)
                var baseTypes = new List<string>();
                if (!string.IsNullOrEmpty(baseType) && baseType != "object") baseTypes.Add(baseType);
                baseTypes.AddRange(interfaces);

                var baseTypesClause = baseTypes.Any() ? " : " + string.Join(", ", baseTypes) : string.Empty;

                // Determine modifiers
                var modifiers = new List<string>();
                if (currentClassSymbol.IsAbstract) modifiers.Add("abstract");
                if (currentClassSymbol.IsSealed) modifiers.Add("sealed");

                // Append the class signature
                sb.AppendLine($"{Indent(indent)}{currentClassSymbol.DeclaredAccessibility.ToString().ToLower()} {string.Join(" ", modifiers)} partial class {currentClassSymbol.Name}{typeParameters}{baseTypesClause}");

                // Append constraints, if any
                foreach (var constraint in constraints)
                {
                    sb.AppendLine($"{Indent(indent + standardIndent)}{constraint}");
                }

                sb.AppendLine(Indent(indent) + "{");
                indent += standardIndent;
            }

            // Check if base type is a partial class implementing IObservableObject
            var baseTypeSymbol = classSymbol.BaseType as INamedTypeSymbol;
            var baseIsObservable = baseTypeSymbol != null && baseTypeSymbol.ImplementsObservableInterface(iObservableObjectSymbol);

            // ---- Generate observable property fields/properties ----
            foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsPartial() && p.IsAutoProperty()))
            {
                var propertyName = property.Name;
                var propertyType = property.Type.ToDisplayString();
                var fieldName = $"_{char.ToLower(propertyName[0])}{propertyName.Substring(1)}";
                var accessibility = property.DeclaredAccessibility.ToString().ToLower();
                var getterAccessibility = property.GetMethod?.DeclaredAccessibility.ToString().ToLower();
                var setterAccessibility = property.SetMethod?.DeclaredAccessibility.ToString().ToLower();
                var omitGetterAccessibility = getterAccessibility == accessibility;
                var omitSetterAccessibility = setterAccessibility == accessibility;
                sb.AppendLine($"{Indent(indent)}private Event _{propertyName}Changed;");
                sb.AppendLine($"{Indent(indent)}{property.DeclaredAccessibility.ToString().ToLower()} Event {propertyName}Changed => _{propertyName}Changed ?? (_{propertyName}Changed = Rent());");
                sb.AppendLine($"{Indent(indent)}private {propertyType} {fieldName};");
                sb.AppendLine($"{Indent(indent)}{accessibility} partial {propertyType} {propertyName}");
                sb.AppendLine(Indent(indent) + "{");
                indent += standardIndent;

                // Generate getter
                if (!omitGetterAccessibility && !string.IsNullOrEmpty(getterAccessibility))
                {
                    sb.AppendLine($"{Indent(indent)}{getterAccessibility} get => {fieldName};");
                }
                else
                {
                    sb.AppendLine($"{Indent(indent)}get => {fieldName};");
                }

                // Generate setter
                if (!omitSetterAccessibility && !string.IsNullOrEmpty(setterAccessibility))
                {
                    sb.AppendLine($"{Indent(indent)}{setterAccessibility} set");
                }
                else
                {
                    sb.AppendLine($"{Indent(indent)}set");
                }

                sb.AppendLine(Indent(indent) + "{");
                indent += standardIndent;
                sb.AppendLine($"{Indent(indent)}if (EqualsSafe(value, {fieldName})) return;");
                sb.AppendLine($"{Indent(indent)}{fieldName} = value;");
                sb.AppendLine($"{Indent(indent)}_{propertyName}Changed?.Fire();");
                indent -= standardIndent;
                sb.AppendLine(Indent(indent) + "}");
                indent -= standardIndent;

                sb.AppendLine(Indent(indent) + "}");
                sb.AppendLine();
            }
            sb.AppendLine();

            // ---- Always generate the required interface methods to satisfy IObservableObject ----

            // SubscribeToAnyPropertyChange: virtual if no base, override if base is observable
            sb.AppendLine(Indent(indent) + (baseIsObservable
                ? "public override void SubscribeToAnyPropertyChange(object scope, Action<object> handler, ILifetime lifetimeManager)"
                : "public virtual void SubscribeToAnyPropertyChange(object scope, Action<object> handler, ILifetime lifetimeManager)"));
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;

            if (baseIsObservable)
            {
                sb.AppendLine($"{Indent(indent)}base.SubscribeToAnyPropertyChange(scope, handler, lifetimeManager);");
            }
            foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsPartial() && p.IsAutoProperty()))
            {
                var propertyName = property.Name;
                sb.AppendLine($"{Indent(indent)}{propertyName}Changed.Subscribe(scope, handler, lifetimeManager);");
            }
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");
            sb.AppendLine();

            // SubscribeOld
            sb.AppendLine(Indent(indent) + "public void SubscribeOld(string propertyName, Action handler, ILifetime lifetimeManager)");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            sb.AppendLine(Indent(indent) + "var eventProp = GetType().GetProperty($\"{propertyName}Changed\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);");
            sb.AppendLine(Indent(indent) + "if (eventProp != null)");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            sb.AppendLine(Indent(indent) + "var eventInstance = (Event)eventProp.GetValue(this);");
            sb.AppendLine(Indent(indent) + "eventInstance.Subscribe(handler, lifetimeManager);");
            sb.AppendLine(Indent(indent) + "return;");
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");
            sb.AppendLine();

            // SyncOld
            sb.AppendLine(Indent(indent) + "public void SyncOld(string propertyName, Action handler, ILifetime lifetimeManager)");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            sb.AppendLine(Indent(indent) + "SubscribeOld(propertyName, handler, lifetimeManager);");
            sb.AppendLine(Indent(indent) + "handler();");
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");
            sb.AppendLine();

            // ---- Utility/Pool methods ----
            sb.AppendLine(Indent(indent) + "private static bool EqualsSafe<T>(T a, T b) => EqualityComparer<T>.Default.Equals(a, b);");

            var isLifetimeManager = classSymbol.AllInterfaces.Any(i => i.Name == "ILifetime");
            sb.AppendLine(Indent(indent) + $"private static void _ReturnEventsToPool(object me)");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            sb.AppendLine($"{Indent(indent)}var _this = ({classSymbol.Name}{typeParameters})me;");
            foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsPartial() && p.IsAutoProperty()))
            {
                var propertyName = property.Name;
                sb.AppendLine($"{Indent(indent)}if(_this._{propertyName}Changed != null) {{_this._{propertyName}Changed.Dispose();_this._{propertyName}Changed = null;}}");
            }
            if (isLifetimeManager)
            {
                sb.AppendLine(Indent(indent) + $"_this.disposeRegistered = false;");
            }
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");
            sb.AppendLine();

            if (isLifetimeManager)
            {
                sb.AppendLine(Indent(indent) + $"private bool disposeRegistered = false;");
            }
            sb.AppendLine(Indent(indent) + $"private Event Rent()");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            if (isLifetimeManager)
            {
                sb.AppendLine($"{Indent(indent)}if (disposeRegistered == false)");
                sb.AppendLine(Indent(indent) + "{");
                indent += standardIndent;
                sb.AppendLine($"{Indent(indent)}this.OnDisposed(this, _ReturnEventsToPool);");
                sb.AppendLine($"{Indent(indent)}disposeRegistered = true;");
                indent -= standardIndent;
                sb.AppendLine(Indent(indent) + "}");
            }

            sb.AppendLine($"{Indent(indent)}return Event.Create();");
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");
            sb.AppendLine();

            // Close nested classes
            for (int i = 0; i < classHierarchy.Count; i++)
            {
                indent -= standardIndent;
                sb.AppendLine(Indent(indent) + "}");
            }
            // Close namespace if needed
            if (namespaceName != null)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }


        private static string Indent(int amount) => new string(' ', amount);
    }

    public static class PropertySymbolExtensions
    {
        public static bool ImplementsObservableInterface(this INamedTypeSymbol typeSymbol, INamedTypeSymbol iObservableObjectSymbol)
        {
            if (typeSymbol == null)
                return false;

            // Check if the current type implements the interface
            if (typeSymbol.AllInterfaces.Any(i => i.Equals(iObservableObjectSymbol, SymbolEqualityComparer.Default)))
                return true;

            // Recursively check the base type
            return ImplementsObservableInterface(typeSymbol.BaseType, iObservableObjectSymbol);
        }

        public static bool IsPartial(this IPropertySymbol propertySymbol)
        {
            return propertySymbol.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax() as PropertyDeclarationSyntax)
                .Any(syntax => syntax?.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)) == true);
        }

        public static bool IsPartial(this INamedTypeSymbol propertySymbol)
        {
            return propertySymbol.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax() as ClassDeclarationSyntax)
                .Any(syntax => syntax?.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)) == true);
        }

        public static bool IsAutoProperty(this IPropertySymbol propertySymbol)
        {
            return propertySymbol.GetMethod?.DeclaringSyntaxReferences
                       .All(reference => reference.GetSyntax() is AccessorDeclarationSyntax accessor &&
                                         accessor.Body == null &&
                                         accessor.ExpressionBody == null) == true &&
                   propertySymbol.SetMethod?.DeclaringSyntaxReferences
                       .All(reference => reference.GetSyntax() is AccessorDeclarationSyntax accessor &&
                                         accessor.Body == null &&
                                         accessor.ExpressionBody == null) == true;
        }
    }
}
