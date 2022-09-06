using klooie.Gaming.Code;
namespace klooie.Gaming;

public class MPDirective : Directive
{
    private static MPDirective Current { get; set; }
    private Event changed = new Event();

    public static Event Changed => Current.changed;

    public override Task ExecuteAsync()
    {
        if (Current != null) throw new NotSupportedException("There can be only 1 MPDirective");
        Current = this;
        Game.Current.OnDisposed(() => Current = null);
        return Task.CompletedTask;
    }

    public static float Get() => Game.Current.RuleVariables.Get<float>("MP");

    public static void Add(float amount) => Set(Get() + amount);

    public static void Set(float newMP)
    {
        if (newMP < 0) throw new InvalidOperationException("MP cannot be negative");

        Game.Current.RuleVariables.Set(newMP, "MP");
        Current.changed.Fire();
    }
}

