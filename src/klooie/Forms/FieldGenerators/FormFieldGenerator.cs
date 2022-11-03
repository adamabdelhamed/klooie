using System.Reflection;

namespace klooie;
[AttributeUsage(AttributeTargets.Property)]
public abstract class FormFieldGeneratorAttribute : Attribute
{
    public abstract float GetConfidence(PropertyInfo property, IObservableObject formModel);
    public abstract FormElement Generate(PropertyInfo property, IObservableObject formModel);
    public ConsoleString GenerateLabel(PropertyInfo property, IObservableObject formModel) => property.HasAttr<FormLabelAttribute>() ? ConsoleString.Parse(property.Attr<FormLabelAttribute>().Label) : property.Name.ToConsoleString();

}