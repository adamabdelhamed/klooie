namespace klooie.Gaming;

public partial class TextCollider : GameCollider
{
    public partial ConsoleString Content { get; set; }

    public TextCollider(ConsoleString content, ColliderGroup group = null) : base(group)
    {
        this.Content = content;
        ContentChanged.Sync(() => ResizeTo(Content.Length, 1), this);
    }

    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(Content, 0, 0);
}

public class NoCollisionTextCollider : TextCollider
{
    public NoCollisionTextCollider(ConsoleString content, ColliderGroup group = null) : base(content, group) { }
    public override bool CanCollideWith(GameCollider other) => false;
}
