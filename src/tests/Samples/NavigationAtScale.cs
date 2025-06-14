﻿using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Threading.Tasks;

namespace klooie.tests;
/*
public class NavigationAtScaleGame : Game
{
    private const int Margin = 6;
    private const int Spray = 15;
    private const int CharacterSpeed = 100;
    private Camera camera;
    private Random random;
    protected override async Task Startup()
    {
        await base.Startup();
        random = new Random();

        camera = LayoutRoot.Add(new Camera() { Background = new RGB(10, 20, 10) }).Fill();
        var center = new LocF(0, 0);
        camera.BigBounds = center.ToRect(400, 400);
        await camera.FadeIn();
        PlaceBackgroundTexture();

        var topLeftArea = GameBounds.TopLeft.RadialOffset(45, Spray * 2 + Margin * 2);
        var bottomRightArea = GameBounds.BottomRight.RadialOffset(225, Spray * 2 + Margin * 2);

        camera.PointAt(topLeftArea);
        var greenTask = PlaceClusterOfCharacters(topLeftArea, RGB.Green, 8);
        var redTask = PlaceClusterOfCharacters(bottomRightArea, RGB.Red, 8);

        await Task.WhenAll(greenTask, redTask);

        var greenCharacters = greenTask.Result;
        var redCharacters = redTask.Result;

        SendCharactersTo(greenCharacters, bottomRightArea);
        SendCharactersTo(redCharacters, topLeftArea);

        new CameraOperator(camera, greenCharacters[0], greenCharacters[0].Velocity, this, new AlwaysCenteredMovement());
        await Delay(10000);
    }


    private void PlaceBackgroundTexture()
    {
        for(var x = Margin; x < GameBounds.Width - Margin; x+=20)
        {
            for (var y = Margin/2f; y < GameBounds.Height - Margin/2f; y+=10)
            {
                GamePanel.Add(new Label("#".ToConsoleString(new RGB(40, 40, 40))) { CompositionMode = CompositionMode.BlendBackground }).MoveTo(GameBounds.Left + x,GameBounds.Top + y);
            }
        }
    }

    private async Task<GameCollider[]> PlaceClusterOfCharacters(LocF near, RGB color, int count)
    {
        var ret = new GameCollider[count];
        for(var i = 0; i < count; i++)
        {
            var nextX = near.Left + random.Next(-Spray, Spray);
            var nextY = near.Top + random.Next(-Spray/2, Spray/2);
            var character = GamePanel.Add(new GameCollider() { Background = color });
            character.MoveTo(nextX, nextY);
            character.NudgeFree();
            ret[i] = character;
            await Task.Delay(50);
        }
        return ret;
    }

    private void SendCharactersTo(GameCollider[] characters, LocF goal)
    {
        foreach(var character in characters)
        {
            var vision = Vision.Create(character);
            var destination = new GameCollider(new RectF(goal.Left, goal.Top, 1, 1));
            Invoke(()=>Mover.Invoke(Navigate.Create(new NavigateOptions()
            {
                Speed = ()=>CharacterSpeed,
                Velocity = character.Velocity,
                Vision = vision,
                Destination = () => destination,

            })));
        }
    }

    public override ConsolePanel GamePanel => camera;
    public override RectF GameBounds => camera.BigBounds;
}
*/