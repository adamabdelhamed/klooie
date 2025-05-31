namespace klooie;
/// <summary>
/// A panel that displays its children as a grid of tiles
/// </summary>
public abstract partial class Gallery : ProtectedConsolePanel
{
    /// <summary>
    /// The horizontal margin used to space out the tiles
    /// </summary>
    public partial int HMargin { get; set; }

    /// <summary>
    /// The vertical margin used to space out the tiles
    /// </summary>
    public partial int VMargin { get; set; }

    /// <summary>
    /// Creates a new gallery
    /// </summary>
    public Gallery()
    {
        HMargin = 2;
        VMargin = 1;
        BoundsChanged.Subscribe(Refresh, this);
        HMarginChanged.Subscribe(Refresh, this);
        VMarginChanged.Subscribe(Refresh, this);
    }

    /// <summary>
    /// Refreshes the layout
    /// </summary>
    public void Refresh()
    {
        var x = HMargin;
        var y = VMargin;
        var rowHeight = 0;
        foreach (var child in ProtectedPanel.Children)
        {
            var proposed = new RectF(x, y, child.Width, child.Height);
            if (new RectF(0,0,Width, Height).Contains(proposed))
            {
                child.X = x;
                child.Y = y;
                rowHeight = Math.Max(rowHeight, child.Height);
                x += child.Width + HMargin;
            }
            else
            {
                x = HMargin;
                y += rowHeight + VMargin;
                rowHeight = child.Height;
                child.X = x;
                child.Y = y;
                x += child.Width + HMargin;
            }
        }
    }
}

/// <summary>
/// A gallery that displays a specific type of item
/// </summary>
/// <typeparam name="T"></typeparam>
public class DataGallery<T> : Gallery
{
    private Func<T, int, ConsoleControl> itemFactory;

    /// <summary>
    /// How many items to skip
    /// </summary>
    public int Skip { get; set; } = 0;

    /// <summary>
    /// How many items to take at one time
    /// </summary>
    public int Take { get; set; } = 30;

    /// <summary>
    /// The data source you provided
    /// </summary>
    public IEnumerable<T> Data { get; private set; }

    /// <summary>
    /// Specify this function to customize the sort order
    /// </summary>
    public Func<T, int> OrderBy { get; set; } = T => 0;

    /// <summary>
    /// Called whenever a page of items is shown
    /// </summary>
    public Event Shown { get; private set; } = new Event();

    /// <summary>
    /// Creates a data gallery
    /// </summary>
    /// <param name="itemFactory">a function that can convert a T into a ConsoleControl</param>
    public DataGallery(Func<T, int, ConsoleControl> itemFactory)
    {
        this.itemFactory = itemFactory;
    }

    /// <summary>
    /// Shows the given data
    /// </summary>
    /// <param name="data"></param>
    public void Show(IEnumerable<T> data)
    {
        Data = data;
        ProtectedPanel.Controls.Clear();
        var i = 0;
        var page = data.OrderBy(OrderBy).Skip(Skip).Take(Take).ToArray();
        foreach (var dataItem in page)
        {
            var ui = itemFactory(dataItem, i++);
            ProtectedPanel.Add(ui);
        }
        Refresh();
        Shown.Fire();
    }
}
