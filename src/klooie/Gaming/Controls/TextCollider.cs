namespace klooie.Gaming;

public partial class TextCollider : GameCollider
{
    public partial ConsoleString Content { get; set; }

    public TextCollider() : base(false) { }

    public TextCollider(ConsoleString content, bool connectToMainColliderGroup = true) : base(connectToMainColliderGroup)
    {
        this.Content = content;
    }

    protected override void OnInit()
    {
        base.OnInit();
        ContentChanged.Sync(() => ResizeTo(Content?.Length ?? 0, 1), this);
    }

    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(Content, 0, 0);
}

public partial class CharCollider : GameCollider
{
    public partial ConsoleCharacter Content { get; set; }

    public CharCollider() : base(false) { }

    public CharCollider(ConsoleCharacter content, bool connectToMainColliderGroup = true) : base(connectToMainColliderGroup)
    {
        this.Content = content;
    }

    protected override void OnPaint(ConsoleBitmap context) => context.DrawPoint(Content, 0, 0);
}