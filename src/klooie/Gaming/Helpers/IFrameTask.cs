namespace klooie.Gaming;

public interface IFrameTask : ILifetime
{
    string Name { get; }
    TimeSpan LastExecutionTime { get; set; }
    void Execute();
}
