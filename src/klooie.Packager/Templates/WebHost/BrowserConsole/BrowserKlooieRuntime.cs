using klooie.blazor.Hosting;
using Microsoft.JSInterop;

namespace klooie.blazor.BrowserConsole;

public sealed class BrowserKlooieRuntime : IDisposable
{
    private readonly BrowserKlooieTerminalHost host;
    private readonly Recyclable subscriptionLifetime;
    private readonly int subscriptionLifetimeLease;
    private readonly Task entryTask;
    private ConsoleApp? app;
    private Exception? entryException;
    private bool disposed;

    public BrowserKlooieRuntime(KlooieBlazorAppRegistration registration, IJSRuntime js, HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(registration);

        BrowserKlooieTerminalHost.InitConsoleProvider();
        FrameBuffer = new BrowserConsoleFrameBuffer(80, 25);
        host = new BrowserKlooieTerminalHost(FrameBuffer);
        subscriptionLifetime = DefaultRecyclablePool.Instance.Rent(out subscriptionLifetimeLease);
        ConsoleApp.Starting.Subscribe(this, static me => me.BindStartingApp(), subscriptionLifetime);
        entryTask = RunEntryPointAsync(registration, js, http);
    }

    public BrowserConsoleFrameBuffer FrameBuffer { get; }

    public BrowserConsoleFrame Tick(int width, int height, TimeSpan budget)
    {
        if (disposed) return BrowserConsoleFrame.Empty;
        if (entryException is not null) throw entryException;

        host.Resize(width, height);
        var currentApp = app;
        if (currentApp is not null)
        {
            currentApp.Tick(budget);
        }

        return FrameBuffer.ToFrame();
    }

    public void EnqueueKey(ConsoleKeyInfo key)
    {
        if (disposed) return;
        host.EnqueueKey(key);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        app?.StopCooperative();
        app?.TryDispose(app.Lease, "BrowserKlooieRuntime disposed");
        subscriptionLifetime.TryDispose(subscriptionLifetimeLease, "BrowserKlooieRuntime disposed");
    }

    private async Task RunEntryPointAsync(KlooieBlazorAppRegistration registration, IJSRuntime js, HttpClient http)
    {
        try
        {
            BinaryAssetProvider.Current = await BrowserAssetProvider.CreateAsync(http);
            SoundProvider.Current = new BrowserSoundProvider(js);

            using (ConsoleAppRunner.Use(new BrowserConsoleAppRunner(this)))
            {
                await registration.RunAsync();
            }
        }
        catch (Exception ex)
        {
            entryException = ex;
        }
    }

    private void BindStartingApp()
    {
        if (ConsoleApp.Current is not null) BindApp(ConsoleApp.Current);
    }

    private void BindApp(ConsoleApp app)
    {
        this.app = app;
        if (app.LayoutRoot is not LayoutRootPanel root) return;

        root.TerminalHost = host;
        host.SyncSize(root);
    }

    private sealed class BrowserConsoleAppRunner : IConsoleAppRunner
    {
        private readonly BrowserKlooieRuntime runtime;

        public BrowserConsoleAppRunner(BrowserKlooieRuntime runtime)
        {
            this.runtime = runtime;
        }

        public async Task RunAsync(ConsoleApp app)
        {
            var stopped = app.LoopStopped.CreateNextFireTask();
            app.StartCooperative();
            runtime.BindApp(app);

            try
            {
                await stopped;
            }
            finally
            {
                if (ReferenceEquals(runtime.app, app)) runtime.app = null;
                app.TryDispose(app.Lease, "Browser console app run completed");
            }
        }
    }
}
