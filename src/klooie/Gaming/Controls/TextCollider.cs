namespace klooie.Gaming;

public class TextCollider : GameCollider
{
    private ConsoleString _content;
    public ConsoleString Content { get => _content; set => SetHardIf(ref _content, value, _content != value); }

    public TextCollider(ConsoleString content, ColliderGroup group = null) : base(group)
    {
        this.Content = content;
        Sync(nameof(Content), () => ResizeTo(Content.Length, 1), this);
    }

    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(Content, 0, 0);
}

public class NoCollisionTextCollider : TextCollider
{
    public NoCollisionTextCollider(ConsoleString content, ColliderGroup group = null) : base(content, group) { }
    public override bool CanCollideWith(GameCollider other) => false;
}
