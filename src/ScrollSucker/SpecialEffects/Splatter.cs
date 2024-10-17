namespace ScrollSucker;

public class SplatterAttribute : Attribute
{
    public string Sound { get; private set; } = "splat";
    public SplatterEffect Effect { get; private set; }
    public SplatterAttribute(SplatterEffect effect, string sound = null)
    {
        Effect = effect;
        Sound = sound ?? Sound;
    }
}

 
public enum SplatterEffect
{
    Blast,
    Slice
}

public class SplatterOptions
{
    public int? MinSplatterAmount { get; set; }
    public List<SplatterElement> Splatter { get; set; }
    public LocF? Location { get; set; }
    public float Speed { get; set; } = 100;
    public Angle ImpactAngle { get; set; }

    public SplatterEffect Effect { get; set; }

    public int ZIndex { get; set; }
    public string Sound { get; set; }
    public Character Holder { get; set; }

    public bool Break { get; set; } = true;
    public float SplatterTime { get; set; } = 200;

    public RGB? SplatterColor { get; set; }  

    public IDelayProvider DelayProvider { get; set; } = Game.Current;
    public ConsolePanel GamePanel { get; set; } = Game.Current.GamePanel;
    public ColliderGroup ColliderGroup { get; set; } = Game.Current.MainColliderGroup;
}

public static class Splatter
{
    private static Random r = new Random();
    public static int MaxConcurrentSplatter { get; set; } = 40;

    public static void Initialize()
    {
        HP.Current.OnCharacterDestroyed.Subscribe(ev =>
        {
            var effectOverride = ev.RawArgs.Damager?.GetType().Attr<SplatterAttribute>()?.Effect;
            var soundOverride = ev.RawArgs.Damagee?.GetType().Attr<SplatterAttribute>()?.Sound;
            TrySplatter(ev.RawArgs.Damagee, new SplatterOptions()
            {
                ImpactAngle = ev.Collision.HasValue ? ev.Collision.Value.Angle : 90,
                Break = true,
                Effect = effectOverride.HasValue ? effectOverride.Value : SplatterEffect.Blast,
                Sound = soundOverride ?? "splat",
            });
        }, Game.Current);
    }

    public static bool TrySplatter(ConsoleControl element, SplatterOptions options)
    {
        if (element == null) return false;
        if (MaxConcurrentSplatter == 0) return false;
        if (Game.Current.GamePanel.Controls.WhereAs<SplatterElement>().Count() >= MaxConcurrentSplatter)
        {
            return false;
        }
        options.Location = options.Location.HasValue ? options.Location.Value : element.Center();
        options.Splatter = options.Splatter ?? CreateSplatter(element, options);
        Execute(options);
        return true;

    }

    private static List<SplatterElement> CreateSplatter(ConsoleControl element, SplatterOptions options)
    {
        var ret = new List<SplatterElement>();
        for (var x = 0; x < element.Bitmap.Width; x++)
        {
            for (var y = 0; y < element.Bitmap.Height; y++)
            {
                var p = element.Bitmap.GetPixel(x, y);
                if (p.BackgroundColor != RGB.Black && element.CompositionMode != CompositionMode.BlendBackground)
                {
                    ret.Add(new SplatterElement((p.Value + "").ToConsoleString(options.SplatterColor.HasValue ? options.SplatterColor.Value : p.ForegroundColor, p.BackgroundColor), options.ColliderGroup));
                }
                else
                {
                    ret.Add(new SplatterElement((p.Value + "").ToConsoleString(options.SplatterColor.HasValue ? options.SplatterColor.Value : p.ForegroundColor, p.Value == ' ' ? p.BackgroundColor : null), options.ColliderGroup));
                }
            }
        }

        if (ret.None())
        {
            ret.Add(new SplatterElement((" ").ToConsoleString(bg: options.SplatterColor.HasValue ? options.SplatterColor.Value : RGB.Red), options.ColliderGroup));
        }

        return ret;
    }

    private static Dictionary<char, char[]> SplatterMap = new Dictionary<char, char[]>()
    {
        { 'o', new char[]{'c', ',' } },
        { 'O', new char[]{'C', ',' } },
        { '0', new char[]{'C', ',' } },
        { '1', new char[]{'i', '.' } },
        { 'm', new char[]{'n', '.' } },
        { 'B', new char[]{'R', '.' } },
        { 'T', new char[]{'|', '-' } },
        { 'A', new char[]{'-', '/', '\\' } },
        { 'R', new char[]{'|', 'c', '\\' } },
        { 'G', new char[]{'C', '-' } },
        { 'E', new char[]{'|', 'F', '_' } },
        { 'g', new char[]{'o', ',' } },
        { 'u', new char[]{'c', ',' } },
        { 'a', new char[]{'c', '.' } },
        { 'r', new char[]{'i', '-' } },
        { 'd', new char[]{'c', '|' } },
        { 'h', new char[]{'|', 'r' } },
        { 'e', new char[]{'c', '-' } },
        { 'n', new char[]{'i', ',' } },
        { 'c', new char[]{'.', ',' } },
        { ';', new char[]{'.', ',' } },
    };

    private static ConsoleString[] Break(ConsoleCharacter c, int count)
    {
        var ret = new ConsoleString[count];
        if(c.Value == ' ')
        {
            return new ConsoleString[] { new ConsoleString(new ConsoleCharacter[] { c }) };
        }
        if (SplatterMap.TryGetValue(c.Value, out char[] parts) == false)
        {
            parts = new char[] { '.', ',' };
        }

        var partIndex = 0;
        for (var i = 0; i < count; i++)
        {
            ret[i] = ("" + parts[partIndex++]).ToConsoleString(c.ForegroundColor, c.BackgroundColor);

            if (partIndex >= parts.Length)
            {
                partIndex = 0;
            }
        }
        return ret;
    }


    private static void Execute(SplatterOptions options)
    {
        if (options.Sound != null)
        {
            Game.Current.Sound.Play(options.Sound);
        }

        PrepareSplatter(options);
        AddSplatterToGame(options);
    }

    private static void PrepareSplatter(SplatterOptions options)
    {
        while (options.MinSplatterAmount.HasValue && options.Splatter.Count < options.MinSplatterAmount.Value)
        {
            var toBreak = r.Next(0, options.Splatter.Count);
            var randomPrototype = options.Splatter[toBreak];
            options.Splatter.RemoveAt(toBreak);
            randomPrototype.Dispose();
            var broken = Break(randomPrototype.Content[0], 2);
            options.Splatter.Add(new SplatterElement(broken[0], options.ColliderGroup));
            options.Splatter.Add(new SplatterElement(broken[1], options.ColliderGroup));
        }

        if (options.Break)
        {
            for (var i = 0; i < options.Splatter.Count; i++)
            {
                var splatterToBreak = options.Splatter[i];
                var parts = Break(splatterToBreak.Content[0], 2);
                var chosenPart = parts[r.Next(0, parts.Length)];
                splatterToBreak.Content = chosenPart;
            }
        }
    }

    private static void AddSplatterToGame(SplatterOptions options)
    {
        var maxAngleDiff = 10;
        var count = options.GamePanel.Controls.WhereAs<SplatterElement>().Count();
        for (var i = 0; i < Math.Min(10,options.Splatter.Count); i++)
        {
            if (count + i >= MaxConcurrentSplatter)
            {
                for (var j = i; j < options.Splatter.Count; j++)
                {
                    options.Splatter[j].TryDispose();
                }
                break;
            }
            else
            {
                options.Splatter[i].MoveTo(options.Location.Value.Left, options.Location.Value.Top, -1);
                options.GamePanel.Add(options.Splatter[i]);
            }

            var splatterEl = options.Splatter[i];
            splatterEl.AddTag("Splatter");
            var v = splatterEl.Velocity;

            v.Angle = options.Effect == SplatterEffect.Slice ? i % 2 == 1 ? options.ImpactAngle.Add(90) : options.ImpactAngle.Add(-90) :
                options.ImpactAngle;

            var angleDiff = r.Next(-maxAngleDiff * (i + 1), maxAngleDiff * (i + 1));
            v.Angle = v.Angle.Add(angleDiff);
            v.Speed = options.Speed;
            v.OnCollision.SubscribeOnce((imp) => splatterEl.TryDispose());
            var f = new Friction(v) { Decay = .8f };
            Game.Current.Invoke(() => Finish(options, splatterEl, v, f));
        }
    }

    private static async Task Finish(SplatterOptions options, SplatterElement splatterEl, Velocity v, Friction f)
    {
        await options.DelayProvider.Delay(options.SplatterTime);

        v.Stop();
        f.Dispose();

        if (splatterEl.IsExpired == false)
        {
            splatterEl.MoveTo(splatterEl.Left, splatterEl.Top, 232323);
            v.Angle = r.Next(80, 100);
            v.Speed = 25;
            await options.DelayProvider.Delay(50);
            v.Speed = 50;
            await options.DelayProvider.Delay(50);
            v.Speed = 100;
            await options.DelayProvider.Delay(50);
            v.Speed = 200;
            await options.DelayProvider.Delay(1500);
            splatterEl.Dispose();
        }
    }
}

public class SplatterElement : NoCollisionTextCollider
{
    public SplatterElement(ConsoleString content, ColliderGroup group = null) : base(content, group)
    {
        CompositionMode = CompositionMode.BlendBackground;
    }
}
