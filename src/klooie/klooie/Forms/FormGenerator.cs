namespace klooie;

public static class FormGenerator
{
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
            LabelColumnPercentage = labelColumnPercentage,
        };

        foreach (var property in properties)
        {
            ConsoleControl editControl = null;
            if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType == typeof(string))
            {
                var value = (string)property.GetValue(o);
                var textBox = new TextBox() { SelectAllOnFocus = property.HasAttr<FormSelectAllOnFocusAttribute>(), Value = value == null ? ConsoleString.Empty : value.ToString().ToConsoleString() };

                if (property.HasAttr<FormContrastAttribute>())
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

            if (property.HasAttr<FormWidth>())
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
