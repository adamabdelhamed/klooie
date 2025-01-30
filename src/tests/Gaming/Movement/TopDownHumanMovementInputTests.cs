using klooie;
using klooie.Gaming;
using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;

namespace tests.Gaming;
[TestClass]
public class TopDownHumanMovementInputTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void WASDTurning() => GamingTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
    {
        var game = Game.Current;
        var angleLabel = game.GamePanel.Add(new Label() { Text = "Angle: 0".ToWhite() }).DockToTop(padding: 1).DockToRight(padding: 2);

        var player = game.GamePanel.Add(new GameCollider() { Background = RGB.Green });
        player.Velocity.Angle = Angle.Right;
        player.Velocity.OnAngleChanged.Subscribe(() => angleLabel.Text = ("Angle: " + player.Velocity.Angle.ToString()).ToWhite(), player);
        player.MoveTo(game.GameBounds.Center);

        var movement = new TopDownHumanMovementInput();
        movement.Bind(player);

        Assert.AreEqual(Angle.Right, player.Velocity.Angle);
        Assert.AreEqual(0, player.Velocity.Speed);
        await game.SendKey(ConsoleKey.W.KeyInfo());
        Assert.AreEqual(Angle.Up, player.Velocity.Angle);
        Assert.AreEqual(0, player.Velocity.Speed);
        await game.SendKey(ConsoleKey.S.KeyInfo());
        Assert.AreEqual(Angle.Down, player.Velocity.Angle);
        Assert.AreEqual(0, player.Velocity.Speed);
        await game.SendKey(ConsoleKey.A.KeyInfo());
        Assert.AreEqual(Angle.Left, player.Velocity.Angle);
        Assert.AreEqual(0, player.Velocity.Speed);
        await game.SendKey(ConsoleKey.D.KeyInfo());
        Assert.AreEqual(Angle.Right, player.Velocity.Angle);
        Assert.AreEqual(0, player.Velocity.Speed);

        await game.SendKey(ConsoleKey.D.KeyInfo());
        Assert.AreEqual(Angle.Right, player.Velocity.Angle);
        Assert.AreEqual(movement.Speed, player.Velocity.Speed);

        player.Velocity.Stop();

        movement.MovementFilter.SubscribeOnce(ctx => ctx.Suppress());
        await game.SendKey(ConsoleKey.D.KeyInfo());
        Assert.AreEqual(Angle.Right, player.Velocity.Angle);
        Assert.AreEqual(0, player.Velocity.Speed);

        await game.SendKey(ConsoleKey.D.KeyInfo());
        Assert.AreEqual(Angle.Right, player.Velocity.Angle);
        Assert.AreEqual(movement.Speed, player.Velocity.Speed);

        game.Stop();
    });
}
