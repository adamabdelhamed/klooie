using System.Reflection;

namespace klooie;

public static class FormGenerator
{
    private static List<FormFieldGeneratorAttribute> generators;

    public static FormOptions FromObject(IObservableObject model, string labelColSpec = null, string valueColSpec = null)
    {
        var ret = new FormOptions();
        ret.LabelColumnSpec = labelColSpec ?? ret.LabelColumnSpec;
        ret.ValueColumnSpec = valueColSpec ?? ret.ValueColumnSpec;

        generators = generators ?? LoadGenerators();
        var properties = model.GetType().GetProperties().Where(p => p.HasAttr<FormIgnoreAttribute>() == false && p.GetSetMethod() != null && p.GetGetMethod() != null).ToList();

        foreach (var property in properties)
        {
            var generator = property.Attr<FormFieldGeneratorAttribute>() ?? AutoDetectGenerator(property, model);
            if(generator == null) throw new Exception($"Unable to generate a form field for property {property.Name} of type {property.PropertyType.Name}");

            var field = generator.Generate(property, model);
            ret.Elements.Add(field);
            if (field.SupportsDynamicWidth == false) continue;
            
            field.ValueControl.Width = property.HasAttr<FormWidth>() ? property.Attr<FormWidth>().Width : 20;
            field.SupportsDynamicWidth = !property.HasAttr<FormWidth>();
        }

        return ret;
    }

    private static FormFieldGeneratorAttribute? AutoDetectGenerator(PropertyInfo property, IObservableObject formModel) => generators
        .Select(g => new { Generator = g, Confidence = g.GetConfidence(property, formModel) })
        .Where(scored => scored.Confidence > 0)
        .OrderByDescending(scored => scored.Confidence)
        .FirstOrDefault()?.Generator;
    

    private static List<FormFieldGeneratorAttribute> LoadGenerators()
    {
        var ret = new List<FormFieldGeneratorAttribute>();
        foreach (var assembly in new Assembly[] { Assembly.GetEntryAssembly(), Assembly.GetExecutingAssembly() })
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(FormFieldGeneratorAttribute))))
            {
                ret.Add(Activator.CreateInstance(type) as FormFieldGeneratorAttribute);
            }
        }
        return ret;
    }
}
