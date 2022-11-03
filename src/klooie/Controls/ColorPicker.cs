namespace klooie;

public class ColorPicker : Dropdown<RGB>
{
    protected override IEnumerable<DialogChoice> Choices() => RGB.ColorsToNames.Select(c => new DialogChoice 
    {
        DisplayText = c.Value.ToConsoleString(), 
        Value = c.Key, 
        Id = c.Value 
    });
}

