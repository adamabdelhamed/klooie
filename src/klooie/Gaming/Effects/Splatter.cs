using klooie.Gaming;
using klooie;
using Microsoft.CodeAnalysis;

namespace CLIborg;

public enum SplatterEffect
{
    Blast,
    Slice
}

public class SplatterOptions : Recyclable
{
    public List<SplatterElement> Splatter { get; private set; } = new List<SplatterElement>();
    public LocF Location { get; set; }
    public float Speed { get; set; } = 100;
    public Angle ImpactAngle { get; set; }

    public SplatterEffect Effect { get; set; }
    public string? Sound { get; set; }
 

    public bool Break { get; set; } = true;
    public float SplatterTime { get; set; } = 900;

    public IDelayProvider DelayProvider { get; set; } = Game.Current;
    public ConsolePanel GamePanel { get; set; } = Game.Current.GamePanel;

    internal int FinishedCount { get; set; }

 

    protected override void OnInit()
    {
        base.OnInit();
        Splatter.Clear();
        FinishedCount = 0;
    }
}

public static class Splatter
{
    public static int MaxConcurrentSplatter { get; set; } = 20;

    public static bool TrySplatter(SplatterOptions options)
    {
        if (MaxConcurrentSplatter == 0) return false;
        if(options.Splatter.Count == 0) throw new ArgumentException("Splatter must have at least one element");

        var splatterCount = 0;
        for (int i = 0;i < Game.Current.GamePanel.Controls.Count; i++)
        {
            if (Game.Current.GamePanel.Controls[i] is SplatterElement)
            {
                splatterCount++;
            }
        }

        if (splatterCount >= MaxConcurrentSplatter) return false;

        var maxSplatterToAddOnThisTry = MaxConcurrentSplatter - splatterCount;

        Game.Current.Sound.Play(options.Sound);
        AddSplatterToGame(options, maxSplatterToAddOnThisTry);
        return true;

    }

    public static bool TryEjectShell(LocF location, Angle angle, ColliderGroup splatterGroup)
    {
        var options = SplatterOptionsPool.Instance.Rent();
        var shell = SplatterElementPool.Instance.Rent();
        shell.Bind(new ConsoleCharacter('o', RGB.Yellow), splatterGroup);

        options.Effect = SplatterEffect.Blast;
        options.ImpactAngle = RaiseIfHorizontal(angle);
        options.Location = location;
        options.Break = false;
        options.SplatterTime = 200;
        options.Speed = 70;
        options.Sound = "shell";
        options.Splatter.Add(shell);

        return TrySplatter(options);
    }


    private static Angle RaiseIfHorizontal(Angle a)
    {
        var rounded = a.RoundAngleToNearest(90);
        if (rounded == Angle.Up || rounded == Angle.Down) return a;
        return rounded == Angle.Right ? a.Add(-15) : a.Add(15);
    }

    public static void CreateSplatter(ConsoleControl element, ColliderGroup splatterGroup, List<SplatterElement> buffer, RGB? color)
    {
        for (var x = 0; x < element.Bitmap.Width; x++)
        {
            for (var y = 0; y < element.Bitmap.Height; y++)
            {
                var p = element.Bitmap.GetPixel(x, y);
                if (p.BackgroundColor != RGB.Black && element.CompositionMode != CompositionMode.BlendBackground)
                {
                    var splatter = SplatterElementPool.Instance.Rent();
                    splatter.Bind(new ConsoleCharacter(p.Value, color.HasValue ? color.Value : p.ForegroundColor, p.BackgroundColor), splatterGroup);
                    buffer.Add(splatter);
                }
                else
                {
                    var splatter = SplatterElementPool.Instance.Rent();
                    splatter.Bind(new ConsoleCharacter(p.Value, color.HasValue ? color.Value : p.ForegroundColor), splatterGroup);
                    buffer.Add(splatter);
                }
            }
        }

        if (buffer.None())
        {
            var splatter = SplatterElementPool.Instance.Rent();
            splatter.Bind(new ConsoleCharacter(' ', ConsoleString.DefaultForegroundColor, color.HasValue ? color.Value : RGB.Red), splatterGroup);
            buffer.Add(splatter);
        }
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

    private static ConsoleCharacter[] Break(ConsoleCharacter c, int count)
    {
        var ret = new ConsoleCharacter[count];
        if (SplatterMap.TryGetValue(c.Value, out char[] parts) == false)
        {
            parts = new char[] { '.', ',' };
        }

        var partIndex = 0;
        for (var i = 0; i < count; i++)
        {
            ret[i] = new ConsoleCharacter(parts[partIndex++], c.ForegroundColor, c.BackgroundColor);

            if (partIndex >= parts.Length)
            {
                partIndex = 0;
            }
        }
        return ret;
    }


  

    private static void AddSplatterToGame(SplatterOptions options, int maxSplatterToAddOnThisTry)
    {
        var maxAngleDiff = 10;
        var toAdd = Math.Min(options.Splatter.Count, maxSplatterToAddOnThisTry);
        for (var i = 0; i < options.Splatter.Count; i++)
        {
            if (i >= toAdd)
            {
                for (var j = i; j < options.Splatter.Count; j++)
                {
                    options.Splatter[j].Dispose();
                }
                break;
            }
            else
            {
                options.Splatter[i].MoveTo(options.Location.Left, options.Location.Top, -1);
                options.Splatter[i].MoveByRadial(options.ImpactAngle, 2);
                options.GamePanel.Add(options.Splatter[i]);
            }

            var splatterEl = options.Splatter[i];
            splatterEl.AddTag("Splatter");
            var velocity = splatterEl.Velocity;

            velocity.Angle = options.Effect == SplatterEffect.Slice ? i % 2 == 1 ? options.ImpactAngle.Add(90) : options.ImpactAngle.Add(-90) :
                options.ImpactAngle;

            var angleDiff = PseudoRandom.Next(-maxAngleDiff * (i + 1), maxAngleDiff * (i + 1));
            velocity.Angle = velocity.Angle.Add(angleDiff);
            velocity.Speed = options.Speed;
            var friction = FrictionPool.Instance.Rent();
            friction.Bind(velocity);
            var state = FinishSplatterStatePool.Instance.Rent();
            state.Options = options;
            state.SplatterElement = splatterEl;
            state.Velocity = velocity;
            state.Friction = friction;
            Finish(state);
        }
    }

    private static void Finish(FinishSplatterState finishState)
    {
        ConsoleApp.Current.InnerLoopAPIs.Delay(finishState.Options.SplatterTime, finishState, Step1);
    }

    private static void Step1(object stateObj)
    {
        var state = (FinishSplatterState)stateObj;

        state.Velocity.Stop();
        state.Friction.TryDispose();

        if (state.SplatterElement == null) return;
        
        state.SplatterElement.MoveTo(state.SplatterElement.Left, state.SplatterElement.Top, 232323);
        state.Velocity.Angle = PseudoRandom.Next(80, 100);
        state.Velocity.Speed = 25;

        ConsoleApp.Current.InnerLoopAPIs.Delay(50, state, Step2);        
    }

    private static void Step2(object stateObj)
    {
        var state = (FinishSplatterState)stateObj;
        if (state.SplatterElement == null) return;
        state.Velocity.Speed = 50;
        ConsoleApp.Current.InnerLoopAPIs.Delay(50, state, Step3);
    }

    private static void Step3(object stateObj)
    {
        var state = (FinishSplatterState)stateObj;
        if (state.SplatterElement == null) return;
        state.Velocity.Speed = 100;
        ConsoleApp.Current.InnerLoopAPIs.Delay(50, state, Step4);
    }

    private static void Step4(object stateObj)
    {
        var state = (FinishSplatterState)stateObj;
        if (state.SplatterElement == null) return;
        state.Velocity.Speed = 200;
        ConsoleApp.Current.InnerLoopAPIs.Delay(2000, state, FinalStep);
    }

    private static void FinalStep(object stateObj)
    {
        var state = (FinishSplatterState)stateObj;
        if (state.SplatterElement == null) return;
        state.SplatterElement.TryDispose();
        state.SplatterElement = null;
        state.Options.FinishedCount++;

        if (state.Options.FinishedCount == state.Options.Splatter.Count)
        {
            state.Options.TryDispose();
            state.TryDispose();
        }
    }
}

public class FinishSplatterState : Recyclable
{
    public SplatterOptions Options;
    public SplatterElement? SplatterElement;
    public Velocity Velocity;
    public Friction Friction;
    protected override void OnInit()
    {
        base.OnInit();
        Options = null!;
        SplatterElement = null!;
        Velocity = null!;
        Friction = null!;
    }
}

public class SplatterElement : CharCollider
{
    public override bool CanCollideWith(ICollidable other) => false;

    public void Bind(ConsoleCharacter content, ColliderGroup splatterGroup)
    {
        this.Content = content;
        ConnectToGroup(splatterGroup);
        CompositionMode = CompositionMode.BlendBackground;
    }
}

public static class PseudoRandom
{
    private static Random r = new Random();

    public static double NextDouble() => r.NextDouble();

    public static bool NextBool() => NextDouble() < .5f;

    public static int Next(int min, int exclusiveMax) => r.Next(min, exclusiveMax);

}