using klooie.Gaming;
using PowerArgs;
using System.Diagnostics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace klooie;

public sealed class VeldridTerminalHost : ITerminalHost
{
    // Public knobs
    public static float BoardZoom = 1f;
    public const int CellPxWidth = 10;
    private static readonly int CellPxHeight = CellPxWidth * 2;

    // Singleton-ish attachment state (keeps the old behavior: one host/window shared across roots)
    private static CompositionOwnerCapture? ownerCapture;
    private static VeldridTerminalHost? host;
    private static LayoutRootPanel? attachedRoot;

    public static void Init()
    {
        ConsoleProvider.Current = new NoOpConsole();
        ownerCapture = new CompositionOwnerCapture() { IdProvider = LayoutRootPanel.GetIdForPresentation };
        Container.CompositionObserver = ownerCapture;

        host = new VeldridTerminalHost(ownerCapture, msg => Trace.WriteLine(msg));
        VeldridWindow.Instance.EnsureInitialized(msg => Trace.WriteLine(msg));
    }

    public static void Attach(LayoutRootPanel root)
    {
        if (ownerCapture == null) throw new InvalidOperationException("Call Init() before attaching a root.");
        attachedRoot = root;
        root.TerminalHost = host!;
        host!.SyncSize(root);

        // Preserve your current behavior/hook point
        ConsoleApp.Current.InvokeNextCycle(host.EnableZoom);
    }

    public static void SyncSizeForCurrentRoot()
    {
        if (host == null || attachedRoot == null) return;
        host.SyncSize(attachedRoot);
    }

    // Instance state
    private readonly CompositionOwnerCapture ownerCaptureInstance;
    private readonly Action<string>? log;
    private int lastCols;
    private int lastRows;

    private VeldridTerminalHost(CompositionOwnerCapture ownerCapture, Action<string>? log = null)
    {
        ownerCaptureInstance = ownerCapture;
        this.log = log;
    }

    public bool Present(LayoutRootPanel root, ConsoleBitmap bitmap)
    {
        ownerCaptureInstance.Begin(root.Width, root.Height);
        var owners = ownerCaptureInstance.SnapshotOwners();
        VeldridWindow.Instance.Render(root, bitmap, owners, ownerCaptureInstance);
        return true;
    }

    public bool SyncSize(LayoutRootPanel root)
    {
        var fbW = VeldridWindow.Instance.FramebufferWidth;
        var fbH = VeldridWindow.Instance.FramebufferHeight;
        if (fbW <= 0 || fbH <= 0) return false;

        var zoom = BoardZoom;
        if (zoom <= 0) zoom = 0.01f;

        var effectiveCellPxW = CellPxWidth * zoom;
        var effectiveCellPxH = CellPxHeight * zoom;

        var cols = (int)MathF.Floor(fbW / effectiveCellPxW);
        var rows = (int)MathF.Floor(fbH / effectiveCellPxH);

        if (cols < 1) cols = 1;
        if (rows < 1) rows = 1;

        log?.Invoke(
            $"WindowPx=({fbW},{fbH}) " +
            $"Zoom={zoom:F2} " +
            $"EffectiveCellPx=({effectiveCellPxW:F2},{effectiveCellPxH:F2}) " +
            $"ViewCells=({cols},{rows})"
        );

        var rootCols = root.Width;
        var rootRows = root.Height;

        var capacityUnchanged = cols == lastCols && rows == lastRows;
        var rootAlreadySized = cols == rootCols && rows == rootRows;

        if (capacityUnchanged && rootAlreadySized) return false;

        lastCols = cols;
        lastRows = rows;

        root.ResizeTo(cols, rows);
        return true;
    }

    // Zoom hook (kept as your existing placeholder)
    private void EnableZoom()
    {
        //if (HumanInputController.Poller?.IsConnected == false) return;
        //HumanInputController.Poller!.RightJoystickMoved.Subscribe(Operate3DCamera, ConsoleApp.Current);
    }

    private static void Operate3DCamera(LocF stick)
    {
        if (Game.Current?.IsPaused == true) return;

        var y = stick.Top;
        const float dead = 0.12f;
        if (MathF.Abs(y) < dead) return;

        var s = (MathF.Abs(y) - dead) / (1f - dead);
        s = MathF.Min(s, 1f);
        s = s * s;

        var delta = 0.03f * s * MathF.Sign(y);

        var currentZoom = BoardZoom;
        var nextZoom = currentZoom * (1f - delta);

        nextZoom = Math.Clamp(nextZoom, 0.25f, 4f);
        if (MathF.Abs(nextZoom - currentZoom) < 0.0001f) return;

        BoardZoom = nextZoom;
        SyncSizeForCurrentRoot();
    }

    // Keep the NoOpConsole where it belongs (private implementation detail)
    private sealed class NoOpConsole : IConsoleProvider
    {
        public RGB ForegroundColor { get; set; }
        public RGB BackgroundColor { get; set; }
        public bool KeyAvailable => false;
        public void Append(string text) { }
        public void Clear() { }
        public int CursorLeft { get; set; }
        public int CursorTop { get; set; }
        public int BufferWidth { get; set; }
        public int WindowHeight { get; set; }
        public int WindowWidth { get; set; }
        public void Write(char[] buffer, int length) { }
        public void Write(object output) { }
        public void WriteLine(object output) { }
        public void WriteLine() { }
        public void Write(ConsoleString consoleString) { }
        public void Write(in ConsoleCharacter consoleCharacter) { }
        public void WriteLine(ConsoleString consoleString) { }
        public ConsoleKeyInfo ReadKey() => throw new NotImplementedException();
        public int Read() => throw new NotImplementedException();
        public ConsoleKeyInfo ReadKey(bool intercept) => throw new NotImplementedException();
        public string ReadLine() => throw new NotImplementedException();
    }

    // Window/renderer implementation detail
    private sealed class VeldridWindow
    {
        public static VeldridWindow Instance { get; } = new VeldridWindow();

        private Sdl2Window? window;
        private GraphicsDevice? gd;
        private CommandList? cl;
        private CellInstancedRenderer? renderer;

        private bool initialized;
        private bool swapchainDirty;
        private bool rendererDirty;
        private long lastResizeTimestamp;
        private const double ResizeSettleMs = 150;

        public int WindowWidth => window?.Width ?? 0;
        public int WindowHeight => window?.Height ?? 0;

        public int FramebufferWidth => (int)(gd?.MainSwapchain?.Framebuffer.Width ?? 0);
        public int FramebufferHeight => (int)(gd?.MainSwapchain?.Framebuffer.Height ?? 0);

        private VeldridWindow() { }

        public void EnsureInitialized(Action<string> log)
        {
            if (initialized) return;
            initialized = true;

            var windowCI = new WindowCreateInfo(100, 100, 1280, 720, WindowState.Normal, "klooie 3d Sample");
            var gdOptions = new GraphicsDeviceOptions(false, PixelFormat.D24_UNorm_S8_UInt, false, ResourceBindingModel.Improved, true, true);
            VeldridStartup.CreateWindowAndGraphicsDevice(windowCI, gdOptions, GraphicsBackend.Vulkan, out window, out gd);

            window.Resized += () => swapchainDirty = true;

            cl = gd.ResourceFactory.CreateCommandList();
            renderer = new CellInstancedRenderer(gd);

            swapchainDirty = true;
        }

        private void PresentBlack()
        {
            var fb = gd!.MainSwapchain.Framebuffer;
            if (fb.Width == 0 || fb.Height == 0) return;
            cl!.Begin();
            cl.SetFramebuffer(fb);
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.ClearDepthStencil(1f);
            cl.End();
            gd.SubmitCommands(cl);
            gd.SwapBuffers(gd.MainSwapchain);
        }

        public void Render(LayoutRootPanel root, ConsoleBitmap bitmap, int[] ownersSnapshot, CompositionOwnerCapture ownerCapture)
        {
            if (!initialized || window == null || gd == null || cl == null || renderer == null) return;
            if (window.Exists == false) return;

            window.PumpEvents();
            if (window.Exists == false)
            {
                Environment.Exit(0);
                return;
            }

            if (window.Width == 0 || window.Height == 0) return;

            if (swapchainDirty)
            {
                swapchainDirty = false;
                gd.WaitForIdle();
                gd.MainSwapchain.Resize((uint)Math.Max(1, window.Width), (uint)Math.Max(1, window.Height));
                rendererDirty = true;
                lastResizeTimestamp = Stopwatch.GetTimestamp();
            }

            if (rendererDirty)
            {
                if (Stopwatch.GetElapsedTime(lastResizeTimestamp).TotalMilliseconds < ResizeSettleMs)
                {
                    PresentBlack();
                    return;
                }

                rendererDirty = false;
                gd.WaitForIdle();
                renderer.Dispose();
                renderer = new CellInstancedRenderer(gd);
            }

            var fb = gd.MainSwapchain.Framebuffer;
            if (fb.Width == 0 || fb.Height == 0) return;

            cl.Begin();
            cl.SetFramebuffer(fb);
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.ClearDepthStencil(1f);

            renderer.Draw(cl, root, bitmap, ownersSnapshot, window.Width, window.Height, (int)fb.Width, (int)fb.Height);

            cl.End();
            gd.SubmitCommands(cl);
            gd.SwapBuffers(gd.MainSwapchain);
        }
    }
}
