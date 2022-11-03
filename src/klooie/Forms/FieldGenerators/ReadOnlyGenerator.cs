using System.Reflection;

namespace klooie.Forms;

public sealed class ReadOnlyGenerator : FormFieldGeneratorAttribute
{
    public override FormElement Generate(PropertyInfo property, IObservableObject formModel)
    {
        var label = new Label();
        SyncLabel(property, label, formModel);
        return new FormElement()
        {
            Label = GenerateLabel(property, formModel),
            ValueControl = label
        };
    }

    public override float GetConfidence(PropertyInfo property, IObservableObject formModel) => property.HasAttr<FormReadOnlyAttribute>() ? .85f : .1f;

    protected void SyncLabel(PropertyInfo property, Label label, IObservableObject formModel) =>
        formModel.Sync(property.Name, () => label.Text = property.GetValue(formModel).ToConsoleString() , label);
}
