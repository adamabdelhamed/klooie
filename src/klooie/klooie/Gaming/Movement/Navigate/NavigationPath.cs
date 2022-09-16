namespace klooie.Gaming;
public class NavigationPath : Lifetime
{
#if DEBUG
    private ObservableCollection<RectF> tail;
#else
    private List<RectF> tail;
#endif
    private Navigate options;
    private TimeSpan lastProgressTime = Game.Current.MainColliderGroup.Now;

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
    public bool IsReallyStuck => Game.Current.MainColliderGroup.Now - lastProgressTime > TimeSpan.FromSeconds(7);

    public GameCollider FindLocalTarget() => tail.Count > 0 ? new ColliderBox(tail[0]) : null;
    

    public void PruneTail()
    {
        for (var i = tail.Count - 1; i >= 0; i--)
        {
            var curr = tail[i];
           var d = options.Element.Bounds.CalculateNormalizedDistanceTo(curr);
            if (d <= options.Options.CloseEnough)
            {
                lastProgressTime = Game.Current.MainColliderGroup.Now;

                for (var j = 0; j < i + 1; j++)
                {
                    if (tail.Count > 1)
                    {
                        tail.RemoveAt(0);
                    }
                }
                return;
            }
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