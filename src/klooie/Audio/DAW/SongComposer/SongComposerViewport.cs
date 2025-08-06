using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

// Viewport for the multi-track Composer editor; each row = 1 track (not midi note)
public partial class SongComposerViewport : IObservableObject
{
    public const int DefaultFirstVisibleTrack = 0;         // Top row is track 0
    public const int TrackRowHeight = 3;                   // Each track row is 3 chars high (configurable)

    public partial int FirstVisibleTrack { get; set; }      // Index of first visible track (row)
    public partial int TracksOnScreen { get; set; }         // Number of visible tracks
    public partial double FirstVisibleBeat { get; set; }
    public double LastVisibleBeat => FirstVisibleBeat + BeatsOnScreen;

    public partial double BeatsOnScreen { get; set; }

    public void ScrollTracks(int delta, int trackCount)
    {
        // Scroll up/down track lanes, stay within bounds of track list
        FirstVisibleTrack = Math.Clamp(FirstVisibleTrack + delta, 0, Math.Max(0, trackCount - TracksOnScreen));
    }

    public void ScrollBeats(double dx)
    {
        FirstVisibleBeat = Math.Max(0, FirstVisibleBeat + dx);
    }

    public SongComposer Composer { get; }

    public SongComposerViewport(SongComposer composer)
    {
        this.Composer = composer;
        FirstVisibleTrack = DefaultFirstVisibleTrack;
        TracksOnScreen = 0; // Will be set later by panel size, etc.
    }

    public void OnBeatChanged(double beat)
    {
        if (beat > FirstVisibleBeat + BeatsOnScreen * 0.8)
        {
            FirstVisibleBeat = ConsoleMath.Round(beat - BeatsOnScreen * 0.2);
            Composer.RefreshVisibleSet();
        }
        else if (beat < FirstVisibleBeat)
        {
            FirstVisibleBeat = Math.Max(0, ConsoleMath.Round(beat - BeatsOnScreen * 0.8));
            Composer.RefreshVisibleSet();
        }
    }
}
