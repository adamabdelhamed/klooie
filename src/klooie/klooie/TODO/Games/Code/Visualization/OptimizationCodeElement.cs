using PowerArgs;
namespace klooie.Gaming.Code;
public class OptimizationCodeElement : CodeControl
{
    public ConsoleString Code { get; private set; }
    public override ConsoleString LineOfCode => Code;

    public OptimizationCodeElement(ConsoleString code) : base(null)
    {
        this.MoveTo(Left, Top);
        this.Code = code;
        this.ResizeTo(code.Length, 1);
    }

    public override string ToString() => $"Optimization: '{Code.StringValue}'";
    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(Code, 0, 0);
}

