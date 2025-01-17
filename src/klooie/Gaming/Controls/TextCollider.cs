namespace klooie.Gaming;

public partial class TextCollider : GameCollider
{
    public partial ConsoleString Content { get; set; }

    public TextCollider() : base(false) { }

    public TextCollider(ConsoleString content, bool connectToMainColliderGroup = true) : base(connectToMainColliderGroup)
    {
        this.Content = content;
    }

    protected override void ProtectedInit()
    {
        base.ProtectedInit();
        ContentChanged.Sync(() => ResizeTo(Content?.Length ?? 0, 1), this);
    }

    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(Content, 0, 0);
}

public class NoCollisionTextCollider : TextCollider
{
    public NoCollisionTextCollider(ConsoleString content, bool connectToMainColliderGroup = true) : base(content, connectToMainColliderGroup) { }
    public override bool CanCollideWith(ICollidable other) => false;
}
