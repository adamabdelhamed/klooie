namespace klooie;

/// <summary>
/// A class that represents a form element
/// </summary>
public sealed class FormElement
{
    /// <summary>
    /// True if the value control supports having its width changed by the form
    /// </summary>
    public bool SupportsDynamicWidth { get; set; } = true;
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
    /// The Column spec to use for the label column. See GridLayout for details on how to specify the
    /// value as pixels, percentage, or remainder values.
    /// </summary>
    public string LabelColumnSpec { get; set; } = "25%";

    /// <summary>
    /// The Column spec to use for the value column. See GridLayout for details on how to specify the
    /// value as pixels, percentage, or remainder values.
    /// </summary>
    public string ValueColumnSpec { get; set; } = "75%";

    /// <summary>
    /// The form elements to render
    /// </summary>
    public List<FormElement> Elements { get; private set; } = new List<FormElement>(); 
}

/// <summary>
/// A control that lets users edit a set of values as in a form
/// </summary>
public sealed class Form : ProtectedConsolePanel
{
    private GridLayout grid;
    private Dictionary<int, int> rowMap;

    /// <summary>
    /// The options that were provided
    /// </summary>
    public FormOptions Options { get; private init; }

    /// <summary>
    /// Creates a form using the given options
    /// </summary>
    /// <param name="options">form options</param>
    public Form(FormOptions options)
    {
        this.Options = options;
        this.Ready.Subscribe(InitializeForm, this);
    }

    private void InitializeForm()
    {
        CreateGridLayout();

        for (int i = 0; i < Options.Elements.Count; i++)
        {
            var element = this.Options.Elements[i];
            var label = grid.Add(new Label(element.Label), 0, rowMap[i]);
            var valueControl = WrapInPanelIfCannotBeResized(element);
            grid.Add(valueControl, 1, rowMap[i]);
        }
    }

    private ConsoleControl WrapInPanelIfCannotBeResized(FormElement element)
    {
        if (element.SupportsDynamicWidth) return element.ValueControl;

        var panel = new ConsolePanel() { Height = 1 };
        panel.Add(element.ValueControl);
        return panel;
    }

    private void CreateGridLayout()
    {
        grid?.Dispose();
        var rows = new List<GridRowDefinition>();
        rowMap = new Dictionary<int, int>();
        var elementRows = Options.Elements.Select(e => new GridRowDefinition() { Height = e.ValueControl.Height, Type = GridValueType.Pixels }).ToArray();
        for (var i = 0; i < elementRows.Length; i++)
        {
            rowMap.Add(i, rows.Count);
            rows.Add(elementRows[i]);
            if (i < elementRows.Length - 1)
            {
                var spacerRow = new GridRowDefinition() { Height = 1, Type = GridValueType.Pixels };
                rows.Add(spacerRow);
            }
        }

        var rowSpec = new GridLayoutOptions() { Rows = rows }.GetRowSpec();
        var colSpec = Options.LabelColumnSpec + ";" + Options.ValueColumnSpec;
        grid = ProtectedPanel.Add(new GridLayout(rowSpec, colSpec)).Fill();
    }
}


