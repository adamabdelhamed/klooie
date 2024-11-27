using System.Reflection;

namespace klooie.Forms;

public sealed class TextBoxGenerator : FormFieldGeneratorAttribute
{
    public override FormElement Generate(PropertyInfo property, IObservableObject formModel)
    {
        var textBox = new TextBox() { Background = RGB.White, Foreground = RGB.Black };
        textBox.SelectAllOnFocus = true;
        SyncTextBox(property, textBox, formModel);
        return new FormElement()
        {
            Label = GenerateLabel(property, formModel),
            ValueControl = textBox
        };
    }

    public override float GetConfidence(PropertyInfo property, IObservableObject formModel)
    {
        var isStringLike = property.PropertyType == typeof(string) || property.PropertyType == typeof(ConsoleString);
        var canRevive = ArgRevivers.CanRevive(property.PropertyType);
        return isStringLike ? .75f : canRevive ? .5f : 0;
    }

    protected void SyncTextBox(PropertyInfo property, TextBox textBox, IObservableObject formModel)
    {
        var reviver = () =>
        {
            try
            {
                return ArgRevivers.Revive(property.PropertyType, property.Name, "" + textBox.Value);
            }
            catch (Exception ex)
            {
                return property.GetValue(formModel);
            }
        };

        formModel.SyncOld(property.Name, () => textBox.Value = property.GetValue(formModel).ToConsoleString(), textBox);
        textBox.ValueChanged.Subscribe(() => property.SetValue(formModel, reviver()), textBox);
    }
}
