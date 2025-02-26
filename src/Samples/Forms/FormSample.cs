﻿//#Sample -Id FormSample
using PowerArgs;
using klooie;
namespace klooie.Samples;

// define a class where each property will map to a form input field
public partial class FormModel : IObservableObject
{
    [FormWidth(25)] // this attribute controls the width of the input control
    [FormLabel("First Name")] // this attribute lets you customize the label
    public partial string FirstName { get; set; }

    [FormWidth(25)]
    [FormLabel("Last Name")]
    public partial string LastName { get; set; }

    [FormReadOnly] // this attribute makes the value display as a label that can't be edited
    public partial string SSN { get; set; }

    [FormToggle("Yes", "No")] // this attribute lets you customize the labels on the toggle control.
    [FormLabel("Loves Klooie?")]
    public partial bool LovesKlooie { get; set; }

    [FormWidth(25)]
    [FormLabel("Favorite Color")]
    public partial ConsoleColor FavoriteColor { get; set; }

    [FormSlider(Min = 0, Max = 125)] // this attribute makes the age input a slider rather than a text box
    [FormWidth(25)]
    public partial short Age { get; set; }

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
//#EndSample

public class FormSampleRunner : IRecordableSample
{
    public string OutputPath => @"Forms\FormSample.gif";
    public int Width => 60;
    public int Height => 25;


    public ConsoleApp Define() => new FormSample();


    internal static async Task SimulateUserInput()
    {
        var form = ConsoleApp.Current.LayoutRoot.Descendents.WhereAs<Form>().Single();
        form.Focus();
        var app = ConsoleApp.Current;
        await app.SendKey(ConsoleKey.A, shift: true);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.D);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.A);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.M);
        await Task.Delay(50);

        await app.SendKey(ConsoleKey.Tab);
        await Task.Delay(350);

        await app.SendKey(ConsoleKey.A, shift: true);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.B);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.D);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.E);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.L);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.H);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.A);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.M);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.E);
        await Task.Delay(50);
        await app.SendKey(ConsoleKey.D);
        await Task.Delay(50);

        await app.SendKey(ConsoleKey.Tab);
        await Task.Delay(350);

        await app.SendKey(ConsoleKey.Tab);
        await Task.Delay(350);
        await app.SendKey(ConsoleKey.Enter);
        await Task.Delay(350);

        for (var i = 0; i < 10; i++)
        {
            await app.SendKey(ConsoleKey.DownArrow);
            await Task.Delay(100);
        }

        await app.SendKey(ConsoleKey.Enter);
        await Task.Delay(350);

        await app.SendKey(ConsoleKey.Tab);
        await Task.Delay(350);

        await app.SendKey(ConsoleKey.LeftArrow);
        await Task.Delay(100);
    }
}