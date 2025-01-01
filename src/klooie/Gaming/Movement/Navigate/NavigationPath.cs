namespace klooie.Gaming;
public class NavigationPath : Lifetime
{
#if DEBUG
    private ObservableCollection<RectF> tail;
#else
    private List<RectF> tail;
#endif
    private Navigate options;

    public NavigationPath(Navigate options, List<RectF> steps)
    {
        this.options = options;
#if DEBUG
        this.tail = new ObservableCollection<RectF>();
#else
        tail = new List<RectF>();
#endif

        foreach(var step in steps)
        {
            tail.Add(step);
        }

#if DEBUG
        if(options.Options.Show) ShowPath();
#endif
    }

    public GameCollider FindLocalTarget() => tail.Count > 0 ? new ColliderBox(tail[0]) : null;


    public void PruneTail()
    {
        var buffer = ObstacleBufferPool.Instance.Rent();
        try
        {
            for (var i = tail.Count - 1; i >= 0; i--)
            {
                var curr = tail[i];
                var d = options.Element.Bounds.CalculateDistanceTo(curr);
                var weAreCloseEnoughThatWeAreWillingToDoAnExpensiveLineOfSightTest = d < 2;
                if (weAreCloseEnoughThatWeAreWillingToDoAnExpensiveLineOfSightTest)
                {
                    buffer.WriteableBuffer.Clear();
                    options.Element.GetObstacles(buffer);
                    var blockingObstacle = options.Element.GetLineOfSightObstruction(tail[i], buffer.ReadableBuffer, CastingMode.Precise);
                    if (blockingObstacle == null)
                    {
                        for (var j = 0; j < i + 1; j++)
                        {
                            if (tail.Count > 1)
                            {
                                var x = tail[0].Left + (tail[0].Width - options.Element.Bounds.Width) / 2f;
                                var y = tail[0].Top + (tail[0].Height - options.Element.Bounds.Height) / 2f;
                                if (options.Element.TryMoveTo(x, y))
                                {
                                    options.Velocity.Angle = tail[0].CalculateAngleTo(tail[1]);
                                }
                                tail.RemoveAt(0);
                            }
                        }
                        return;
                    }
                }
            }
        }
        finally
        {
            ObstacleBufferPool.Instance.Return(buffer);
        }
    }

   
    public void ShowPath(int z = -5000)
    {
#if DEBUG
        for (var i = 0; i < tail.Count; i++)
        {
            var el = tail[i];
            var display = Game.Current.GamePanel.Add(new ConsoleControl() { Background = RGB.Green });
            display.MoveTo(el.Left, el.Top, int.MaxValue);
            display.ResizeTo(el.Width, el.Height);
            EarliestOf(this, tail.GetMembershipLifetime(el)).OnDisposed(display.Dispose);
        }
#endif
    }
}