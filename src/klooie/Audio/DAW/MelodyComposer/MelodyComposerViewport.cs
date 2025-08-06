
using klooie;

public partial class MelodyComposerViewport : Viewport
{
    public const int DefaultFirstVisibleMidi = 50;
    public override int RowHeightChars => 1;
    public override int ColWidthChars => 1;
    public MelodyComposerViewport() : base() => SetFirstVisibleRow(DefaultFirstVisibleMidi);
}