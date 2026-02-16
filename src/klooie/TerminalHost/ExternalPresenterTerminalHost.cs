using klooie.Gaming;

namespace klooie;

public sealed class ExternalPresenterTerminalHost : ITerminalHost
{
    private readonly FrameBridge bridge;

    public ExternalPresenterTerminalHost(FrameBridge bridge) => this.bridge = bridge;

    public IDisposable BeginFrame(LayoutRootPanel root)
    {
        bridge.OwnerCapture.Begin(root.Width, root.Height);
        Container.CompositionObserver = bridge.OwnerCapture;
        return new Scope(root);
    }

    public bool Present(LayoutRootPanel root, ConsoleBitmap bitmap)
    {
        bridge.Enqueue(BuildSceneSnapshot(root, bridge.OwnerCapture.SnapshotOwners()));
        return true;
    }

    public bool SyncSize(LayoutRootPanel root)
    {
        // For now: mirror ANSI size behavior since LayoutRootPanel still depends on ConsoleProvider for dimensions.
        // Later: FrameBridge host can instead accept window size from the presenter and drive root.ResizeTo().
        return false;
    }

    private static SceneSnapshot BuildSceneSnapshot(LayoutRootPanel root, int[] ownerIds)
    {
        var w = root.Width;
        var h = root.Height;

        var pixels = new ConsoleCharacter[w * h];
        for (var y = 0; y < h; y++)
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                pixels[row + x] = root.Bitmap.GetPixel(x, y);
            }
        }

        var buffer = Container.DescendentBufferPool.Rent();
        try
        {
            root.PopulateDescendentsWithZeroAllocations(buffer);

            var states = new ControlState[buffer.Count];
            for (var i = 0; i < buffer.Count; i++)
            {
                var c = buffer[i];
                states[i] = new ControlState(LayoutRootPanel.GetIdForPresentation(c), c.Left, c.Top);
            }

            return new SceneSnapshot
            {
                ViewWidth = w,
                ViewHeight = h,
                Pixels = pixels,
                OwnerIds = ownerIds,
                Controls = states
            };
        }
        finally
        {
            Container.DescendentBufferPool.Return(buffer);
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly LayoutRootPanel root;
        public Scope(LayoutRootPanel root) => this.root = root;
        public void Dispose() => Container.CompositionObserver = null;
    }
}
