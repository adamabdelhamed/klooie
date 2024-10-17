namespace ScrollSucker;

public enum MenuResult
{
    Play,
    Edit,
    Exit
}
public class Menu : ConsoleApp
{
    public Event PlaySelected { get; private set; } = new Event();
    public Event EditSelected { get; private set; } = new Event();

    private Menu() { }

    protected override async Task Startup()
    {
        LayoutRoot.Add(new ConsoleControl() { Width = 200, Height = 3, Background = RGB.Green }).CenterBoth();
        LayoutRoot.Add(new Label() { Foreground = RGB.Black, CompositionMode = CompositionMode.BlendBackground, Text = "Press enter to play".ToConsoleString() }).CenterBoth();

        PushKeyForLifetime(ConsoleKey.Enter, () => PlaySelected.Fire() ,this);
        PushKeyForLifetime(ConsoleKey.E, ConsoleModifiers.Alt, () => EditSelected.Fire(), this);
    }

    public static MenuResult Show()
    {
        var menu = new Menu();
        var ret = MenuResult.Exit;
        menu.PlaySelected.SubscribeOnce(() =>
        {
            ret = MenuResult.Play;
            menu.Stop();
        });
        menu.EditSelected.SubscribeOnce(() =>
        {
            ret = MenuResult.Edit;
            menu.Stop();
        });

        menu.Run();
        return ret;
    }
}

