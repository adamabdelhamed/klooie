namespace klooie.Gaming;

public class TextCollider : GameCollider
{
    public ConsoleString Content { get => Get<ConsoleString>(); set => Set(value); }

    public TextCollider(ConsoleString content)
    {
        this.TransparentBackground = true;
        this.Content = content;
        Sync(nameof(Content), () => ResizeTo(Content.Length, this.Height), this);
    }

    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(Content, 0, 0);
}

public class NoCollisionTextCollider : TextCollider
{
    public NoCollisionTextCollider(ConsoleString content) : base(content) { }
    public override bool CanCollideWith(GameCollider other) => false;
}
