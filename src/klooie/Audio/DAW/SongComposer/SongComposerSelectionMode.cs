using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie;

public class SongComposerSelectionMode : SongComposerInputMode
{
    public RGB SelectionModeColor { get; set; } = RGB.Blue;
    public static readonly RGB SelectedMelodyColor = RGB.Cyan;

    private enum SelectionPhase { PickingAnchor, ExpandingSelection }
    private SelectionPhase selectionPhase = SelectionPhase.PickingAnchor;

    // Store logical selection position in (beat, trackIndex)
    private (double Beat, int Track)? selectionAnchorBeatTrack = null;
    private (double Beat, int Track)? selectionCursorBeatTrack = null;
    private (double Beat, int Track)? selectionPreviewCursorBeatTrack = null;

    // Store visual cursor/anchor for drawing
    private (int X, int Y)? selectionAnchor = null;
    private (int X, int Y)? selectionCursor = null;
    private (int X, int Y)? selectionPreviewCursor = null;

    private ConsoleControl? selectionRectangle = null;
    private ConsoleControl? anchorPreviewControl = null;

    public override void Paint(ConsoleBitmap context)
    {
        base.Paint(context);
        (int X, int Y)? cursor = selectionPhase == SelectionPhase.PickingAnchor ? selectionPreviewCursor : selectionCursor;

        if (cursor.HasValue)
        {
            int x = cursor.Value.X * Composer<object>.ColWidthChars;
            int y = cursor.Value.Y * Composer.Viewport.RowHeightChars;
            if (x >= 0 && x < Composer.Width)
            {
                for (int dy = 0; dy < Composer.Viewport.RowHeightChars; dy++)
                {
                    int rowY = y + dy;
                    if (rowY < Composer.Height)
                    {
                        var existingPixel = context.GetPixel(x, rowY);
                        context.DrawString(
                            "|".ToConsoleString(SelectionModeColor, existingPixel.BackgroundColor),
                            x, rowY
                        );
                    }
                }
            }
        }
    }

    public override void HandleKeyInput(ConsoleKeyInfo k)
    {
        if (k.Key == ConsoleKey.OemPlus || k.Key == ConsoleKey.Add)
        {
            Composer.BeatsPerColumn /= 2;
            SyncCursorToCurrentZoom();
        }
        else if (k.Key == ConsoleKey.OemMinus || k.Key == ConsoleKey.Subtract)
        {
            Composer.BeatsPerColumn *= 2;
            SyncCursorToCurrentZoom();
        }
        else if (selectionPhase == SelectionPhase.PickingAnchor)
        {
            HandlePickAnchorPhase(k);
        }
        else
        {
            HandleExpandSelectionPhase(k);
        }
    }

    public override void Enter()
    {
        Composer.StatusChanged.Fire(ConsoleString.Parse("[White]Selection mode active. Use [B=Cyan][Black] arrows or WASD [D][White] to select an anchor point."));
        selectionPhase = SelectionPhase.PickingAnchor;
        selectionAnchorBeatTrack = null;
        selectionCursorBeatTrack = null;
        selectionAnchor = null;
        selectionCursor = null;
        selectionPreviewCursor = null;
        selectionPreviewCursorBeatTrack = null;
        HandleKeyInput(ConsoleKey.F5.KeyInfo());
        Composer.ModeChanging.SubscribeOnce((m) =>
        {
            selectionAnchor = null;
            selectionRectangle?.TryDispose();
            selectionRectangle = null;
            anchorPreviewControl?.TryDispose();
            anchorPreviewControl = null;
        });
    }

    // -- Picking Anchor Phase --
    private void HandlePickAnchorPhase(ConsoleKeyInfo k)
    {
        if (selectionPreviewCursorBeatTrack == null)
        {
            int trackBase = Composer.Viewport.FirstVisibleRow + Composer.Viewport.RowsOnScreen / 2;

            // Pick the first visible melody clip, if any, otherwise default to middle track
            var closest = Composer.Tracks
                .SelectMany((t, ti) => t.Melodies.Select(m => (melody: m, trackIdx: ti)))
                .OrderBy(pair => Math.Abs(pair.melody.StartBeat - Composer.CurrentBeat))
                .FirstOrDefault();

            int initTrack = closest.melody != null ? closest.trackIdx : trackBase;
            selectionPreviewCursorBeatTrack = (Composer.CurrentBeat, initTrack);
            SyncCursorToCurrentZoom();
            UpdateAnchorPreview(selectionPreviewCursor.Value);
            return;
        }

        var (beat, track) = selectionPreviewCursorBeatTrack.Value;
        bool handled = false;

        if (IsLeft(k))
        {
            selectionPreviewCursorBeatTrack = (beat - Composer.BeatsPerColumn, track);
            handled = true;
        }
        else if (IsRight(k))
        {
            selectionPreviewCursorBeatTrack = (beat + Composer.BeatsPerColumn, track);
            handled = true;
        }
        else if (IsUp(k))
        {
            selectionPreviewCursorBeatTrack = (beat, Math.Max(0, track - 1));
            handled = true;
        }
        else if (IsDown(k))
        {
            selectionPreviewCursorBeatTrack = (beat, Math.Min(Composer.Tracks.Count - 1, track + 1));
            handled = true;
        }
        else if (k.Key == ConsoleKey.Enter)
        {
            selectionAnchorBeatTrack = selectionPreviewCursorBeatTrack;
            selectionCursorBeatTrack = selectionPreviewCursorBeatTrack;
            selectionPhase = SelectionPhase.ExpandingSelection;
            RemoveAnchorPreview();
            SyncCursorToCurrentZoom();
            UpdateSelectionRectangle();
            Composer.StatusChanged.Fire(ConsoleString.Parse("[White]Anchor point set. Use [B=Cyan][Black] arrows or WASD [D][White] to refine the selected area. Press [B=Cyan][Black] enter [D][White] to finalize selection."));
            return;
        }
        else if (k.Key == ConsoleKey.Escape)
        {
            Composer.NextMode();
            RemoveAnchorPreview();
            return;
        }

        if (handled)
        {
            SyncCursorToCurrentZoom();
        }
    }

    private void UpdateAnchorPreview((int X, int Y) anchor)
    {
        int left = anchor.X * Composer<object>.ColWidthChars;
        int top = anchor.Y * Composer.Viewport.RowHeightChars;
        if (anchorPreviewControl == null)
        {
            anchorPreviewControl = Composer.AddPreviewControl();
            anchorPreviewControl.ZIndex = 100;
            anchorPreviewControl.Background = SelectionModeColor;
            anchorPreviewControl.ZIndex = 1; // Above selection rectangle
        }
        anchorPreviewControl.MoveTo(left, top);
        anchorPreviewControl.ResizeTo(Composer.Viewport.ColWidthChars, Composer.Viewport.RowHeightChars);
    }

    private void RemoveAnchorPreview()
    {
        anchorPreviewControl?.Dispose();
        anchorPreviewControl = null;
        selectionPreviewCursor = null;
        selectionPreviewCursorBeatTrack = null;
    }

    // -- Expand Selection Phase --
    private void HandleExpandSelectionPhase(ConsoleKeyInfo k)
    {
        if (selectionCursorBeatTrack == null) return;
        var (beat, track) = selectionCursorBeatTrack.Value;
        bool handled = false;

        if (IsLeft(k))
        {
            selectionCursorBeatTrack = (beat - Composer.BeatsPerColumn, track);
            handled = true;
        }
        else if (IsRight(k))
        {
            selectionCursorBeatTrack = (beat + Composer.BeatsPerColumn, track);
            handled = true;
        }
        else if (IsUp(k))
        {
            selectionCursorBeatTrack = (beat, Math.Max(0, track - 1));
            handled = true;
        }
        else if (IsDown(k))
        {
            selectionCursorBeatTrack = (beat, Math.Min(Composer.Tracks.Count - 1, track + 1));
            handled = true;
        }
        else if (k.Key == ConsoleKey.Enter)
        {
            if (selectionAnchor == null || selectionCursor == null) return;
            Composer.SelectedMelodies.Clear();

            var (ax, ay) = selectionAnchor.Value;
            var (cx, cy) = selectionCursor.Value;
            int colMin = Math.Min(ax, cx), colMax = Math.Max(ax, cx);
            int rowMin = Math.Min(ay, cy), rowMax = Math.Max(ay, cy);

            double beat0 = Composer.Viewport.FirstVisibleBeat + colMin * Composer.BeatsPerColumn;
            double beat1 = Composer.Viewport.FirstVisibleBeat + colMax * Composer.BeatsPerColumn;
            int track0 = Composer.Viewport.FirstVisibleRow + Math.Min(ay, cy);
            int track1 = Composer.Viewport.FirstVisibleRow + Math.Max(ay, cy);

            if (beat0 > beat1) (beat0, beat1) = (beat1, beat0);
            if (track0 > track1) (track0, track1) = (track1, track0);

            // Select melodies that overlap with the selection region
            for (int t = track0; t <= track1 && t < Composer.Tracks.Count; t++)
            {
                foreach (var melody in Composer.Tracks[t].Melodies)
                {
                    double melodyStart = melody.StartBeat;
                    double melodyEnd = melody.StartBeat + melody.DurationBeats;
                    bool overlaps = (melodyEnd >= beat0) && (melodyStart <= beat1);
                    if (overlaps)
                        Composer.SelectedMelodies.Add(melody);
                }
            }

            // Optionally, colorize MelodyCells for feedback
            var selectedSet = new HashSet<MelodyClip>(Composer.SelectedMelodies);
            foreach (var cell in Composer.Descendents.OfType<MelodyCell>())
            {
                if (selectedSet.Contains(cell.Melody))
                    cell.Background = SelectedMelodyColor;
            }

            var plural = Composer.SelectedMelodies.Count == 1 ? "melody" : "melodies";
            Composer.StatusChanged.Fire(ConsoleString.Parse($"[White]Selected [Cyan]{Composer.SelectedMelodies.Count}[White] {plural}."));
            Composer.NextMode();
            selectionRectangle?.Dispose();
            selectionRectangle = null;
            selectionPhase = SelectionPhase.PickingAnchor;
            selectionAnchor = null;
            selectionCursor = null;
            selectionAnchorBeatTrack = null;
            selectionCursorBeatTrack = null;
            return;
        }
        else if (k.Key == ConsoleKey.Escape)
        {
            Composer.NextMode();
            selectionRectangle?.Dispose();
            selectionRectangle = null;
            selectionPhase = SelectionPhase.PickingAnchor;
            selectionAnchor = null;
            selectionCursor = null;
            selectionAnchorBeatTrack = null;
            selectionCursorBeatTrack = null;
            return;
        }

        if (handled)
        {
            SyncCursorToCurrentZoom();
            UpdateSelectionRectangle();
        }
    }

    private void UpdateSelectionRectangle()
    {
        if (selectionAnchor == null || selectionCursor == null) return;
        var (ax, ay) = selectionAnchor.Value;
        var (cx, cy) = selectionCursor.Value;

        int left = Math.Min(ax, cx) * Composer.Viewport.ColWidthChars;
        int top = Math.Min(ay, cy) * Composer.Viewport.RowHeightChars;
        int width = (Math.Abs(ax - cx) + 1) * Composer.Viewport.ColWidthChars;
        int height = (Math.Abs(ay - cy) + 1) * Composer.Viewport.RowHeightChars;

        if (selectionRectangle == null)
        {
            selectionRectangle = Composer.AddPreviewControl();
            selectionRectangle.Background = SelectionModeColor;
            selectionRectangle.ZIndex = 0;
        }
        selectionRectangle.MoveTo(left, top);
        selectionRectangle.ResizeTo(width, height);
    }

    // -- Beat/Track to X/Y cell mapping and sync --
    public void SyncCursorToCurrentZoom()
    {
        if (selectionAnchorBeatTrack != null)
        {
            var (beat, track) = selectionAnchorBeatTrack.Value;
            selectionAnchor = BeatTrackToXY(beat, track);
        }
        if (selectionCursorBeatTrack != null)
        {
            var (beat, track) = selectionCursorBeatTrack.Value;
            selectionCursor = BeatTrackToXY(beat, track);
        }
        if (selectionPreviewCursorBeatTrack != null)
        {
            var (beat, track) = selectionPreviewCursorBeatTrack.Value;
            selectionPreviewCursor = BeatTrackToXY(beat, track);
        }
        UpdateSelectionRectangle();
        if (anchorPreviewControl?.IsStillValid(anchorPreviewControl.Lease) == true && selectionPreviewCursor.HasValue)
        {
            UpdateAnchorPreview(selectionPreviewCursor.Value);
        }
    }

    private (int X, int Y) BeatTrackToXY(double beat, int track)
    {
        int x = (int)Math.Round((beat - Composer.Viewport.FirstVisibleBeat) / Composer.BeatsPerColumn);
        int y = track - Composer.Viewport.FirstVisibleRow;
        x = Math.Max(0, Math.Min(Composer.Width / Composer.Viewport.ColWidthChars - 1, x));
        y = Math.Max(0, Math.Min(Composer.Viewport.RowsOnScreen - 1, y));
        return (x, y);
    }

    // -- Utility helpers for keys --
    private bool IsLeft(ConsoleKeyInfo k)
        => k.Key == ConsoleKey.LeftArrow || k.Key == ConsoleKey.A;
    private bool IsRight(ConsoleKeyInfo k)
        => k.Key == ConsoleKey.RightArrow || k.Key == ConsoleKey.D;
    private bool IsUp(ConsoleKeyInfo k)
        => k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.W;
    private bool IsDown(ConsoleKeyInfo k)
        => k.Key == ConsoleKey.DownArrow || k.Key == ConsoleKey.S;
}
