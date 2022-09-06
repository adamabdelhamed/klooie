namespace klooie.Gaming.Code;


public class WireOptions
{
    public RectF LeftConnection { get; set; }
    public RectF RightConnection { get; set; }
    public ILifetimeManager LifetimeManager { get; set; }

}



public class WireElement : GameCollider
{
    public ConsoleCharacter WirePen { get; set; }
    public WireElement(int z, ConsoleCharacter pen)
    {
        this.WirePen = pen;
        this.ResizeTo(1, 1);
        this.MoveTo(0, 0, z);
    }

    protected override void OnPaint(ConsoleBitmap context) => context.DrawPoint(WirePen, 0, 0);
}

public class WireHandle
{
    private List<WireElement> elements;
    private ILifetimeManager owner;
    public WireHandle(List<WireElement> elements, ILifetimeManager owner)
    {
        this.elements = elements;
        this.owner = owner;
    }

    public async Task TransmitAsync(ConsoleString data, bool leftToRight, TimeSpan duration, IDelayProvider delayProvider)
    {
        if (owner.IsExpired) return;

        if (leftToRight == false)
        {
            data = new ConsoleString(data.Reverse());
        }

        IOrderedEnumerable<WireElement> wireElementsOrdered;

        if (leftToRight)
        {
            wireElementsOrdered = elements.OrderBy(e => e.Left);
        }
        else
        {
            wireElementsOrdered = elements.OrderByDescending(e => e.Left);
        }

        var leftMost = wireElementsOrdered.First();
        var rightMost = wireElementsOrdered.Last();

        if (leftMost.Top >= rightMost.Top && leftToRight)
        {
            wireElementsOrdered = wireElementsOrdered.ThenByDescending(e => e.Top);
        }
        else
        {
            wireElementsOrdered = wireElementsOrdered.ThenBy(e => e.Top);
        }

        var orderedList = wireElementsOrdered.ToList();

        List<ConsoleControl> dataElements = new List<ConsoleControl>();
        for (var i = 0; i < data.Length; i++)
        {
            var start = i;
            var dataElement = Game.Current.GamePanel.Add(new GameCollider() { Pen = data[start] });
            dataElement.MoveTo(orderedList[start].Left, orderedList[start].Top, 2);
            dataElements.Add(dataElement);
            Lifetime.EarliestOf(owner, wireElementsOrdered.First()).OnDisposed(() =>
            {
                if (dataElement.IsExpired == false)
                {
                    dataElement.Dispose();
                }
            });
        }

        await Animator.AnimateAsync(new FloatAnimatorOptions()
        {
            From = 0,
            To = 1,
            Duration = duration.TotalMilliseconds,
            EasingFunction = Animator.EaseOutSoft,
            DelayProvider = delayProvider,
            Setter = v =>
            {
                if (owner.IsExpired) return;

                if (v < 1)
                {
                    var currentStep = (int)(Math.Floor(v * wireElementsOrdered.Count()));
                    for (var i = 0; i < dataElements.Count; i++)
                    {
                        var myStep = currentStep + i;
                        if (myStep < orderedList.Count)
                        {
                            dataElements[i].MoveTo(orderedList[myStep].Left, orderedList[myStep].Top);
                        }
                        else
                        {
                            dataElements[i].Dispose();
                        }
                    }
                }
                else
                {
                    foreach (var dataElement in dataElements.Where(e => e.IsExpired == false))
                    {
                        dataElement.Dispose();
                    }
                }
            }
        });
    }
}

public static class Wire
{
    public const char DefaultWireChar = '-';

    public static WireHandle Connect(WireOptions options)
    {
        var internetX = Game.Current.GamePanel.Width - 10;
        var wireElements = new List<WireElement>();

        for (var x = (float)Math.Floor(options.LeftConnection.Right) + 1; x <= internetX; x++)
        {
            var wireElement = Game.Current.GamePanel.Add(new WireElement(1, new ConsoleCharacter(DefaultWireChar)));
            wireElements.Add(wireElement);
            wireElement.MoveTo(x, options.LeftConnection.Top);
        }

        var top = (float)Math.Floor(Math.Min(options.LeftConnection.Top, options.RightConnection.Top));
        var bottom = (float)Math.Ceiling(Math.Max(options.LeftConnection.Top, options.RightConnection.Top));

        for (var y = top; y < bottom; y++)
        {
            var wireElement = Game.Current.GamePanel.Add(new WireElement(1, new ConsoleCharacter(':')));
            wireElements.Add(wireElement);
            wireElement.MoveTo(internetX, y);
        }

        for (var x = internetX; x <= (float)Math.Floor(options.RightConnection.Left); x++)
        {
            var wireElement = Game.Current.GamePanel.Add(new WireElement(1, new ConsoleCharacter(DefaultWireChar)));
            wireElements.Add(wireElement);
            wireElement.MoveTo(x, options.RightConnection.Top - 1);
        }

        options.LifetimeManager.OnDisposed(() =>
        {
            foreach (var wireElement in wireElements)
            {
                wireElement.Dispose();
            }
            wireElements.Clear();
            wireElements = null;
        });

        return new WireHandle(wireElements, options.LifetimeManager);
    }
}
