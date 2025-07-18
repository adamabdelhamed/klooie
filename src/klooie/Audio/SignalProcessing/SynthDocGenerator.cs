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
}
