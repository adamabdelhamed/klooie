namespace klooie;
public class SortExpression
{
    public string Value { get; set; }
    public bool Descending { get; set; }

    public SortExpression(string value, bool descending = false)
    {
        this.Value = value;
        this.Descending = descending;
    }
}
