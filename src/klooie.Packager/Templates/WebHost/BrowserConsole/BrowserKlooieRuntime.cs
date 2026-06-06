using klooie.blazor.Hosting;
using Microsoft.JSInterop;
using System.Reflection;

namespace klooie.blazor.BrowserConsole;

public sealed class BrowserKlooieRuntime : IDisposable
{
    private readonly BrowserKlooieTerminalHost host;
    private readonly Recyclable subscriptionLifetime;
    private readonly int subscriptionLifetimeLease;
    private readonly Task entryTask;
    private readonly Queue<BrowserOverlayCommand> overlayCommands = new();
    private ConsoleApp? app;
    private Exception? entryException;
    private bool lastFrameHadPresentation;
    private bool entryPointCompleted;
    private bool disposed;

    public BrowserKlooieRuntime(KlooieBlazorAppRegistration registration, IJSRuntime js, HttpClient http, string? hostName)
    {
        ArgumentNullException.ThrowIfNull(registration);

        BrowserHostEnvironment.HostName = hostName ?? "";
        BrowserKlooieTerminalHost.InitConsoleProvider();
        FrameBuffer = new BrowserConsoleFrameBuffer(80, 25);
        host = new BrowserKlooieTerminalHost(FrameBuffer);
        subscriptionLifetime = DefaultRecyclablePool.Instance.Rent(out subscriptionLifetimeLease);
        ConsoleApp.Starting.Subscribe(this, static me => me.BindStartingApp(), subscriptionLifetime);
        BrowserHostEnvironment.OverlayRequested.Subscribe(this, static (me, request) => me.EnqueueOverlayCommand(request), subscriptionLifetime);
        entryTask = RunEntryPointAsync(registration, js, http);
    }

    public BrowserConsoleFrameBuffer FrameBuffer { get; }

    public BrowserConsoleFrame Tick(int width, int height, TimeSpan budget, string? gamepadSnapshotJson, bool mobileExperience, string? hostName)
    {
        if (disposed) return BrowserConsoleFrame.Empty;
        if (entryException is not null) throw entryException;

        BrowserHostEnvironment.HostName = hostName ?? "";
        BrowserHostEnvironment.IsMobileExperience = mobileExperience;
        TryUpdateBrowserGamepads(gamepadSnapshotJson);
        host.Resize(width, height);
        var currentApp = app;
        if (currentApp is not null)
        {
            currentApp.Tick(budget);
        }

        return AddFrameCommands(FrameBuffer.ToFrame(), currentApp, entryPointCompleted && app is null);
    }

    public void EnqueueKey(ConsoleKeyInfo key)
    {
        if (disposed) return;
        host.EnqueueKey(key);
    }

    private static void TryUpdateBrowserGamepads(string? gamepadSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(gamepadSnapshotJson)) return;

        try
        {
            BrowserControllerInput.UpdateGamepadsJson(gamepadSnapshotJson);
        }
        catch
        {
        }
    }

    private BrowserConsoleFrame AddFrameCommands(BrowserConsoleFrame frame, ConsoleApp? app, bool appStopped)
    {
        var touchButtonReleases = BrowserControllerInput.DrainTouchButtonReleases();
        var touchButtonHints = BrowserControllerInput.DrainTouchButtonHints();
        var overlayCommands = DrainOverlayCommands();
        var presentation = app?.Presentation is ConsoleBitmapPresentation browserPresentation ? browserPresentation.CreateFrame() : ConsoleBitmapPresentationFrame.Empty;
        var hasPresentation = ReferenceEquals(presentation, ConsoleBitmapPresentationFrame.Empty) == false;
        if (touchButtonReleases.Length == 0 && touchButtonHints.Length == 0 && overlayCommands.Length == 0 && hasPresentation == false && lastFrameHadPresentation == false && appStopped == false) return frame;

        lastFrameHadPresentation = hasPresentation;

        return new BrowserConsoleFrame
        {
            Width = frame.Width,
            Height = frame.Height,
            Full = frame.Full,
            X = frame.X,
            Y = frame.Y,
            Text = frame.Text,
            Foreground = frame.Foreground,
            Background = frame.Background,
            TouchButtonReleases = touchButtonReleases,
            TouchButtonHints = touchButtonHints,
            Presentation = presentation,
            OverlayCommands = overlayCommands,
            AppStopped = appStopped
        };
    }

    private void EnqueueOverlayCommand(BrowserOverlayRequest request)
    {
        lock (overlayCommands)
        {
            overlayCommands.Enqueue(new BrowserOverlayCommand(request.Id, request.Title, request.Message));
        }
    }

    private BrowserOverlayCommand[] DrainOverlayCommands()
    {
        lock (overlayCommands)
        {
            if (overlayCommands.Count == 0) return Array.Empty<BrowserOverlayCommand>();
            var ret = overlayCommands.ToArray();
            overlayCommands.Clear();
            return ret;
        }
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
            TryConfigureBrowserBigASCII(js);
            TryConfigureBrowserAppStorage(js);

            using (ConsoleAppRunner.Use(new BrowserConsoleAppRunner(this)))
            {
                await registration.RunAsync();
            }
        }
        catch (Exception ex)
        {
            entryException = ex;
        }
        finally
        {
            entryPointCompleted = true;
        }
    }

    private static void TryConfigureBrowserBigASCII(IJSRuntime js)
    {
        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("TotallyTextualBattleSimulator.BigASCII", throwOnError: false);
                var method = type?.GetMethod("ConfigureBrowserRasterizer", BindingFlags.Public | BindingFlags.Static, [typeof(object)]);
                if (method is null) continue;

                method.Invoke(null, [js]);
                return;
            }
        }
        catch
        {
        }
    }

    private static void TryConfigureBrowserAppStorage(IJSRuntime js)
    {
        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("TotallyTextualBattleSimulator.AppStorage", throwOnError: false);
                var method = type?.GetMethod("ConfigureBrowserStorage", BindingFlags.Public | BindingFlags.Static, [typeof(object)]);
                if (method is null) continue;

                method.Invoke(null, [js]);
                return;
            }
        }
        catch
        {
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
                await stopped.ConfigureAwait(false);
            }
            finally
            {
                if (ReferenceEquals(runtime.app, app)) runtime.app = null;
                app.TryDispose(app.Lease, "Browser console app run completed");
            }
        }
    }
}
