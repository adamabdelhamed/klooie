namespace klooie;

/// <summary>
/// A class that represents a form element
/// </summary>
public sealed class FormElement
{
    /// <summary>
    /// The label for the form element
    /// </summary>
    public ConsoleString Label { get; set; }
    /// <summary>
    /// The control that renders the form element's value
    /// </summary>
    public ConsoleControl ValueControl { get; set; }
}

/// <summary>
/// Options for configuring a form
/// </summary>
public sealed class FormOptions
{
    /// <summary>
    /// The percentage of the available width to use for labels
    /// </summary>
    public double LabelColumnPercentage { get; set; }

    /// <summary>
    /// The form elements to render
    /// </summary>
    public ObservableCollection<FormElement> Elements { get; private set; } = new ObservableCollection<FormElement>(); 
}

/// <summary>
/// A control that lets users edit a set of values as in a form
/// </summary>
public sealed class Form : ConsolePanel
{
    /// <summary>
    /// The options that were provided
    /// </summary>
    public FormOptions Options { get; private set; }

    /// <summary>
    /// Creates a form using the given options
    /// </summary>
    /// <param name="options">form options</param>
    public Form(FormOptions options)
    {
        this.Options = options;
        this.AddedToVisualTree.Subscribe(InitializeForm, this);
    }

    private void InitializeForm()
    {
        var formFieldStack = Add(new StackPanel() { Background = this.Background, Orientation = Orientation.Vertical, Margin = 1 }).Fill();

        this.Sync(nameof(this.Bounds), () =>
        {
            var labelColumnWidth = ConsoleMath.Round(this.Width * this.Options.LabelColumnPercentage);
            var valueColumnWidth = ConsoleMath.Round(this.Width * (1 - this.Options.LabelColumnPercentage));

            while (labelColumnWidth + valueColumnWidth > this.Width)
            {
                labelColumnWidth--;
            }

            while (labelColumnWidth + valueColumnWidth < this.Width)
            {
                valueColumnWidth++;
            }

            Descendents.WhereAs<FormField>().ForEach(f => f.LabelColumnWidth = labelColumnWidth);

        }, this);

        foreach (var element in this.Options.Elements)
        {
            formFieldStack.Add(new FormField(element.Label, element.ValueControl) { LabelColumnWidth = ConsoleMath.Round(this.Width * this.Options.LabelColumnPercentage) }).FillHorizontally();
        }

        this.Options.Elements.Added.Subscribe((addedElement) =>
        {
            var index = this.Options.Elements.IndexOf(addedElement);

            var formField = new FormField(addedElement.Label, addedElement.ValueControl) { LabelColumnWidth = ConsoleMath.Round(this.Width * this.Options.LabelColumnPercentage) };
            formFieldStack.Controls.Insert(index, formField);
            formField.FillHorizontally();
        }, this);

        this.Options.Elements.Removed.Subscribe((removedElement) =>
        {
            var index = formFieldStack.Children.WhereAs<FormField>().Select(f => f.ValueControl).ToList().IndexOf(removedElement.ValueControl);
            formFieldStack.Controls.RemoveAt(index);
        }, this);

        this.Options.Elements.AssignedToIndex.Subscribe((assignment) => throw new NotSupportedException("Index assignments not supported in form elements"), this);
    }
}

internal class FormField : ProtectedConsolePanel
{
    private ConsoleControl valueControl;
    private ConsoleString labelText;
    public ConsoleControl ValueControl => valueControl;
    public int LabelColumnWidth { get => Get<int>(); set => Set(value); }

    public FormField(ConsoleString label, ConsoleControl valueControl)
    {
        LabelColumnWidth = 10;
        this.labelText = label;
        this.valueControl = valueControl;
        Ready.SubscribeOnce(Init);
        valueControl.Sync(nameof(valueControl.Bounds), () => this.Height = Math.Max(1, valueControl.Height), valueControl);
    }

    private void Init()
    {
        ProtectedPanel.Add(new Label() { Text = labelText, Width = LabelColumnWidth });
        valueControl.Height = valueControl.Height == 0 ? 1 : valueControl.Height;
        valueControl.X = LabelColumnWidth;
        ProtectedPanel.Add(valueControl);
        Subscribe(nameof(LabelColumnWidth), () => valueControl.X = LabelColumnWidth, this);
    }
}


