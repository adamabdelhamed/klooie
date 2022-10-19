namespace klooie;

public class ColorPicker : ProtectedConsolePanel
{
    public RGB Value { get => Get<RGB>(); set => Set(value); }

    public ColorPicker()
    {
        CanFocus = true;
        var dropdown = ProtectedPanel.Add(new Dropdown(RGB.ColorsToNames
            .Select(c => new DialogChoice { DisplayText = c.Value.ToConsoleString(), Value = c.Key, Id = c.Value }))).Fill();

        dropdown.Sync(nameof(dropdown.Value), () => this.Value = (RGB)dropdown.Value.Value, this);
        this.Subscribe(nameof(Value), () => dropdown.Value = dropdown.Options.Where(o => o.Value.Equals(Value)).Single(), this);

        this.Focused.Subscribe(() => dropdown.Focus(), this);
    }
}

