# Forms
Klooie has a utility to help you build forms that accept user input.

```cs
using PowerArgs;
using klooie;
namespace klooie.Samples;

// define a class where each property will map to a form input field
public class FormModel : ObservableObject
{
    [FormWidth(25)] // this attribute controls the width of the input control
    [FormLabel("First Name")] // this attribute lets you customize the label
    public string FirstName { get => Get<string>(); set => Set(value); }

    [FormWidth(25)]
    [FormLabel("Last Name")]
    public string LastName { get => Get<string>(); set => Set(value); }

    [FormReadOnly] // this attribute makes the value display as a label that can't be edited
    public string SSN { get => Get<string>(); set => Set(value); }

    [FormToggle("Yes", "No")] // this attribute lets you customize the labels on the toggle control.
    [FormLabel("Loves Klooie?")]
    public bool LovesKlooie { get => Get<bool>(); set => Set(value); }

    [FormWidth(25)]
    [FormLabel("Favorite Color")]
    public ConsoleColor FavoriteColor { get => Get<ConsoleColor>(); set => Set(value); }

    [FormSlider(Min = 0, Max = 125)] // this attribute makes the age input a slider rather than a text box
    [FormWidth(25)]
    public short Age { get => Get<short>(); set => Set(value); }

    [FormIgnore] // this property is ignored and not included in the form
    public string IgnoredProperty { get; set; }

    public FormModel()
    {
        SSN = "123 45 678";
        Age = 37;
        LovesKlooie = true; // of course
    }
}

public class FormSample : ConsoleApp
{
    protected override async Task Startup()
    {
        // create a view model and use it to generate a form
        var formModel = new FormModel();

        // use GridLayout specs to format the label column and value column. "40%" means that the label
        // column takes up 40% of the width. "1r" means that the value column takes one share of the remaining value.
        LayoutRoot.Add(new Form(FormGenerator.FromObject(formModel, "40%","1r"))).Fill(padding: new Thickness(2,0,1,0));
        
        // the user fills out the form
        await FormSampleRunner.SimulateUserInput();
        
        // after the user fills out the form your model object is populated with whatever edits the user made
        var formData = $"Name: {formModel.FirstName} {formModel.LastName}, Love: {formModel.LovesKlooie}, Fav Color: {formModel.FavoriteColor}, Age: {formModel.Age}";
        
        // display some of the inputs to prove that the model was updated
        await MessageDialog.Show(new ShowMessageOptions(formData) { MaxLifetime = Task.Delay(4000).ToLifetime() });
        Stop();
    }
}

// Entry point for your application
public static class FormSampleProgram
{
    public static void Main() => new FormSample().Run();
}

```
The sample above creates an application that looks like this.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Forms/FormSample.gif?raw=true)
