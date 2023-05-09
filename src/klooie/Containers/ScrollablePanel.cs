namespace klooie;

/// <summary>
/// A panel that can scroll its content. Your responsibility is to
/// add content to the ScrollableContent property on this class and
/// to size the ScrollableContent panel to be whatever size you need.
/// 
/// If the size of ScrollableContent is larger than this panel then scrollbars
/// will be added. They will become focusable and also enable keyboard shortcuts
/// like home, end, page up, and page down.
/// 
/// If a control within ScrollableContent gets focus and it not currently in view then
/// it will be automatically scrolled into view.
/// </summary>
public class ScrollablePanel : ProtectedConsolePanel
{
    private Scrollbar verticalScrollbar;
    private Scrollbar horizontalScrollbar;

    /// <summary>
    /// The content that you should add to
    /// </summary>
    public ConsolePanel ScrollableContent { get; private set; }

    /// <summary>
    /// Gets or sets the current horizontal scroll amount in pixels
    /// </summary>
    public int HorizontalScrollUnits
    {
        get
        {
            return Get<int>();
        }
        set
        {
            if (value < 0) throw new IndexOutOfRangeException("Value must be >= 0");
            Set(value);
        }
    }

    /// <summary>
    /// Gets or sets the current vertical scroll amount in pixels
    /// </summary>
    public int VerticalScrollUnits
    {
        get
        {
            return Get<int>();
        }
        set
        {
            if (value < 0) throw new IndexOutOfRangeException("Value must be >= 0");
            Set(value);
        }
    }

    /// <summary>
    /// Creates a scrollable panel
    /// </summary>
    public ScrollablePanel()
    {
        ScrollableContent = ProtectedPanel.Add(new ConsolePanel()).Fill();
        Sync(nameof(Background), () => ScrollableContent.Background = Background, this);
        verticalScrollbar = ProtectedPanel.Add(new Scrollbar(Orientation.Vertical) { ZIndex = 10, Width = 1 }).DockToRight();
        horizontalScrollbar = ProtectedPanel.Add(new Scrollbar(Orientation.Horizontal) { ZIndex = 10, Height = 1 }).DockToBottom();
        AddedToVisualTree.Subscribe(OnAddedToVisualTree, this);
    }

    public override bool IsInView(ConsoleControl c)
    {
        return true;
    }

    private void OnAddedToVisualTree()
    {
        Application.FocusChanged.Subscribe(FocusChanged, this);
        Sync(nameof(HorizontalScrollUnits), UpdateScrollbars, this);
        Sync(nameof(VerticalScrollUnits), UpdateScrollbars, this);
        ScrollableContent.Subscribe(nameof(Bounds), UpdateScrollbars, this);
        ScrollableContent.Controls.Sync(c => c.Subscribe(nameof(Bounds), UpdateScrollbars, c), (c) => { }, () => { }, this);
    }

    private void UpdateScrollbars()
    {
        var contentSize = ScrollableContentSize;

        if (contentSize.Height <= Height)
        {
            verticalScrollbar.Height = 0;
            verticalScrollbar.CanFocus = false;
            VerticalScrollUnits = 0; // dangerous because if the observable is ever changed to notify on equal changes then this will cause a stack overflow
        }
        else
        {
            var verticalPercentageShowing = Height / (double)contentSize.Height;
            var verticalPercentageScrolled = VerticalScrollUnits / (double)contentSize.Height;


            var verticalScrollbarHeight = (int)Math.Ceiling(Height * verticalPercentageShowing);

            verticalScrollbar.Height = verticalScrollbarHeight;
            verticalScrollbar.Y = ConsoleMath.Round(Height * verticalPercentageScrolled);

            if (verticalScrollbar.Y == Height && verticalPercentageScrolled < 1)
            {
                verticalScrollbar.Y--;
            }
            else if (verticalScrollbar.Y == 0 && verticalPercentageScrolled > 0)
            {
                verticalScrollbar.Y = 1;
            }

            verticalScrollbar.CanFocus = true;
        }

        if (contentSize.Width <= Width)
        {
            horizontalScrollbar.Width = 0;
            horizontalScrollbar.CanFocus = false;
            HorizontalScrollUnits = 0; // dangerous because if the observable is ever changed to notify on equal changes then this will cause a stack overflow
        }
        else
        {
            var horizontalPercentageShowing = Width / (double)contentSize.Width;
            var horizontalPercentageScrolled = HorizontalScrollUnits / (double)contentSize.Width;
            horizontalScrollbar.Width = (int)(Width * horizontalPercentageShowing);
            horizontalScrollbar.X = (int)(Width * horizontalPercentageScrolled);

            if (verticalScrollbar.X == Width && horizontalPercentageScrolled < 1)
            {
                verticalScrollbar.X--;
            }
            else if (verticalScrollbar.X == 0 && horizontalPercentageScrolled > 0)
            {
                verticalScrollbar.X = 1;
            }

            horizontalScrollbar.CanFocus = true;
        }
    }

    private void FocusChanged(ConsoleControl newlyFocused)
    {
        bool focusedControlIsWithinMe = VisitControlTree((control) =>
        {
            if (IsExpired || IsExpiring || IsBeingRemoved) return false;
            return control is Scrollbar == false && control == Application.FocusedControl;
        });

        if (focusedControlIsWithinMe)
        {
            var offset = Application.FocusedControl.CalculateRelativePosition(this);

            var visibleWindowBounds = new RectF(HorizontalScrollUnits, VerticalScrollUnits, Width, Height);
            var focusedControlBounds = new RectF(offset.Left, offset.Top, Application.FocusedControl.Width, Application.FocusedControl.Height);

            if (focusedControlBounds.IsAbove(visibleWindowBounds))
            {
                int amount = ConsoleMath.Round(visibleWindowBounds.Top - focusedControlBounds.Top);
                VerticalScrollUnits -= amount;
            }

            if (focusedControlBounds.IsBelow(visibleWindowBounds))
            {
                int amount = ConsoleMath.Round(focusedControlBounds.Bottom - visibleWindowBounds.Bottom);
                VerticalScrollUnits += amount;
            }

            if (focusedControlBounds.IsLeftOf(visibleWindowBounds))
            {
                int amount = ConsoleMath.Round(visibleWindowBounds.Left - focusedControlBounds.Left);
                HorizontalScrollUnits -= amount;
            }

            if (focusedControlBounds.IsRightOf(visibleWindowBounds))
            {
                int amount = ConsoleMath.Round(focusedControlBounds.Right - visibleWindowBounds.Right);
                HorizontalScrollUnits += amount;
            }
        }
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        ScrollableContent.Paint();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                int scrollX = x + HorizontalScrollUnits;
                int scrollY = y + VerticalScrollUnits;

                if (scrollX >= ScrollableContent.Width || scrollY >= ScrollableContent.Height)
                {
                    continue;
                }

                var scrolledPixel = ScrollableContent.Bitmap.GetPixel(scrollX, scrollY);
                context.DrawPoint(scrolledPixel, x, y);
            }
        }

        if (ScrollableContent.Width > Width)
        {
            horizontalScrollbar.Paint();
            DrawScrollbar(horizontalScrollbar, context);
        }

        if (ScrollableContent.Height > Height)
        {
            verticalScrollbar.Paint();
            DrawScrollbar(verticalScrollbar, context);
        }
    }

    private void DrawScrollbar(Scrollbar bar, ConsoleBitmap context)
    {
        for (int x = 0; x < bar.Width; x++)
        {
            for (int y = 0; y < bar.Height; y++)
            {
                context.DrawPoint(bar.Bitmap.GetPixel(x, y), x + bar.X, y + bar.Y);
            }
        }
    }

    internal Size ScrollableContentSize
    {
        get
        {
            int w = 0;
            int h = 0;

            foreach (var c in ScrollableContent.Controls.Where(c => c.IsVisible))
            {
                w = Math.Max(w, c.X + c.Width);
                h = Math.Max(h, c.Y + c.Height);
            }
            return new Size(w, h);
        }
    }
}

/// <summary>
/// A control that implements scrolling
/// </summary>
public class Scrollbar : ConsoleControl
{
    private ScrollablePanel ScrollablePanel => Parent?.Parent as ScrollablePanel;
    private Orientation orientation;

    internal Scrollbar(Orientation orientation)
    {
        this.orientation = orientation;
        Background = RGB.White;
        KeyInputReceived.Subscribe(OnKeyInputReceived, this);
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        base.OnPaint(context);
        if (HasFocus)
        {
            context.FillRectUnsafe(new ConsoleCharacter(' ', backgroundColor: FocusColor), 0, 0, Width, Height);
        }
    }

    private void OnKeyInputReceived(ConsoleKeyInfo info)
    {
        var scrollableSize = ScrollablePanel.ScrollableContentSize;
        if (info.Key == ConsoleKey.Home)
        {
            if (orientation == Orientation.Vertical)
            {
                ScrollablePanel.VerticalScrollUnits = 0;
            }
            else
            {
                ScrollablePanel.HorizontalScrollUnits = 0;
            }
        }
        else if (info.Key == ConsoleKey.End)
        {
            if (orientation == Orientation.Vertical)
            {
                ScrollablePanel.VerticalScrollUnits = scrollableSize.Height - ScrollablePanel.Height;
            }
            else
            {
                ScrollablePanel.HorizontalScrollUnits = scrollableSize.Width - ScrollablePanel.Width;
            }
        }
        if (info.Key == ConsoleKey.PageUp)
        {
            if (orientation == Orientation.Vertical)
            {
                int upAmount = Math.Min(ScrollablePanel.Height, ScrollablePanel.VerticalScrollUnits);
                ScrollablePanel.VerticalScrollUnits -= upAmount;
            }
            else
            {
                int leftAmount = Math.Min(ScrollablePanel.Width, ScrollablePanel.HorizontalScrollUnits);
                ScrollablePanel.HorizontalScrollUnits -= leftAmount;
            }
        }
        else if (info.Key == ConsoleKey.PageDown)
        {
            if (orientation == Orientation.Vertical)
            {
                int downAmount = Math.Min(ScrollablePanel.Height, ScrollablePanel.ScrollableContentSize.Height - ScrollablePanel.VerticalScrollUnits - ScrollablePanel.Height);
                ScrollablePanel.VerticalScrollUnits += downAmount;
            }
            else
            {
                int rightAmount = Math.Min(ScrollablePanel.Width, ScrollablePanel.ScrollableContentSize.Width - ScrollablePanel.HorizontalScrollUnits - ScrollablePanel.Width);
                ScrollablePanel.VerticalScrollUnits += rightAmount;
            }
        }
        else if (info.Key == ConsoleKey.DownArrow)
        {
            if (ScrollablePanel.VerticalScrollUnits < scrollableSize.Height - ScrollablePanel.Height)
            {
                ScrollablePanel.VerticalScrollUnits++;
            }
        }
        else if (info.Key == ConsoleKey.UpArrow)
        {
            if (ScrollablePanel.VerticalScrollUnits > 0)
            {
                ScrollablePanel.VerticalScrollUnits--;
            }
        }
        else if (info.Key == ConsoleKey.RightArrow)
        {
            if (ScrollablePanel.HorizontalScrollUnits < scrollableSize.Width - ScrollablePanel.Width)
            {
                ScrollablePanel.HorizontalScrollUnits++;
            }
        }
        else if (info.Key == ConsoleKey.LeftArrow)
        {
            if (ScrollablePanel.HorizontalScrollUnits > 0)
            {
                ScrollablePanel.HorizontalScrollUnits--;
            }
        }
    }
}
