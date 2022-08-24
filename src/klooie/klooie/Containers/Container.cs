using PowerArgs;
namespace klooie;

public enum CompositionMode
{
    /// <summary>
    /// The default. The control being painted always paints over whatever pixel is beneath it
    /// </summary>
    PaintOver = 0,
    /// <summary>
    /// If the control being painted's pixel has a non-default BG and the pixel it's being
    /// painted on also has a non-default BG then use the background color of the existing pixel instead
    /// of the control pixel. Otherwise behaves like PaintOver.
    /// </summary>
    BlendBackground = 1,
    /// <summary>
    /// If the control being painted's pixel would end up looking invisible on the parent panel
    /// then skip drawing it so that the pixel will end up looking transparent.
    /// </summary>
    BlendVisible = 2,
}
public abstract class Container : ConsoleControl
{
    internal Container() { }

    public abstract IEnumerable<ConsoleControl> Children { get; }

    public IEnumerable<ConsoleControl> Descendents
    {
        get
        {
            List<ConsoleControl> descendends = new List<ConsoleControl>();
            VisitControlTree((d) =>
            {
                descendends.Add(d);
                return false;
            });

            return descendends.AsReadOnly();
        }
    }

    /// <summary>
    /// Visits every control in the control tree, recursively, using the visit action provided
    /// </summary>
    /// <param name="visitAction">the visitor function that will be run for each child control, the function can return true if it wants to stop further visitation</param>
    /// <param name="root">set to null, used for recursion</param>
    /// <returns>true if the visitation was short ciruited by a visitor, false otherwise</returns>
    public bool VisitControlTree(Func<ConsoleControl, bool> visitAction, Container root = null)
    {
        bool shortCircuit = false;
        root = root ?? this;

        foreach (var child in root.Children)
        {
            shortCircuit = visitAction(child);
            if (shortCircuit) return true;

            if (child is Container)
            {
                shortCircuit = VisitControlTree(visitAction, child as Container);
                if (shortCircuit) return true;
            }
        }

        return false;
    }

    protected void Compose(ConsoleControl control)
    {
        if (control.IsVisible == false) return;
        control.Paint();

        foreach (var filter in control.Filters)
        {
            filter.Control = control;
            filter.Filter(control.Bitmap);
        }

        if (control.CompositionMode == CompositionMode.PaintOver)
        {
            ComposePaintOver(control);
        }
        else if (control.CompositionMode == CompositionMode.BlendBackground)
        {
            ComposeBlendBackground(control);
        }
        else
        {
            ComposeBlendVisible(control);
        }

    }

    protected virtual (int X, int Y) Transform(ConsoleControl c) => (c.X, c.Y);

    private void ComposePaintOver(ConsoleControl control)
    {
        var position = Transform(control);

        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);

        var myPixX = Bitmap.Pixels.AsSpan();
        for (var x = minX; x < maxX; x++)
        {
            var myPixY = myPixX[x].AsSpan();
            for (var y = minY; y < maxY; y++)
            {
                myPixY[y] = control.Bitmap.Pixels[x - position.X][y - position.Y];
            }
        }
    }

    private void ComposeBlendBackground(ConsoleControl control)
    {
        var position = Transform(control);
        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);
        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                var controlPixel = control.Bitmap.Pixels[x - position.X][y - position.Y];
                var myPixel = Bitmap.Pixels[x][y];
                var controlIsNonDefaultBg = controlPixel.BackgroundColor != ConsoleString.DefaultBackgroundColor;
                var pixelIsNonDefaultBg = myPixel.BackgroundColor != ConsoleString.DefaultBackgroundColor;
                var blend = controlIsNonDefaultBg && pixelIsNonDefaultBg;
                Bitmap.Pixels[x][y] = blend ? new ConsoleCharacter(controlPixel.Value, controlPixel.ForegroundColor, myPixel.BackgroundColor) : controlPixel;
            }
        }
    }

    private void ComposeBlendVisible(ConsoleControl control)
    {
        var position = Transform(control);
        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);
        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                var controlPixel = control.Bitmap.Pixels[x - position.X][y - position.Y];
                var vis = controlPixel.Value == ' ' ? controlPixel.BackgroundColor != Background : controlPixel.ForegroundColor != Background || controlPixel.BackgroundColor != Background;
                Bitmap.Pixels[x][y] = vis ? controlPixel : Bitmap.Pixels[x][y];
            }
        }
    }
}
