namespace klooie;
using PowerArgs;
public class Friction2 : Lifetime
{
    public const int DefaultFrictionEvalFrequency = 50;
    public float Decay { get; set; } = .9f;

    private Velocity tracker;
    public Friction2(Velocity tracker, float evalFrequency = DefaultFrictionEvalFrequency)
    {
        this.tracker = tracker;
        tracker.Collider.OnDisposed(this.Dispose);

        ConsoleApp.Current.Invoke(async () =>
        {
            while (this.IsExpired == false)
            {
                tracker.Speed *= Decay;
                if (tracker.Speed < .1f) tracker.Speed = 0;
                await Task.Delay((int)evalFrequency);
            }
        });
    }
}
