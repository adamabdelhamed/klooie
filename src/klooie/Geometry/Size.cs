namespace klooie;
public readonly struct Size
{
    public readonly int Width { get; init; }
    public readonly int Height { get; init; }
    public Size(int w, int h) : this()
    {
        Width = w;
        Height = h;
    }
}
