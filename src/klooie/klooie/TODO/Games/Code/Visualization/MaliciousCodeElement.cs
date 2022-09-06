namespace klooie.Gaming.Code;

public enum MaliciousCodeHint
{
    NonSpecific,
    Comment,
}

public class MaliciousCodeElement : CodeControl
{
    public bool IsBeingTargeted { get; set; }
    public MaliciousCodeHint Hint { get; set; }

    public string Code { get; private set; }

    public Event Damaged { get; private set; } = new Event();

    public Event Destroyed { get; private set; } = new Event();

    public float HealthPoints { get; set; } = 1;

    public MaliciousCodeElement(string code) : base(null)
    {
        this.MoveTo(Left, Top);
        this.Code = code;
        this.ResizeTo(code.Length, 1);

        Game.Current.Invoke(async () =>
        {
            while (this.IsExpired == false)
            {
                Evaluate();
                await Task.Yield();
            }
        });
    }

    public override ConsoleString LineOfCode => Code.ToRed();

    private void Evaluate()
    {

        if (Hint != MaliciousCodeHint.Comment && this.HasSimpleTag("enemy") == false)
        {
            this.AddTag("enemy");
            this.AddTag(DamageDirective.DamageableTag);
        }
        else if (Hint == MaliciousCodeHint.Comment && this.HasSimpleTag("enemy"))
        {
            this.RemoveTag("enemy");
            this.RemoveTag(DamageDirective.DamageableTag);
        }

        IsBeingTargeted = MainCharacter.Current != null && MainCharacter.Current.Target == this;
    }

    public override string ToString() => $"MALICIOUS: '{Code}'";

    protected override void OnPaint(ConsoleBitmap context)
    {
        var str = Code.ToRed();
        if (Hint == MaliciousCodeHint.Comment)
        {
            str = str.ToDarkGreen();
        }

        if (IsBeingTargeted)
        {
            str = str.ToBlack().ToDifferentBackground(ConsoleColor.Cyan);
        }

        context.DrawString(str, 0, 0);
    }
}