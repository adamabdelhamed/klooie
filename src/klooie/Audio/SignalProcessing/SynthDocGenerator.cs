using System.Reflection;
using System.Text;

namespace klooie;

public static class SynthDocGenerator
{
    private record FieldDoc(string Name, string Description);
    private record ParamDoc(string Name, string Description, List<FieldDoc> Fields);
    private record EffectDoc(string Category, string Name, string Description, ParamDoc? Params);

    public static string GenerateMarkdown()
    {
        var effectType = typeof(IEffect);
        var allTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes());

        var effects = new List<EffectDoc>();

        foreach (var type in allTypes)
        {
            if (!effectType.IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                continue;

            var desc = type.GetCustomAttribute<SynthDescriptionAttribute>();
            var cat = type.GetCustomAttribute<SynthCategoryAttribute>();
            if (desc == null || cat == null)
                continue;

            ParamDoc? paramDoc = null;
            var paramType = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(t => t.IsValueType && t.GetCustomAttribute<SynthDescriptionAttribute>() != null);
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

            effects.Add(new EffectDoc(cat.Category, type.Name, desc.Description, paramDoc));
        }

        var sb = new StringBuilder();
        foreach (var group in effects.GroupBy(e => e.Category).OrderBy(g => g.Key))
        {
            sb.AppendLine($"# {group.Key}");
            sb.AppendLine();
            foreach (var effect in group.OrderBy(e => e.Name))
            {
                sb.AppendLine($"## {effect.Name}");
                sb.AppendLine(effect.Description);
                sb.AppendLine();
                if (effect.Params != null)
                {
                    sb.AppendLine($"### {effect.Params.Name}");
                    sb.AppendLine(effect.Params.Description);
                    sb.AppendLine();
                    sb.AppendLine("| Field | Description |");
                    sb.AppendLine("|-------|-------------|");
                    foreach (var field in effect.Params.Fields)
                        sb.AppendLine($"| `{field.Name}` | {field.Description} |");
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }

    public static string GenerateHtml()
    {
        var effectType = typeof(IEffect);
        var allTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes());

        var effects = new List<EffectDoc>();

        foreach (var type in allTypes)
        {
            if (!effectType.IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                continue;

            var desc = type.GetCustomAttribute<SynthDescriptionAttribute>();
            var cat = type.GetCustomAttribute<SynthCategoryAttribute>();
            if (desc == null || cat == null)
                continue;

            ParamDoc? paramDoc = null;
            var paramType = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(t => t.IsValueType && t.GetCustomAttribute<SynthDescriptionAttribute>() != null);
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

            effects.Add(new EffectDoc(cat.Category, type.Name, desc.Description, paramDoc));
        }

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
            width: 280px;
            background: #2b2b3c;
            padding: 1rem;
            box-shadow: 2px 0 8px rgba(0,0,0,0.3);
            overflow-y: auto;
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
            transition: background 0.2s ease;
        }
        .menu-item:hover {
            background: #505070;
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

        foreach (var group in effects.GroupBy(e => e.Category).OrderBy(g => g.Key))
        {
            sb.AppendLine($"<div class=\"menu-group\">");
            sb.AppendLine($"<h2>{group.Key}</h2>");
            foreach (var effect in group.OrderBy(e => e.Name))
            {
                string id = $"{group.Key}_{effect.Name}".Replace(" ", "").Replace(".", "");
                sb.AppendLine($"<div class=\"menu-item\" onclick=\"showSection('{id}')\">{effect.Name}</div>");
            }
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div><div class=\"main\">");

        // HOME SECTION
        sb.AppendLine("""
    <div id="home" class="section active">
        <h1>Welcome to the Synth Effect Documentation</h1>
        <p>This interactive documentation gives you a deep dive into every synth effect available in your system.</p>
        <p>Select a category on the left to explore filters, delays, modulation, distortion, dynamics, and more — all powered by your custom audio engine.</p>
        <p>Each effect includes detailed descriptions and parameter explanations to help you design the perfect patch.</p>
    </div>
    """);

        foreach (var group in effects.GroupBy(e => e.Category).OrderBy(g => g.Key))
        {
            foreach (var effect in group.OrderBy(e => e.Name))
            {
                string id = $"{group.Key}_{effect.Name}".Replace(" ", "").Replace(".", "");

                sb.AppendLine($"<div id=\"{id}\" class=\"section\">");
                sb.AppendLine($"<h2>{effect.Name}</h2>");
                sb.AppendLine($"<p>{effect.Description}</p>");

                if (effect.Params != null)
                {
                    sb.AppendLine($"<h3>{effect.Params.Name}</h3>");
                    sb.AppendLine($"<p>{effect.Params.Description}</p>");
                    sb.AppendLine("<table>");
                    sb.AppendLine("<tr><th>Field</th><th>Description</th></tr>");
                    foreach (var field in effect.Params.Fields)
                    {
                        sb.AppendLine($"<tr><td><code>{field.Name}</code></td><td>{field.Description}</td></tr>");
                    }
                    sb.AppendLine("</table>");
                }

                sb.AppendLine("</div>");
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
