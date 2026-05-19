namespace klooie;

public interface IConsoleAppRunner
{
    Task RunAsync(ConsoleApp app);
}

public static class ConsoleAppRunner
{
    private static readonly AsyncLocal<IConsoleAppRunner?> CurrentRunner = new();
    private static readonly IConsoleAppRunner BlockingRunner = new BlockingConsoleAppRunner();

    public static Task RunAsync(ConsoleApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return (CurrentRunner.Value ?? BlockingRunner).RunAsync(app);
    }

    public static IDisposable Use(IConsoleAppRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);

        var previous = CurrentRunner.Value;
        CurrentRunner.Value = runner;
        return new RestoreRunner(previous);
    }

    private sealed class BlockingConsoleAppRunner : IConsoleAppRunner
    {
        public Task RunAsync(ConsoleApp app)
        {
            app.Run();
            return Task.CompletedTask;
        }
    }

    private sealed class RestoreRunner : IDisposable
    {
        private readonly IConsoleAppRunner? previous;
        private bool disposed;

        public RestoreRunner(IConsoleAppRunner? previous)
        {
            this.previous = previous;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CurrentRunner.Value = previous;
        }
    }
}
