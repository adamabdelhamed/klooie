namespace klooie;
/// <summary>
/// Helpers for doing 2d layout
/// </summary>
public static class Layout
{
    /// <summary>
    /// Positions the given controls in a horizontal stack
    /// </summary>
    /// <param name="margin"></param>
    /// <param name="controls"></param>
    /// <returns></returns>
    public static int StackHorizontally(int margin, IList<ConsoleControl> controls)
    {
        int left = 0;
        int width = 0;
        for (var i = 0; i < controls.Count; i++)
        {
            var control = controls[i];
            control.X = left;
            width += control.Width + (i < controls.Count - 1 ? margin : 0);
            left += control.Width + margin;
        }
        return width;
    }



    public static int StackVertically(int margin, IList<ConsoleControl> controls)
    {
        int top = 0;
        int height = 0;
        for (var i = 0; i < controls.Count; i++)
        {
            var control = controls[i];

            control.Y = top;
            height += control.Height + (i < controls.Count - 1 ? margin : 0);
            top += control.Height + margin;
        }
        return height;
    }

    public static T CenterBoth<T>(this T child) where T : ConsoleControl => 
        child.CenterHorizontally().CenterVertically();


    public static T CenterVertically<T>(this T child) where T : ConsoleControl
    {
        return DoTwoWayLayoutAction(child, (c, p) =>
        {
            if (p.Height == 0 || c.Height == 0) return;
            var middle = p.Height / 2f;
            c.Y = ConsoleMath.Round(middle - child.Height / 2f);
        });
    }

    public static T CenterHorizontally<T>(this T child) where T : ConsoleControl
    {
        return DoTwoWayLayoutAction(child, (c, p) =>
        {
            if (p.Width == 0 || c.Width == 0) return;
            var middle = p.Width / 2f;
            c.X = ConsoleMath.Round(middle - child.Width / 2f);
        });
    }

    public static T Fill<T>(this T child, Thickness? padding = null) where T : ConsoleControl
    {
        var effectivePadding = padding.HasValue ? padding.Value : new Thickness(0, 0, 0, 0);
        return DoParentTriggeredLayoutAction(child, (c, p) =>
        {
            if (p.Width == 0 || p.Height == 0) return;

            var newX = 0;
            var newY = 0;
            var newW = p.Width;
            var newH = p.Height;
            newX += effectivePadding.Left;
            newW -= effectivePadding.Left;
            newW -= effectivePadding.Right;

            newY += effectivePadding.Top;
            newH -= effectivePadding.Top;
            newH -= effectivePadding.Bottom;

            if (newW < 0) newW = 0;
            if (newH < 0) newH = 0;

            c.Bounds = new RectF(newX, newY, newW, newH);
        });
    }

    public static T FillMax<T>(this T child, int? maxWidth = null, int? maxHeight = null) where T : ConsoleControl
    {
        CenterBoth(child);
        return DoParentTriggeredLayoutAction(child, (c, p) =>
        {
            if (p.Width == 0 || p.Height == 0) return;

            var newW = p.Width;
            var newH = p.Height;

            if (maxWidth.HasValue && newW > maxWidth.Value) newW = maxWidth.Value;
            if (maxHeight.HasValue && newH > maxHeight.Value) newH = maxHeight.Value;

            c.Bounds = new RectF(c.Left, c.Top, newW, newH);
        });
    }

    public static T FillHorizontally<T>(this T child, Thickness? padding = null) where T : ConsoleControl
    {
        var effectivePadding = padding.HasValue ? padding.Value : new Thickness(0, 0, 0, 0);
        return DoParentTriggeredLayoutAction(child, (c, p) =>
        {
            if (p.Width == 0) return;
            if (p.Width - (effectivePadding.Right + effectivePadding.Left) <= 0) return;
            c.Bounds = new RectF(effectivePadding.Left, c.Y, p.Width - (effectivePadding.Right + effectivePadding.Left), c.Height);
        });
    }

    public static T FillVertically<T>(this T child, Thickness? padding = null) where T : ConsoleControl
    {
        var effectivePadding = padding.HasValue ? padding.Value : new Thickness(0, 0, 0, 0);
        return DoParentTriggeredLayoutAction(child, (c, p) => c.Bounds = new RectF(c.X, effectivePadding.Top, c.Width, p.Height - (effectivePadding.Top + effectivePadding.Bottom)));
    }

    public static T DockToBottom<T>(this T child, Container parent = null, int padding = 0) where T : ConsoleControl =>
        DoTwoWayLayoutAction(child, (c, p) => c.Y = p.Height - c.Height - padding);


    public static T DockToTop<T>(this T child, int padding = 0) where T : ConsoleControl =>
        DoTwoWayLayoutAction(child, (c, p) => c.Y = padding);


    public static T DockToRight<T>(this T child, int padding = 0) where T : ConsoleControl =>
        DoTwoWayLayoutAction(child, (c, p) => c.X = p.Width - c.Width - padding);


    public static T DockToLeft<T>(this T child, int padding = 0) where T : ConsoleControl =>
        DoTwoWayLayoutAction(child, (c, p) => c.X = padding);


    private static T DoTwoWayLayoutAction<T>(this T child, Action<T, Container> a) where T : ConsoleControl
    {
        if (child.Parent == null) throw new ArgumentException("This control does yet have a parent");
        var syncAction = () => a(child, child.Parent);
        child.Subscribe(nameof(ConsoleControl.Bounds), syncAction, child.Parent);
        child.Parent.Subscribe(nameof(ConsoleControl.Bounds), syncAction, child.Parent);
        syncAction();
        return child;
    }

    private static T DoParentTriggeredLayoutAction<T>(this T child, Action<T, Container> a) where T : ConsoleControl
    {
        if (child.Parent == null) throw new ArgumentException("This control does yet have a parent");
        var syncAction = () => a(child, child.Parent);
        child.Parent.Subscribe(nameof(ConsoleControl.Bounds), syncAction, child.Parent);
        syncAction();
        return child;
    }
}

public readonly struct Thickness
{
    /// <summary>
    /// the amount of padding on the left
    /// </summary>
    public readonly int Left { get; private init; }

    /// <summary>
    /// the amount of padding on the right
    /// </summary>
    public readonly int Right { get; private init; }

    /// <summary>
    /// the amount of padding on top
    /// </summary>
    public readonly int Top { get; private init; }

    /// <summary>
    /// the amount of padding on the bottom
    /// </summary>
    public readonly int Bottom { get; private init; }

    /// <summary>
    /// the total amount of horizontal padding
    /// </summary>
    public int HorizontalPadding => Left + Right;

    /// <summary>
    /// the total amount of vertical padding
    /// </summary>
    public int VerticalPadding => Top + Bottom;

    /// <summary>
    /// Creates a new thickness
    /// </summary>
    /// <param name="l">the amount of padding on the left</param>
    /// <param name="r">the amount of padding on the right</param>
    /// <param name="t">the amount of padding on top</param>
    /// <param name="b">the amount of padding on the bottom</param>
    public Thickness(int l, int r, int t, int b)
    {
        this.Left = l;
        this.Right = r;
        this.Top = t;
        this.Bottom = b;
    }

    // A thickness that is optimized for the console by making
    // the left and right 2 pixels and the top and bottom 1. This
    // is because characters are typically about twice as tall as
    // they are wide.
    public static Thickness Square = new Thickness(2, 2, 1, 1);
}