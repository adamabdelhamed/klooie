using System.Reflection;
namespace klooie;

public sealed class DropdownGenerator : FormFieldGeneratorAttribute
{
    public override FormElement Generate(PropertyInfo property, IObservableObject formModel)
    {
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

    public override float GetConfidence(PropertyInfo property, IObservableObject formModel) => property.PropertyType.IsEnum ? .75f : 0;

    protected void SyncDropdown(PropertyInfo property, Dropdown dropdown, IObservableObject formModel)
    {
        formModel.SyncOld(property.Name, () =>
        {
            var latestValue = property.GetValue(formModel);
            dropdown.Value = dropdown.Options.Where(o => o.Value.Equals(latestValue)).Single();
        }, dropdown);
        dropdown.ValueChanged.Subscribe(() => property.SetValue(formModel, dropdown.Value.Value), dropdown);
    }
}
