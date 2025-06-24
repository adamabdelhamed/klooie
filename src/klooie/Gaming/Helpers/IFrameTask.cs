namespace klooie.Gaming;

public interface IFrameTask : ILifetime
{
    string Name { get; }
    void Execute();
}
