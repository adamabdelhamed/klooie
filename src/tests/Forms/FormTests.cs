﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Slow)]
public partial class FormTests
{
    public TestContext TestContext { get; set; }

    partial class SimpleFormModel : IObservableObject
    {
        [FormLabel("[Orange]First Name")]
        public partial string FirstName { get; set; }

        public partial string LastName { get; set; }

        [FormReadOnly]
        public partial string SSN { get; set; }

        public partial bool IsHappy { get; set; }

        [FormToggle("Good","Bad")]
        public partial bool Mood { get; set; }

        public partial ConsoleColor FavoriteColor { get; set; }

        [FormSlider(Min = 0, Max = 125)]
        [FormWidth(25)]
        public partial short Age { get; set; }

        [FormIgnore]
        public string Ignored { get; set; }

        public SimpleFormModel()
        {
            FirstName = "First";
            LastName = "Last";
            SSN = "*** ** ****";
            Mood = true;
            Age = 37;
            FavoriteColor = ConsoleColor.Magenta;
        }
    }

    [TestMethod]
    public void Forms_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        await Task.Delay(1000);
        var model = new SimpleFormModel();
        var options = FormGenerator.FromObject(model);
        var form = ConsoleApp.Current.LayoutRoot.Add(new Form(options)).Fill();
        await context.PaintAndRecordKeyFrameAsync();

        model.FirstName = "Adam";
        await context.PaintAndRecordKeyFrameAsync();

        model.LastName = "Abdelhamed";
        await context.PaintAndRecordKeyFrameAsync();

        var firstNameTextBox = form.Descendents.WhereAs<TextBox>().First();
        var lastNameTextBox = form.Descendents.WhereAs<TextBox>().Last();
        var moodToggle = form.Descendents.WhereAs<ToggleControl>().Last();
        var ageSlider = form.Descendents.WhereAs<SliderWithValueLabel>().First();

        firstNameTextBox.Value = "George".ToConsoleString();
        await context.PaintAndRecordKeyFrameAsync();

        lastNameTextBox.Value = "Washington".ToConsoleString();
        await context.PaintAndRecordKeyFrameAsync();

        moodToggle.On = true;
        await Task.Delay(1000);
        await context.PaintAndRecordKeyFrameAsync();

        ageSlider.Focus();
        for (var i = 1; i <= 15; i++)
        {
            ageSlider.Value = i;
            await context.PaintAndRecordKeyFrameAsync();
        }

        for (var i = 0; i < 10; i++)
        {
            ConsoleApp.Current.MoveFocus();
            await context.PaintAndRecordKeyFrameAsync();
        }

        ConsoleApp.Current.Stop();
    });
}
