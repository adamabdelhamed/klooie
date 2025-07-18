using System.Reflection;
using System.Text;

namespace klooie;

public static class SynthDocGenerator
{
    private record FieldDoc(string Name, string Description);
    private record ParamDoc(string Name, string Description, List<FieldDoc> Fields);
    private record ItemDoc(string Category, string Name, string Description, ParamDoc? Params);
    private record SectionDoc(string Title, List<ItemDoc> Items);

    public static string GenerateHtml()
    {
        var effectType = typeof(IEffect);
        var patchType = typeof(ISynthPatch);
        var allTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes());

        // Helper to collect docs for any base type (IEffect or ISynthPatch)
        static List<ItemDoc> CollectDocs(IEnumerable<Type> types, Type baseType)
        {
            var docs = new List<ItemDoc>();
            foreach (var type in types)
            {
                if (!baseType.IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                    continue;
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

                docs.Add(new ItemDoc(cat.Category, type.Name, desc.Description, paramDoc));
            }
            return docs;
        }

        // Collect all docs
        var effectDocs = CollectDocs(allTypes, effectType);
        var patchDocs = CollectDocs(allTypes, patchType);

        // Group by section (Patches/Effects), then by category
        var sections = new List<SectionDoc>
        {
            new SectionDoc("Patches", patchDocs),
            new SectionDoc("Effects", effectDocs)
        };

        var sb = new StringBuilder();

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
    </style>
</head>
<body>
<div class="sidebar">
    <div class="menu-group">
        <h2>Welcome</h2>
        <div class="menu-item" onclick="showSection('home')">Home</div>
    </div>
""");

        // Sidebar menu for PATCHES and EFFECTS (separated and grouped by category)
        foreach (var section in sections)
        {
            sb.AppendLine($"<div class=\"section-divider\">{section.Title}</div>");
            foreach (var group in section.Items.GroupBy(e => e.Category).OrderBy(g => g.Key))
            {
                sb.AppendLine($"<div class=\"menu-group\">");
                sb.AppendLine($"<h2>{group.Key}</h2>");
                foreach (var item in group.OrderBy(e => e.Name))
                {
                    string id = $"{section.Title}_{group.Key}_{item.Name}".Replace(" ", "").Replace(".", "");
                    sb.AppendLine($"<div class=\"menu-item\" onclick=\"showSection('{id}')\">{item.Name}</div>");
                }
                sb.AppendLine("</div>");
            }
        }

        sb.AppendLine("</div><div class=\"main\">");

        // HOME SECTION
        sb.AppendLine("""
    <div id="home" class="section active">
        <h1>Welcome to the Synth Documentation</h1>
        <p>This interactive documentation gives you a deep dive into every synth effect and patch available in your system.</p>
        <p>Select a category on the left under <b>Patches</b> or <b>Effects</b> to explore your custom audio engine’s palette of sounds and creative tools.</p>
        <p>Each effect and patch includes detailed descriptions and parameter explanations to help you design the perfect patch or effect chain.</p>
    </div>
    """);

        // Patch & Effect content
        foreach (var section in sections)
        {
            foreach (var group in section.Items.GroupBy(e => e.Category).OrderBy(g => g.Key))
            {
                foreach (var item in group.OrderBy(e => e.Name))
                {
                    string id = $"{section.Title}_{group.Key}_{item.Name}".Replace(" ", "").Replace(".", "");
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

                    sb.AppendLine("</div>");
                }
            }
        }

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

        return sb.ToString();
    }
}
