
using klooie;

public partial class MelodyComposerViewport : Viewport
{
    public const int DefaultFirstVisibleMidi = 50;
    public MelodyComposerViewport() : base() => SetFirstVisibleRow(DefaultFirstVisibleMidi);
}