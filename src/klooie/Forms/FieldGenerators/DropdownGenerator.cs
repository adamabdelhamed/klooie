using System.Reflection;
namespace klooie;

public sealed class DropdownGenerator : FormFieldGeneratorAttribute
{
    public override FormElement Generate(PropertyInfo property, IObservableObject formModel)
    {
        if(property.HasAttr<FormDropdownProviderAttribute>())
        {
            return CreateFormElementUsingFormDropdownProvider(property, formModel);
        }

        if (property.HasAttr<FormDropdownFromEnumAttribute>())
        {
            return CreateFormElementUsingFormDropdownFromEnumAttribute(property, formModel);
        }

        var dropdown = new Dropdown(Enums.GetEnumValues(property.PropertyType).Select(v => new DialogChoice()
        {
            Id = v.ToString(),
            DisplayText = v.ToString().ToConsoleString(),
            Value = v,
        }));

        SyncDropdown(property, dropdown, formModel);
        return new FormElement()
        {
            Label = GenerateLabel(property, formModel),
            ValueControl = dropdown
        };
    }

    private FormElement CreateFormElementUsingFormDropdownProvider(PropertyInfo property, IObservableObject formModel)
    {
        var dropdown = new Dropdown(property.Attr<FormDropdownProviderAttribute>().GetOptions(formModel));
        SyncDropdown(property, dropdown, formModel);
        return new FormElement()
        {
            Label = GenerateLabel(property, formModel),
            ValueControl = dropdown
        };
    }

    private FormElement CreateFormElementUsingFormDropdownFromEnumAttribute(PropertyInfo property, IObservableObject formModel)
    {
        var dropdown = new Dropdown(property.Attr<FormDropdownFromEnumAttribute>().GetOptions());
        SyncDropdown(property, dropdown, formModel);
        return new FormElement()
        {
            Label = GenerateLabel(property, formModel),
            ValueControl = dropdown
        };
    }

    public override float GetConfidence(PropertyInfo property, IObservableObject formModel)
    {
        if(property.HasAttr<FormDropdownFromEnumAttribute>()) return 1;
        return property.PropertyType.IsEnum ? .75f : 0;
    }

    protected void SyncDropdown(PropertyInfo property, Dropdown dropdown, IObservableObject formModel)
    {
        formModel.SyncOld(property.Name, () =>
        {
            var latestValue = property.GetValue(formModel);
            dropdown.Value = dropdown.Options.Where(o => EqualsSafe(o, latestValue)).Single();
        }, dropdown);
        dropdown.ValueChanged.Subscribe(() => property.SetValue(formModel, dropdown.Value.Value), dropdown);
    }

    private static bool EqualsSafe(DialogChoice c, object v)
    {
        if (c.Value == null && v == null) return true;
        if (c.Value == null || v == null) return false;
        return c.Value.Equals(v);
    }
}
