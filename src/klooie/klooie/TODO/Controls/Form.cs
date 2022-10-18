
namespace klooie;

/// <summary>
/// An attribute that tells the form generator to ignore this
/// property
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class FormIgnoreAttribute : Attribute { }

/// <summary>
/// An attribute that tells the form generator to give this
/// property a read only treatment
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class FormReadOnlyAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public class FormSelectAllOnFocusAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public class FormContrastAttribute : Attribute { }

/// <summary>
/// An attribute that tells the form generator to use yes no labels for a toggle
/// </summary>
public class FormYesNoAttribute : Attribute { }

public class FormSliderAttribute : Attribute 
{
    public RGB BarColor { get; set; } = RGB.White;
    public RGB HandleColor { get; set; } = RGB.Gray;
    public float Min { get; set; } = 0;
    public float Max { get; set; } = 100;
    public float Value { get; set; } = 0;
    public float Increment { get; set; } = 10;
    public bool EnableWAndSKeysForUpDown { get; set; } = false;

    public Slider Factory()
    {
        return new Slider()
        {
            BarColor = BarColor,
            HandleColor = HandleColor, 
            Min = Min,
            Max = Max,
            Value = Value,
            Increment = Increment,
            EnableWAndSKeysForUpDown = EnableWAndSKeysForUpDown
        };
    }
}

/// <summary>
/// An attribute that tells the form generator to give this
/// property a specific value width
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class FormWidth : Attribute 
{
    public int Width { get; private set; }

    /// <summary>
    /// Creates a new FormWidth attribute
    /// </summary>
    /// <param name="width"></param>
    public FormWidth(int width)
    {
        this.Width = width;
    }
}

/// <summary>
/// An attribute that lets you override the display string 
/// on a form element
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Enum | AttributeTargets.Field)]
public class FormLabelAttribute : Attribute
{
    /// <summary>
    /// The label to display on the form element
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Initialized the attribute
    /// </summary>
    /// <param name="label">The label to display on the form element</param>
    public FormLabelAttribute(string label) { this.Label = label; }
}

/// <summary>
/// A class that represents a form element
/// </summary>
public class FormElement
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
public class FormOptions
{
    /// <summary>
    /// The percentage of the available width to use for labels
    /// </summary>
    public double LabelColumnPercentage { get; set; }

    /// <summary>
    /// The form elements to render
    /// </summary>
    public ObservableCollection<FormElement> Elements { get; private set; } = new ObservableCollection<FormElement>();

    /// <summary>
    /// Autogenerates form options for the given object by reflecting on its properties. All public properties with getters 
    /// and setters will be included in the form unless it has the FormIgnore attribute on it. This method supports strings,
    /// ints, and enums.
    /// 
    /// The form will be configured to two way bind all the form elements to the property values.
    /// </summary>
    /// <param name="o">The object to create form options for</param>
    /// <param name="labelColumnPercentage">the label column percentage to use</param>
    /// <returns></returns>
    public static FormOptions FromObject(object o, double labelColumnPercentage = .25)
    {
        var properties = o.GetType().GetProperties().Where(p => p.HasAttr<FormIgnoreAttribute>() == false && p.GetSetMethod() != null && p.GetGetMethod() != null).ToList();

        var ret = new FormOptions()
        {
            Elements = new ObservableCollection<FormElement>(),
            LabelColumnPercentage = labelColumnPercentage,
        };

        foreach (var property in properties)
        {
            ConsoleControl editControl = null;
            if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType == typeof(string))
            {
                var value = (string)property.GetValue(o);
                var textBox = new TextBox() { SelectAllOnFocus = property.HasAttr<FormSelectAllOnFocusAttribute>(), Value = value == null ? ConsoleString.Empty : value.ToString().ToConsoleString() };
                    
                if(property.HasAttr<FormContrastAttribute>())
                {
                    textBox.Background = RGB.White;
                    textBox.Foreground = RGB.Black;
                }
                    
                textBox.Sync(nameof(textBox.Value), () => property.SetValue(o, textBox.Value.ToString()), textBox);
                (o as IObservableObject)?.Sync(property.Name, () =>
                {
                    var valueRead = property.GetValue(o);
                    if (valueRead is ICanBeAConsoleString)
                    {
                        textBox.Value = (valueRead as ICanBeAConsoleString).ToConsoleString();
                    }
                    else
                    {
                        textBox.Value = (valueRead + "").ToConsoleString();
                    }
                }, textBox);
                editControl = textBox;
            }
            else if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType == typeof(int))
            {
                if (property.HasAttr<FormSliderAttribute>())
                {
                    var value = (int)property.GetValue(o);
                    var slider = property.Attr<FormSliderAttribute>().Factory();
                    slider.Value = value;
                    slider.Sync(nameof(slider.Value), () =>
                    {
                        property.SetValue(o, (int)slider.Value);
                    }, slider);
                    (o as IObservableObject)?.Sync(property.Name, () =>
                    {
                        var valueRead = (int)property.GetValue(o);
                        slider.Value = valueRead;
                    }, slider);
                    editControl = slider;
                }
                else
                {
                    var value = (int)property.GetValue(o);
                    var textBox = new TextBox() { SelectAllOnFocus = property.HasAttr<FormSelectAllOnFocusAttribute>(), Foreground = RGB.White, Value = value.ToString().ToWhite() };
                    if (property.HasAttr<FormContrastAttribute>())
                    {
                        textBox.Background = RGB.White;
                        textBox.Foreground = RGB.Black;
                    }
                    textBox.Sync(nameof(textBox.Value), () =>
                    {
                        if (textBox.Value.Length == 0)
                        {
                            textBox.Value = "0".ToConsoleString();
                        }
                        if (textBox.Value.Length > 0 && int.TryParse(textBox.Value.ToString(), out int result))
                        {
                            property.SetValue(o, result);
                        }
                        else if (textBox.Value.Length > 0)
                        {
                            textBox.Value = property.GetValue(o).ToString().ToConsoleString();
                        }
                    }, textBox);
                    (o as IObservableObject)?.Sync(property.Name, () =>
                    {
                        var valueRead = property.GetValue(o);
                        if (valueRead is ICanBeAConsoleString)
                        {
                            textBox.Value = (valueRead as ICanBeAConsoleString).ToConsoleString();
                        }
                        else
                        {
                            textBox.Value = (valueRead + "").ToConsoleString();
                        }
                    }, textBox);

                    textBox.AddedToVisualTree.Subscribe(() =>
                    {
                        var previouslyFocusedControl = textBox.Application.FocusedControl;

                        var emptyStringAction = ((ConsoleControl focusedControl) =>
                        {
                            if (previouslyFocusedControl == textBox && textBox.Application.FocusedControl != textBox)
                            {
                                if (textBox.Value.Length == 0)
                                {
                                    textBox.Value = "0".ToConsoleString();
                                    property.SetValue(o, 0);
                                }
                            }

                            previouslyFocusedControl = textBox.Application.FocusedControl;

                        });

                        textBox.Application.FocusChanged.Subscribe(emptyStringAction, textBox);
                    }, textBox);

                    editControl = textBox;
                }
            }
            else if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType == typeof(float))
            {

                var value = (float)property.GetValue(o);
                var textBox = new TextBox() { SelectAllOnFocus = property.HasAttr<FormSelectAllOnFocusAttribute>(), Value = value.ToString().ToConsoleString() };
                if (property.HasAttr<FormContrastAttribute>())
                {
                    textBox.Background = RGB.White;
                    textBox.Foreground = RGB.Black;
                }
                textBox.Sync(nameof(textBox.Value), () =>
                {
                    if (textBox.Value.Length == 0)
                    {
                        textBox.Value = "".ToConsoleString();
                    }
                    if (textBox.Value.Length > 0 && float.TryParse(textBox.Value.ToString(), out float result))
                    {
                        property.SetValue(o, result);
                    }
                    else if (textBox.Value.Length > 0)
                    {
                        textBox.Value = property.GetValue(o).ToString().ToConsoleString();
                    }
                }, textBox);
                (o as IObservableObject)?.Sync(property.Name, () =>
                {
                    var valueRead = property.GetValue(o);
                    if (valueRead is ICanBeAConsoleString)
                    {
                        textBox.Value = (valueRead as ICanBeAConsoleString).ToConsoleString();
                    }
                    else
                    {
                        textBox.Value = (valueRead + "").ToConsoleString();
                    }
                }, textBox);

                textBox.AddedToVisualTree.Subscribe(() =>
                {
                    var previouslyFocusedControl = textBox.Application.FocusedControl;

                    var emptyStringAction = ((ConsoleControl newlyFocused) =>
                    {
                        if (previouslyFocusedControl == textBox && textBox.Application.FocusedControl != textBox)
                        {
                            if (textBox.Value.Length == 0)
                            {
                                textBox.Value = "0".ToConsoleString();
                                property.SetValue(o, 0);
                            }
                        }

                        previouslyFocusedControl = textBox.Application.FocusedControl;

                    });

                    textBox.Application.FocusChanged.Subscribe(emptyStringAction, textBox);
                }, textBox);

                editControl = textBox;

            }
            else if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType.IsEnum)
            {
                var options = new List<DialogChoice>();
                foreach (var val in Enum.GetValues(property.PropertyType))
                {
                    var enumField = property.PropertyType.GetField(Enum.GetName(property.PropertyType, val));
                    var display = enumField.HasAttr<FormLabelAttribute>() ? enumField.Attr<FormLabelAttribute>().Label.ToConsoleString() : (val + "").ToConsoleString();

                    options.Add(new DialogChoice()
                    {
                        DisplayText = display,
                        Id = val.ToString(),
                        Value = val,
                    });
                }

                var dropdown = new Dropdown(options);
                dropdown.Width = Math.Min(40, options.Select(option => option.DisplayText.Length).Max() + 8);
                dropdown.Subscribe(nameof(dropdown.Value), () => property.SetValue(o, dropdown.Value.Value), dropdown);
                (o as IObservableObject)?.Sync(property.Name, () => dropdown.Value = options.Where(option => option.Value.Equals(property.GetValue(o))).Single(), dropdown);
                editControl = dropdown;
            }
            else if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType == typeof(RGB))
            {

                var dropdown = new ColorPicker();
                dropdown.Width = Math.Min(40, RGB.NamesToColors.Keys.Select(option => option.ToString().Length).Max() + 8);
                dropdown.Subscribe(nameof(dropdown.Value), () => property.SetValue(o, dropdown.Value), dropdown);
                (o as IObservableObject)?.Sync(property.Name, () => dropdown.Value = (RGB)(property.GetValue(o)), dropdown);
                editControl = dropdown;
            }
            else if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType == typeof(bool))
            {
                var toggle = new ToggleControl();

                if (property.HasAttr<FormYesNoAttribute>())
                {
                    toggle.OnLabel = " Yes ";
                    toggle.OffLabel = " No  ";
                }

                toggle.Subscribe(nameof(toggle.On), () => property.SetValue(o, toggle.On), toggle);
                (o as IObservableObject)?.Sync(property.Name, () => toggle.On = (bool)property.GetValue(o), toggle);
                editControl = toggle;
            }
            else
            {
                var value = property.GetValue(o);
                var valueString = value != null ? value.ToString().ToDarkGray() : "<null>".ToDarkGray();
                var valueLabel = new Label() { CompositionMode = CompositionMode.BlendBackground, CanFocus = false, Text = valueString + " (read only)".ToDarkGray() };
                (o as IObservableObject)?.Sync(property.Name, () => valueLabel.Text = (property.GetValue(o) + "").ToConsoleString() + " (read only)".ToDarkGray(), valueLabel);

                editControl = valueLabel;
            }

            if(property.HasAttr<FormWidth>())
            {
                editControl.Width = property.Attr<FormWidth>().Width;
            }

            ret.Elements.Add(new FormElement()
            {
                Label = property.HasAttr<FormLabelAttribute>() ? property.Attr<FormLabelAttribute>().Label.ToConsoleString() : property.Name.ToConsoleString(),
                ValueControl = editControl
            });
        }

        return ret;
    }
}

/// <summary>
/// A control that lets users edit a set of values as in a form
/// </summary>
public class Form : ConsolePanel
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

    private void EnsureSizing(FormElement element)
    {
        if (element.ValueControl is ToggleControl == false && element.ValueControl is Dropdown == false)
        {
            if (element.ValueControl.Width == 0)
            {
                element.ValueControl.FillHorizontally();
            }
        }
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


