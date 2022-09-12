namespace klooie.Gaming;

public class TextCollider : GameCollider
{
    private ConsoleString _content;
    public ConsoleString Content { get => _content; set => SetHardIf(ref _content, value, _content != value); }

    public TextCollider(ConsoleString content)
    {
        this.TransparentBackground = true;
        this.Content = content;
        Sync(nameof(Content), () => ResizeTo(Content.Length, 1), this);
    }

    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(Content, 0, 0);
}

public class NoCollisionTextCollider : TextCollider
{
    public NoCollisionTextCollider(ConsoleString content) : base(content) { }
    public override bool CanCollideWith(GameCollider other) => false;
}
