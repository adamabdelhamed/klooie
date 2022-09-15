namespace klooie;
public class Gallery : ProtectedConsolePanel
{
    public int HMargin { get => Get<int>(); set => Set(value); }
    public int VMargin { get => Get<int>(); set => Set(value); }

    public Gallery()
    {
        HMargin = 2;
        VMargin = 1;

        Subscribe(nameof(Bounds), Refresh, this);
        Subscribe(nameof(HMargin), Refresh, this);
        Subscribe(nameof(VMargin), Refresh, this);
    }

    public void Refresh()
    {
        var x = HMargin;
        var y = VMargin;
        var rowHeight = 0;
        foreach (var child in Children)
        {
            var proposed = new RectF(x, y, child.Width, child.Height);
            if (this.Bounds.Contains(proposed))
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
            }
        }
    }
}

public class DataGallery<T> : Gallery
{
    private Func<T, int, ConsoleControl> itemFactory;
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 30;

    public IEnumerable<T> Data { get; private set; }

    public Func<T, int> OrderBy { get; set; } = T => 0;

    public Event Shown { get; private set; } = new Event();

    public DataGallery(Func<T, int, ConsoleControl> itemFactory)
    {
        this.itemFactory = itemFactory;
    }

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
