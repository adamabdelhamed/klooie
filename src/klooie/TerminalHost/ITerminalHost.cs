 
namespace klooie;

public interface ITerminalHost
{

    /// <summary>
    /// Presents the final bitmap. If it returns false, the caller should treat the frame as skipped.
    /// </summary>
    bool Present(LayoutRootPanel root, ConsoleBitmap bitmap);

    /// <summary>
    /// Allows the host to sync/observe size changes and resize the root if needed.
    /// Returns true if the size changed.
    /// </summary>
    bool SyncSize(LayoutRootPanel root);
}
