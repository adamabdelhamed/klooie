using System.Reflection;
using System.Text;

namespace klooie;

public static class SynthDocGenerator
{
    // Records for doc structure
    private record FieldDoc(string Name, string Description);
    private record ParamDoc(string Name, string Description, List<FieldDoc> Fields);
    private record ItemDoc(string Category, string Name, string Description, ParamDoc? Params, List<ExtensionDoc> Extensions);
    private record SectionDoc(string Title, List<ItemDoc> Items);

    // === Public API ===

    public static string GenerateHtml()
    {
        var sections = GetSections();
        var sb = new StringBuilder();

        AppendHtmlHeader(sb);
        AppendHtmlSidebar(sb, sections);
        AppendHtmlMainStart(sb);
        AppendHtmlHomeSection(sb);
        AppendHtmlItemSections(sb, sections);
        AppendHtmlFooter(sb);

        return sb.ToString();
    }

    public static string GenerateMarkdown()
    {
        var sections = GetSections();
        var sb = new StringBuilder();

        sb.AppendLine("# Synth Documentation");
        sb.AppendLine();
        sb.AppendLine("Welcome! This document provides reference information for all synthesizer patches and effects, with detailed descriptions, parameter explanations, and extension method usage examples.");
        sb.AppendLine();

        foreach (var section in sections)
        {
            sb.AppendLine($"## {section.Title}");
            foreach (var group in section.Items.GroupBy(e => e.Category).OrderBy(g => g.Key))
            {
                sb.AppendLine();
                sb.AppendLine($"### {group.Key}");
                foreach (var item in group.OrderBy(e => e.Name))
                {
                    sb.AppendLine();
                    sb.AppendLine($"#### {item.Name}");
                    sb.AppendLine();
                    sb.AppendLine($"{item.Description}");
                    sb.AppendLine();
                    if (item.Params != null)
                    {
                        sb.AppendLine($"##### {item.Params.Name}");
                        sb.AppendLine();
                        sb.AppendLine($"{item.Params.Description}");
                        sb.AppendLine();
                        sb.AppendLine("| Field | Description |");
                        sb.AppendLine("|-------|-------------|");
                        foreach (var field in item.Params.Fields)
                        {
                            sb.AppendLine($"| `{field.Name}` | {field.Description} |");
                        }
                        sb.AppendLine();
                    }
                    // Extension usage
                    if (item.Extensions.Count > 0)
                    {
                        sb.AppendLine("##### Usage (Extension Methods)");
                        sb.AppendLine();
                        foreach (var ext in item.Extensions)
                        {
                            sb.AppendLine($"- **{ext.Name}** ({ext.Kind})");
                            sb.AppendLine();
                            sb.AppendLine("```csharp");
                            sb.AppendLine(ext.MethodSignature);
                            sb.AppendLine("```");
                            if (!string.IsNullOrWhiteSpace(ext.Summary))
                                sb.AppendLine(ext.Summary.Trim() + "\n");
                            if (ext.ParameterDocs.Count > 0)
                            {
                                sb.AppendLine("| Parameter | Mapped Field | Description |");
                                sb.AppendLine("|-----------|--------------|-------------|");
                                foreach (var pd in ext.ParameterDocs)
                                    sb.AppendLine($"| `{pd.param}` | `{pd.mappedField}` | {pd.description} |");
                                sb.AppendLine();
                            }
                        }
                    }
                }
            }
        }

        return sb.ToString();
    }

    // === Data Extraction / Grouping ===

    private static List<SectionDoc> GetSections()
    {
        var effectType = typeof(IEffect);
        var patchType = typeof(ISynthPatch);
        var allTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes());

        // Gather all extensions ONCE, to avoid scanning repeatedly.
        var allExtensionDocs = CollectAllExtensions(allTypes);

        var effectDocs = CollectDocs(allTypes, effectType, allExtensionDocs);
        var patchDocs = CollectDocs(allTypes, patchType, allExtensionDocs);

        return new List<SectionDoc>
        {
            new SectionDoc("Patches", patchDocs),
            new SectionDoc("Effects", effectDocs)
        };
    }

    public static bool IsTypeMatch(Type type, Type baseType)
    {
        if (type.HasAttr<SynthCategoryAttribute>() == false ||
            type.HasAttr<SynthDescriptionAttribute>() == false) return false;
        if(baseType == typeof(ISynthPatch))
        {
            if (type == typeof(ElectricGuitar))
            {

            }
            var isAssignable = baseType.IsAssignableFrom(type);
            var factory = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.ReturnType == typeof(ISynthPatch) && m.GetParameters().Length == 0)
                .FirstOrDefault();
            var isFactory = factory != null && factory.GetParameters().Length == 0;
            var isAcceptableType = isAssignable || isFactory;
            return isAcceptableType;
        }

        return baseType.IsAssignableFrom(type) && type.IsAbstract == false && type.IsInterface == false;
    }

    private static List<ItemDoc> CollectDocs(IEnumerable<Type> types, Type baseType, List<ExtensionDoc> allExtensions)
    {
        var docs = new List<ItemDoc>();
        foreach (var type in types)
        {
            if (!IsTypeMatch(type, baseType)) continue;
            var desc = type.GetCustomAttribute<SynthDescriptionAttribute>();
            var cat = type.GetCustomAttribute<SynthCategoryAttribute>();
            if (desc == null || cat == null)
                continue;

            ParamDoc? paramDoc = null;
            var paramType = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(t => t.IsValueType && t.GetCustomAttribute<SynthDescriptionAttribute>() != null && t.Name.ToLower().Contains("setting"));
            if (paramType != null)
            {
                var pDesc = paramType.GetCustomAttribute<SynthDescriptionAttribute>()!.Description;
                var fields = new List<FieldDoc>();
                foreach (var field in paramType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var fDesc = field.GetCustomAttribute<SynthDescriptionAttribute>()?.Description ?? "No description.";
                    fields.Add(new FieldDoc(field.Name, fDesc));
                }
                paramDoc = new ParamDoc(paramType.Name, pDesc, fields);
            }

            // --- EXTENSION LOGIC ---
            var extensions = allExtensions
                .Where(ext =>
                    // [ExtensionToEffect] or [ExtensionToPatch] explicitly targets this type
                    (ext.TargetType != null && ext.TargetType == type)
                    // [CoreEffect]: include if extension method's first parameter is assignable from this type
                    || (ext.Kind == "Core" && ext.ThisParameterType != null && ext.ThisParameterType.IsAssignableFrom(type))
                )
                .ToList();

            docs.Add(new ItemDoc(cat.Category, type.Name, desc.Description, paramDoc, extensions));
        }
        return docs;
    }

    // Extension scanner
    private static List<ExtensionDoc> CollectAllExtensions(IEnumerable<Type> allTypes)
    {
        var extensions = new List<ExtensionDoc>();

        // Only look for public static methods in static classes
        foreach (var type in allTypes)
        {
            if (!type.IsSealed || !type.IsAbstract) continue; // static class in C#
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                // Only include extension methods with at least one parameter (the 'this')
                if (!method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
                    continue;
                var ps = method.GetParameters();
                if (ps.Length == 0) continue;

                // Detect effect/patch/core attributes
                var extToEffect = method.GetCustomAttribute<ExtensionToEffectAttribute>();
                var extToPatch = method.GetCustomAttribute<ExtensionToPatchAttribute>();
                var core = method.GetCustomAttribute<CoreEffectAttribute>();
                Type? targetType = null;
                string kind = "";
                if (extToEffect != null)
                {
                    targetType = extToEffect.EffectType;
                    kind = "Effect";
                }
                else if (extToPatch != null)
                {
                    targetType = extToPatch.PatchType;
                    kind = "Patch";
                }
                else if (core != null)
                {
                    // Guess by first argument (typically ISynthPatch or similar)
                    if (ps.Length > 0) targetType = ps[0].ParameterType;
                    kind = "Core";
                }
                else
                {
                    continue; // Not a documented extension
                }

                // Get signature as C# code
                var sb = new StringBuilder();
                sb.Append(method.ReturnType.Name + " ");
                sb.Append(method.Name + "(");
                sb.Append(string.Join(", ", ps.Select(p => (p.IsOptional ? p.ParameterType.Name + " " + p.Name + " = " + (p.DefaultValue ?? "null") : p.ParameterType.Name + " " + p.Name))));
                sb.Append(")");

                // Build summary (first XML doc line or attribute, or just a default)
                string summary = method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description
                    ?? method.GetCustomAttribute<SynthDescriptionAttribute>()?.Description
                    ?? "See parameters below.";

                // For each parameter beyond the first (which is the 'this'), try to map to documented fields
                var paramDocs = new List<(string, string, string)>();
                var settingsType = FindSettingsTypeFromExtension(method, targetType);
                var settingsFields = settingsType?.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .ToDictionary(f => f.Name, f => f) ?? new Dictionary<string, FieldInfo>();

                for (int i = 1; i < ps.Length; i++)
                {
                    var p = ps[i];
                    string mapped = "?";
                    string desc = "";
                    // Try to map parameter to field by name (case-insensitive, ignoring common differences)
                    if (settingsFields.TryGetValue(p.Name, out var fi))
                    {
                        mapped = fi.Name;
                        desc = fi.GetCustomAttribute<SynthDescriptionAttribute>()?.Description ?? "";
                    }
                    else
                    {
                        // Try to match by partial name
                        var match = settingsFields.Values.FirstOrDefault(f => string.Equals(f.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            mapped = match.Name;
                            desc = match.GetCustomAttribute<SynthDescriptionAttribute>()?.Description ?? "";
                        }
                    }
                    paramDocs.Add((p.Name, mapped, desc));
                }

                extensions.Add(new ExtensionDoc(
                    Name: targetType?.Name ?? method.Name,
                    MethodSignature: sb.ToString(),
                    Kind: kind,
                    Summary: summary,
                    ParameterDocs: paramDocs
                )
                {
                    TargetType = targetType,
                    ThisParameterType = ps[0].ParameterType // <--- STORE THIS
                });

            }
        }
        return extensions;
    }

    private static Type? FindSettingsTypeFromExtension(MethodInfo method, Type? targetType)
    {
        // Try to find the settings type by inspecting the body or matching naming conventions
        // (e.g., CompressorEffect.Settings, or FooEffect.Settings)
        if (targetType == null) return null;
        var nested = targetType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(t => t.Name == "Settings" || t.Name.EndsWith("Settings"));
        return nested;
    }

    // === HTML helpers ===

    private static void AppendHtmlHeader(StringBuilder sb)
    {
        sb.AppendLine("""
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Epic Synth Documentation</title>
    <style>
        body {
            margin: 0;
            font-family: "Segoe UI", Roboto, sans-serif;
            display: flex;
            height: 100vh;
            overflow: hidden;
            background: #1e1e2f;
            color: #f0f0f0;
        }
        .sidebar {
            width: 300px;
            background: #2b2b3c;
            padding: 1rem;
            box-shadow: 2px 0 8px rgba(0,0,0,0.3);
            overflow-y: auto;
        }
        .section-divider {
            margin: 1.8rem 0 1rem 0;
            font-size: 1.15rem;
            font-weight: 700;
            color: #ffd56b;
            letter-spacing: 0.05em;
        }
        .menu-group {
            margin-bottom: 2rem;
        }
        .menu-group h2 {
            font-size: 1rem;
            margin-bottom: 0.5rem;
            color: #aaa;
            text-transform: uppercase;
        }
        .menu-item {
            display: block;
            background: #3b3b4f;
            margin: 0.3rem 0;
            padding: 0.6rem 1rem;
            border-radius: 6px;
            cursor: pointer;
            transition: background 0.2s, transform 0.25s;
            will-change: background, transform;
        }
        .menu-item:hover {
            background: #505070;
            transform: translateX(7px) scale(1.04);
        }
        .main {
            flex-grow: 1;
            padding: 2rem;
            overflow-y: auto;
            position: relative;
        }
        .section {
            display: none;
            animation: fadeIn 0.4s ease;
        }
        .section.active {
            display: block;
        }
        @keyframes fadeIn {
            from {opacity: 0; transform: translateY(20px);}
            to {opacity: 1; transform: translateY(0);}
        }
        h1, h2, h3 {
            color: #ffd56b;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 1rem;
            margin-bottom: 2rem;
        }
        th, td {
            border: 1px solid #444;
            padding: 0.5rem;
            text-align: left;
        }
        code {
            background: #333;
            padding: 0.2rem 0.4rem;
            border-radius: 4px;
            font-family: monospace;
        }
        #home h1 {
            font-size: 2rem;
        }
        #home p {
            font-size: 1.1rem;
            line-height: 1.5rem;
        }
        .extension-header {
            font-size: 1.08rem;
            font-weight: 600;
            color: #99f;
            margin-top: 1.5rem;
        }
        .signature-block {
            background: #181830;
            padding: 1rem;
            margin: 0.5rem 0 1rem 0;
            border-radius: 8px;
            font-size: 1rem;
            font-family: monospace;
            color: #ffd56b;
            white-space: pre;
        }
    </style>
</head>
<body>
""");
    }

    private static void AppendHtmlSidebar(StringBuilder sb, List<SectionDoc> sections)
    {
        sb.AppendLine("""
<div class="sidebar">
    <div class="menu-group">
        <h2>Welcome</h2>
        <div class="menu-item" onclick="showSection('home')">Home</div>
    </div>
""");

        foreach (var section in sections)
        {
            sb.AppendLine($"<div class=\"section-divider\">{section.Title}</div>");
            foreach (var group in section.Items.GroupBy(e => e.Category).OrderBy(g => g.Key))
            {
                sb.AppendLine($"<div class=\"menu-group\">");
                sb.AppendLine($"<h2>{group.Key}</h2>");
                foreach (var item in group.OrderBy(e => e.Name))
                {
                    string id = GetSectionId(section.Title, group.Key, item.Name);
                    sb.AppendLine($"<div class=\"menu-item\" onclick=\"showSection('{id}')\">{item.Name}</div>");
                }
                sb.AppendLine("</div>");
            }
        }
        sb.AppendLine("</div>");
    }

    private static void AppendHtmlMainStart(StringBuilder sb)
    {
        sb.AppendLine("<div class=\"main\">");
    }

    private static void AppendHtmlHomeSection(StringBuilder sb)
    {
        sb.AppendLine("""
    <div id="home" class="section active">
        <h1>Welcome to the Synth Documentation</h1>
        <p>This interactive documentation gives you a deep dive into every synth effect and patch available in your system.</p>
        <p>Select a category on the left under <b>Patches</b> or <b>Effects</b> to explore your custom audio engine’s palette of sounds and creative tools.</p>
        <p>Each effect and patch includes detailed descriptions, parameter explanations, and C# extension method examples for fast prototyping.</p>
    </div>
    """);
    }

    private static void AppendHtmlItemSections(StringBuilder sb, List<SectionDoc> sections)
    {
        foreach (var section in sections)
        {
            foreach (var group in section.Items.GroupBy(e => e.Category).OrderBy(g => g.Key))
            {
                foreach (var item in group.OrderBy(e => e.Name))
                {
                    string id = GetSectionId(section.Title, group.Key, item.Name);
                    sb.AppendLine($"<div id=\"{id}\" class=\"section\">");
                    sb.AppendLine($"<h2>{item.Name}</h2>");
                    sb.AppendLine($"<p>{item.Description}</p>");

                    if (item.Params != null)
                    {
                        sb.AppendLine($"<h3>{item.Params.Name}</h3>");
                        sb.AppendLine($"<p>{item.Params.Description}</p>");
                        sb.AppendLine("<table>");
                        sb.AppendLine("<tr><th>Field</th><th>Description</th></tr>");
                        foreach (var field in item.Params.Fields)
                        {
                            sb.AppendLine($"<tr><td><code>{field.Name}</code></td><td>{field.Description}</td></tr>");
                        }
                        sb.AppendLine("</table>");
                    }

                    // Extension usage
                    if (item.Extensions.Count > 0)
                    {
                        sb.AppendLine("<div class=\"extension-header\">Usage (Extension Methods)</div>");
                        foreach (var ext in item.Extensions)
                        {
                            sb.AppendLine($"<div><b>{ext.Name}</b> ({ext.Kind})</div>");
                            sb.AppendLine($"<div class=\"signature-block\">{System.Net.WebUtility.HtmlEncode(ext.MethodSignature)}</div>");
                            if (!string.IsNullOrWhiteSpace(ext.Summary))
                                sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(ext.Summary.Trim())}</p>");
                            if (ext.ParameterDocs.Count > 0)
                            {
                                sb.AppendLine("<table><tr><th>Parameter</th><th>Mapped Field</th><th>Description</th></tr>");
                                foreach (var pd in ext.ParameterDocs)
                                    sb.AppendLine($"<tr><td><code>{pd.param}</code></td><td><code>{pd.mappedField}</code></td><td>{System.Net.WebUtility.HtmlEncode(pd.description)}</td></tr>");
                                sb.AppendLine("</table>");
                            }
                        }
                    }

                    sb.AppendLine("</div>");
                }
            }
        }
    }

    private static void AppendHtmlFooter(StringBuilder sb)
    {
        sb.AppendLine("""
    </div>
    <script>
        function showSection(id) {
            document.querySelectorAll('.section').forEach(sec => {
                sec.classList.remove('active');
            });
            document.getElementById(id).classList.add('active');
        }
    </script>
</body>
</html>
""");
    }

    private static string GetSectionId(string sectionTitle, string groupKey, string itemName)
    {
        return $"{sectionTitle}_{groupKey}_{itemName}".Replace(" ", "").Replace(".", "");
    }

    // Needed for per-extension matching
    private class ExtensionDoc : IEquatable<ExtensionDoc>
    {
        public string Name { get; set; }
        public string MethodSignature { get; set; }
        public string Kind { get; set; }
        public string Summary { get; set; }
        public List<(string param, string mappedField, string description)> ParameterDocs { get; set; }
        public Type? TargetType { get; set; }
        public Type? ThisParameterType { get; set; } // Store the type of the 'this' parameter for Core extensions
        public ExtensionDoc(string Name, string MethodSignature, string Kind, string Summary, List<(string param, string mappedField, string description)> ParameterDocs)
        {
            this.Name = Name;
            this.MethodSignature = MethodSignature;
            this.Kind = Kind;
            this.Summary = Summary;
            this.ParameterDocs = ParameterDocs;
        }

        public override bool Equals(object? obj) => Equals(obj as ExtensionDoc);
        public bool Equals(ExtensionDoc? other)
        {
            if (other is null) return false;
            return Name == other.Name && MethodSignature == other.MethodSignature && Kind == other.Kind;
        }
        public override int GetHashCode() => HashCode.Combine(Name, MethodSignature, Kind);
    }
}


public class ExtensionToEffectAttribute : Attribute
{
    public Type EffectType { get; init; }
    public ExtensionToEffectAttribute(Type effectType)
    {
        EffectType = effectType;
    }
}

public class ExtensionToPatchAttribute : Attribute
{
    public Type PatchType { get; init; }
    public ExtensionToPatchAttribute(Type patchType)
    {
        PatchType = patchType;
    }
}

public class CoreEffectAttribute : Attribute
{
    
}