namespace klooie;
public class Friction : Lifetime
{
    public const int DefaultFrictionEvalFrequency = 50;
    public float Decay { get; set; } = .9f;

    public Friction(Velocity tracker, float evalFrequency = DefaultFrictionEvalFrequency)
    {
        tracker.Collider.OnDisposed(this.Dispose);

        ConsoleApp.Current.Invoke(async () =>
        {
            while (ShouldContinue)
            {
                tracker.Speed *= Decay;
                if (tracker.Speed < .1f) tracker.Speed = 0;
                await Task.Delay((int)evalFrequency);
            }
        });
    }
}
