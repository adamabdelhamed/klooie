using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public class SongComposerViewport : Viewport
{
    public const int DefaultFirstVisibleTrack = 0;
    public override int ColWidthChars => 1;
    public override int RowHeightChars => 3;
    public SongComposerViewport() : base() { }
}
