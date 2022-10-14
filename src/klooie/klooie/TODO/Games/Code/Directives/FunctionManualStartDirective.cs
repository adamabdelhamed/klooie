
namespace klooie.Gaming.Code;

public class FunctionManualStartDirective : EventDrivenFunctionDirective
{
    [ArgDefaultValue(false)]
    public bool MultiThreaded { get; set; } = false;

    [ArgDefaultValue(false)]
    public bool OneTime { get; set; } = false;

    protected override Task OnFunctionIdentified(Function myFunction)
    {
        Game.Current.GamePanel.Add(new FunctionManualStarter(this));
        return Task.CompletedTask;
    }
}

public class FunctionManualStarter : GameCollider
{
    private FunctionManualStartDirective directive;
    private TimeSpan lastStartedTime;

    private int ActiveThreadCount => Process.Current.Threads.Where(f => f.ShouldContinue && f.Options.EntryPoint == directive.Function).Count();
    private bool CanStartBecauseDebounceTimeHasPassed => Game.Current.MainColliderGroup.Now - lastStartedTime >= TimeSpan.FromSeconds(.5);
    public bool IsEnabled => CanStartBecauseDebounceTimeHasPassed && (directive.MultiThreaded || ActiveThreadCount == 0);

    public FunctionManualStarter(FunctionManualStartDirective directive)
    {
        this.directive = directive;
        CompositionMode = CompositionMode.BlendBackground;
        var code = Game.Current.GamePanel.Controls
                .WhereAs<CodeControl>()
                .Where(c => c.Token?.Function == directive.Function)
                .OrderBy(c => c.Token.Line)
                .ThenBy(c => c.Token.Column)
                .First();

        this.ResizeTo(3, 3);
        this.MoveTo(code.Left - Width, code.Top - 1);
        this.Velocity.OnCollision.Subscribe(OnCollision, this);
    }

    private void OnCollision(Collision c)
    {
        if (c.Angle.Value > 90 && c.Angle.Value < 270 && IsEnabled)
        {
            lastStartedTime = Game.Current.MainColliderGroup.Now;
            var lt = directive.Function.Execute();
            lt.OnDisposed(() =>
            {
                FirePropertyChanged(nameof(Bounds));
            });
            FirePropertyChanged(nameof(Bounds));
            if(directive.OneTime)
            {
                this.Dispose();
            }
        }
    }

    private static readonly ConsoleCharacter EnabledButtonPen = new ConsoleCharacter(' ', backgroundColor: RGB.Red);
    private static readonly ConsoleCharacter DisabledButtonPen = new ConsoleCharacter(' ', backgroundColor: RGB.DarkRed);
    private static readonly ConsoleCharacter Rod = new ConsoleCharacter(' ', backgroundColor: RGB.White);

    protected override void OnPaint(ConsoleBitmap context)
    {
        if (IsEnabled)
        {
            context.DrawLine(Rod, 0, 1, Width, 1);
            context.DrawLine(EnabledButtonPen, 0, 0, 0, Height);
        }
        else
        {
            context.DrawLine(DisabledButtonPen, Width - 1, 0, Width - 1, Height);
        }
    }
}

