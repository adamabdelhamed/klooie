namespace klooie.Gaming;

public class TextCollider : GameCollider
{

    private ConsoleString content;
    public ConsoleString Content
    {
        get => content;
        set
        {
            if (content != value)
            {
                content = value;
                ResizeMe();
            }
        }
    }

    public TextCollider() : base(false) { }

    public TextCollider(ConsoleString content, bool connectToMainColliderGroup = true) : base(connectToMainColliderGroup)
    {
        this.Content = content;
    }

    private void ResizeMe() => ResizeTo(Content?.Length ?? 0, 1);
    
    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(Content, 0, 0);

    protected override void OnReturn()
    {
        base.OnReturn();
        content = null;
    }
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