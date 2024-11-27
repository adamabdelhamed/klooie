using Microsoft.CodeAnalysis;
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
    public class ObservableGenerator : ISourceGenerator
    {
        private INamedTypeSymbol iObservableObjectSymbol;
        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {

            var compilation = context.Compilation;

            iObservableObjectSymbol = compilation.GetTypeByMetadataName("klooie.IObservableObject");

            var classes = compilation.SyntaxTrees
                .SelectMany(st => st.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .ToList();

            foreach (var classDeclaration in classes)
            {
                var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

                if (classSymbol == null || !classSymbol.IsPartial()) continue;
                if (!classSymbol.AllInterfaces.Any(i => i.Equals(iObservableObjectSymbol, SymbolEqualityComparer.Default))) continue;

                var source = ProcessClass(classSymbol);
                context.AddSource($"{classSymbol.Name}_{Guid.NewGuid()}_ObservableProperties.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        private string ProcessClass(INamedTypeSymbol classSymbol)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Reflection;");
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

            // Open nested classes
            foreach (var currentClassSymbol in classHierarchy)
            {
                var baseType = currentClassSymbol.BaseType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? string.Empty;
                var interfaces = currentClassSymbol.Interfaces.Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                var typeParameters = currentClassSymbol.TypeParameters.Any()
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


            foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsPartial() && p.IsAutoProperty()))
            {
                var propertyName = property.Name;
                sb.AppendLine($"{Indent(indent)}private Event _{propertyName}Changed;");
                sb.AppendLine($"{Indent(indent)}{property.DeclaredAccessibility.ToString().ToLower()} Event {propertyName}Changed => _{propertyName}Changed ?? (_{propertyName}Changed = Rent());");
            }

            sb.AppendLine();
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
            if (baseIsObservable)
            {
                sb.AppendLine(Indent(indent) + "public override void SubscribeToAnyPropertyChange(Action handler, ILifetimeManager lifetimeManager)");
            }
            else
            {
                sb.AppendLine(Indent(indent) + "public virtual void SubscribeToAnyPropertyChange(Action handler, ILifetimeManager lifetimeManager)");
            }
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;

            if (baseIsObservable)
            {
                sb.AppendLine($"{Indent(indent)}base.SubscribeToAnyPropertyChange(handler, lifetimeManager);");
            }

            foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsPartial() && p.IsAutoProperty()))
            {
                var propertyName = property.Name;
                sb.AppendLine($"{Indent(indent)}{propertyName}Changed.Subscribe(handler, lifetimeManager);");
            }

            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");
            sb.AppendLine();

            if (!baseIsObservable)
            {
                sb.AppendLine(Indent(indent) + "public void SubscribeOld(string propertyName, Action handler, ILifetimeManager lifetimeManager)");
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

                sb.AppendLine(Indent(indent) + "public void SyncOld(string propertyName, Action handler, ILifetimeManager lifetimeManager)");
                sb.AppendLine(Indent(indent) + "{");
                indent += standardIndent;
                sb.AppendLine(Indent(indent) + "SubscribeOld(propertyName, handler, lifetimeManager);");
                sb.AppendLine(Indent(indent) + "handler();");
                indent -= standardIndent;
                sb.AppendLine(Indent(indent) + "}");
                sb.AppendLine();
            }
            sb.AppendLine(Indent(indent) + "private static bool EqualsSafe(object a, object b)");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            sb.AppendLine(Indent(indent) + "if (a == null && b == null) return true;");
            sb.AppendLine(Indent(indent) + "if (a == null ^ b == null) return false;");
            sb.AppendLine(Indent(indent) + "if (ReferenceEquals(a, b)) return true;");
            sb.AppendLine(Indent(indent) + "return a.Equals(b);");
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");


            sb.AppendLine(Indent(indent) + $"private void _ReturnEventsToPool()");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsPartial() && p.IsAutoProperty()))
            {
                var propertyName = property.Name;
                sb.AppendLine($"{Indent(indent)}if(_{propertyName}Changed != null) {{EventPool.Return(_{propertyName}Changed);_{propertyName}Changed = null;}}");
            }
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");
            sb.AppendLine();
            if (classSymbol.AllInterfaces.Any(i => i.Name == "ILifetimeManager"))
            {
                sb.AppendLine(Indent(indent) + $"private bool disposeRegistered = false;");
            }
            sb.AppendLine(Indent(indent) + $"private Event Rent()");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            if (classSymbol.AllInterfaces.Any(i => i.Name == "ILifetimeManager"))
            {
                sb.AppendLine($"{Indent(indent)}if (disposeRegistered == false)");
                sb.AppendLine(Indent(indent) + "{");
                indent += standardIndent;
                    sb.AppendLine($"{Indent(indent)}this.OnDisposed(_ReturnEventsToPool);");
                    sb.AppendLine($"{Indent(indent)}disposeRegistered = true;");
                indent -= standardIndent;
                sb.AppendLine(Indent(indent) + "}");
            }
              
            sb.AppendLine($"{Indent(indent)}return EventPool.Rent();");
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

        private string Indent(int amount) => new string(' ', amount);
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
