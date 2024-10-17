using ScrollSucker;

var splash = new SplashScreen();
splash.Run();
Console.Clear();

var levels = LevelLoader.LoadLevels();
var levelIndex = 0;
var result = Menu.Show();

if(result == MenuResult.Edit)
{
    new LevelEditor(levels, LevelLoader.LevelsDir).Run();
    return;
}
using (var playLifetime = new Lifetime())
{
    var sound = new ScrollSucker.ScrollSuckerSoundEngine();
    sound.Loop("FlowinNMowin", playLifetime);
    while (true)
    {
        var engine = new Runtime(levels[levelIndex]);
        engine.Lost.SubscribeOnce(engine.Stop);
        engine.Won.SubscribeOnce(() => { levelIndex++; engine.Stop(); });
        engine.Run();
        if (levelIndex >= levels.Length) break;
    }
}

