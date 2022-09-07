using klooie;
using klooie.Gaming;
using klooie.tests;
using PowerArgs;

var ev = new Event<LocF>();
Character c = null;
var factory = () =>
{
    c = new Character();
    c.Sync(nameof(c.Bounds), () => ev.Fire(c.Center()), c);
    return c;
};
var game = new TestGame(new GamingTestOptions()
{
    Camera = true,
    FocalPointChanged = ev,
});
game.Invoke(async () =>
{
    Game.Current.GamePanel.Background = new RGB(20, 20, 20);
    await new NavigateTests().NavigateTest(100, true, factory);
    Game.Current.Stop();
});
game.Run();
