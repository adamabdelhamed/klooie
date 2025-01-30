namespace klooie.Gaming;

public static class Debugger
{
    public static void HighlightCell(int x, int y, ILifetime duration, ConsoleCharacter? pen = null, int z = int.MaxValue)
    {
        pen = pen.HasValue ? pen.Value : new ConsoleCharacter('H', RGB.Black, RGB.White);
        var el = Game.Current.GamePanel.Add(new TextCollider(new ConsoleString(new ConsoleCharacter[] { pen.Value })));
        el.MoveTo(x, y, z);
        duration.OnDisposed(el.Dispose);
    }
}
