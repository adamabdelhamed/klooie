namespace ScrollSucker;
public class SplashScreen : ConsoleApp
{
    private bool minSizeNotMetDetected;
    private bool minSizeMetDetected;

    public SplashScreen() : base()
    {
        Invoke(() =>
        {
            PushKeyForLifetime(ConsoleKey.Escape, () => { Console.Clear(); Environment.Exit(0); }, this);
            LayoutRoot.Add(new MinimumSizeShield(new MinimumSizeShieldOptions()
            {
                MinWidth = World.ViewportWidth,
                MinHeight = World.SceneHeight,
                OnMinimumSizeMet = OnMinimumSizeMet,
                OnMinimumSizeNotMet = OnMinimumSizeNotMet
            })).Fill();
        });
    }

    private void OnMinimumSizeNotMet()
    {
        if (minSizeNotMetDetected) return;
        minSizeNotMetDetected = true;
    }

    private void OnMinimumSizeMet()
    {
        if (minSizeMetDetected) return;
        minSizeMetDetected = true;
        Stop();
    }
}
 