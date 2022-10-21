using System.Reflection;
namespace klooie;

public sealed class ToggleGenerator : FormFieldGeneratorAttribute
{
    public override FormElement Generate(PropertyInfo property, IObservableObject formModel)
    {
        var toggle = new ToggleControl();
        if(property.HasAttr<FormToggleAttribute>())
        {
            toggle.OnLabel = property.Attr<FormToggleAttribute>().OnLabel;
            toggle.OffLabel = property.Attr<FormToggleAttribute>().OffLabel;
        }

        SyncToggle(property, toggle, formModel);
        return new FormElement()
        {
            Label = GenerateLabel(property, formModel),
            ValueControl = toggle,
            SupportsDynamicWidth = false,
        };
    }

    public override float GetConfidence(PropertyInfo property, IObservableObject formModel) => property.PropertyType == typeof(bool) ? .75f : 0;

    protected void SyncToggle(PropertyInfo property, ToggleControl toggle, IObservableObject formModel)
    {
        formModel.Sync(property.Name, () => toggle.On = (bool)property.GetValue(formModel), toggle);
        toggle.Subscribe(nameof(toggle.On), () => property.SetValue(formModel,toggle.On), toggle);
    }
}

/// <summary>
/// An attribute that tells the form generator to use particular labels for a toggle
/// </summary>
public sealed class FormToggleAttribute : Attribute 
{
    public string OnLabel { get; private init; }
    public string OffLabel { get; private init; }

    public FormToggleAttribute(string onLabel, string offLabel)
    {
        onLabel = " " + onLabel + " ";
        offLabel = " " + offLabel + " ";
        while (offLabel.Length - onLabel.Length >= 2)
        {
            onLabel = " " + onLabel + " ";
        }

        while (offLabel.Length - onLabel.Length >= 1)
        {
            onLabel = onLabel + " ";
        }

        while (onLabel.Length - offLabel.Length >= 2)
        {
            offLabel = " " + offLabel + " ";
        }

        while (onLabel.Length - offLabel.Length >= 1)
        {
            offLabel = offLabel + " ";
        }

        this.OnLabel = onLabel;
        this.OffLabel = offLabel;
    }
}
