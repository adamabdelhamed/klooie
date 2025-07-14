using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie;

/// <summary>
/// TODOS: 
/// - Selection mode should work when part of the selection is off-screen, so that notes out of theviewport can be selected. We want the user to be able to zoom in and out during selection.
/// </summary>
public class SelectionMode : TimelineInputMode
{
    public RGB SelectionModeColor { get; set; } = RGB.Blue;
    public RGB SelectedNoteColor { get; set; } = RGB.Cyan;
    private enum SelectionPhase { PickingAnchor, ExpandingSelection }
    private SelectionPhase selectionPhase = SelectionPhase.PickingAnchor;

    // Store logical selection position in (beat, midi)
    private (double Beat, int Midi)? selectionAnchorBeatMidi = null;
    private (double Beat, int Midi)? selectionCursorBeatMidi = null;
    private (double Beat, int Midi)? selectionPreviewCursorBeatMidi = null;

    // Store visual cursor/anchor for drawing
    private (int X, int Y)? selectionAnchor = null;
    private (int X, int Y)? selectionCursor = null;
    private (int X, int Y)? selectionPreviewCursor = null;

    private ConsoleControl? selectionRectangle = null;
    private ConsoleControl? anchorPreviewControl = null;
    private List<NoteExpression> selectedNotes = new();

    public List<NoteExpression> SelectedNotes => selectedNotes;

    public override void Paint(ConsoleBitmap context)
    {
        base.Paint(context);
        (int X, int Y)? cursor = selectionPhase == SelectionPhase.PickingAnchor ? selectionPreviewCursor : selectionCursor;

        int x = cursor.Value.X * VirtualTimelineGrid.ColWidthChars;
        if (cursor.HasValue && (x >= 0 && x < Timeline.Width))
        {
            for (int y = 0; y < Timeline.Height; y++)
            {
                var existingPixel = context.GetPixel(x, y);
                context.DrawString(
                    "|".ToConsoleString(SelectionModeColor, existingPixel.BackgroundColor),
                    x, y
                );
            }
        }
    }

    public override void HandleKeyInput(ConsoleKeyInfo k)
    {
        if (k.Key == ConsoleKey.OemPlus || k.Key == ConsoleKey.Add)
        {
            Timeline.BeatsPerColumn /= 2;
            SyncCursorToCurrentZoom();
        }
        else if (k.Key == ConsoleKey.OemMinus || k.Key == ConsoleKey.Subtract)
        {
            Timeline.BeatsPerColumn *= 2;
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
        Timeline.StatusChanged.Fire(ConsoleString.Parse("[White]Selection mode active. Use [B=Cyan][Black] arrows or WASD [D][White] to select an anchor point."));
        selectionPhase = SelectionPhase.PickingAnchor;
        selectionAnchorBeatMidi = null;
        selectionCursorBeatMidi = null;
        selectionAnchor = null;
        selectionCursor = null;
        selectionPreviewCursor = null;
        selectionPreviewCursorBeatMidi = null;
        HandleKeyInput(ConsoleKey.F5.KeyInfo());
    }

    // -- Picking Anchor Phase --
    private void HandlePickAnchorPhase(ConsoleKeyInfo k)
    {
        if (selectionPreviewCursorBeatMidi == null)
        {
            double playheadBeat = Timeline.CurrentBeat;
            int midBase = Timeline.Viewport.FirstVisibleMidi + Timeline.Viewport.MidisOnScreen / 2;

            var closest = Timeline.Descendents
                .OfType<NoteCell>()
                .Where(c => c.Note.Velocity > 0)
                .OrderBy(c => Math.Abs(c.Note.StartBeat - playheadBeat))
                .FirstOrDefault();

            double initBeat = Math.Floor(closest?.Note.StartBeat ?? playheadBeat);
            int initMidi = closest?.Note.MidiNote ?? midBase;

            selectionPreviewCursorBeatMidi = (initBeat, initMidi);
            SyncCursorToCurrentZoom();
            UpdateAnchorPreview(selectionPreviewCursor.Value);
            return;
        }

        var (beat, midi) = selectionPreviewCursorBeatMidi.Value;
        bool handled = false;

        if (IsLeft(k))
        {
            selectionPreviewCursorBeatMidi = (beat - Timeline.BeatsPerColumn, midi);
            handled = true;
        }
        else if (IsRight(k))
        {
            selectionPreviewCursorBeatMidi = (beat + Timeline.BeatsPerColumn, midi);
            handled = true;
        }
        else if (IsUp(k))
        {
            selectionPreviewCursorBeatMidi = (beat, midi + 1);
            handled = true;
        }
        else if (IsDown(k))
        {
            selectionPreviewCursorBeatMidi = (beat, midi - 1);
            handled = true;
        }
        else if (k.Key == ConsoleKey.Enter)
        {
            selectionAnchorBeatMidi = selectionPreviewCursorBeatMidi;
            selectionCursorBeatMidi = selectionPreviewCursorBeatMidi;
            selectionPhase = SelectionPhase.ExpandingSelection;
            RemoveAnchorPreview();
            SyncCursorToCurrentZoom();
            UpdateSelectionRectangle();
            Timeline.StatusChanged.Fire(ConsoleString.Parse("[White]Anchor point set. Use [B=Cyan][Black] arrows or WASD [D][White] to refine the selected area. Press [B=Cyan][Black] enter [D][White] to finalize selection."));
            return;
        }
        else if (k.Key == ConsoleKey.Escape)
        {
            Timeline.NextMode();
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
        int left = anchor.X * VirtualTimelineGrid.ColWidthChars;
        int top = anchor.Y * VirtualTimelineGrid.RowHeightChars;
        if (anchorPreviewControl == null)
        {
            anchorPreviewControl = Timeline.AddPreviewControl();
            anchorPreviewControl.ZIndex = 100;
            anchorPreviewControl.Background = SelectionModeColor;
            anchorPreviewControl.ZIndex = 1; // Above selection rectangle
        }
        anchorPreviewControl.MoveTo(left, top);
        anchorPreviewControl.ResizeTo(VirtualTimelineGrid.ColWidthChars, VirtualTimelineGrid.RowHeightChars);
    }

    private void RemoveAnchorPreview()
    {
        anchorPreviewControl?.Dispose();
        anchorPreviewControl = null;
        selectionPreviewCursor = null;
        selectionPreviewCursorBeatMidi = null;
    }

    // -- Expand Selection Phase --
    private void HandleExpandSelectionPhase(ConsoleKeyInfo k)
    {
        if (selectionCursorBeatMidi == null) return;
        var (beat, midi) = selectionCursorBeatMidi.Value;
        bool handled = false;

        if (IsLeft(k))
        {
            selectionCursorBeatMidi = (beat - Timeline.BeatsPerColumn, midi);
            handled = true;
        }
        else if (IsRight(k))
        {
            selectionCursorBeatMidi = (beat + Timeline.BeatsPerColumn, midi);
            handled = true;
        }
        else if (IsUp(k))
        {
            selectionCursorBeatMidi = (beat, midi + 1);
            handled = true;
        }
        else if (IsDown(k))
        {
            selectionCursorBeatMidi = (beat, midi - 1);
            handled = true;
        }
        else if (k.Key == ConsoleKey.Enter)
        {
            if (selectionAnchor == null || selectionCursor == null) return;
            selectedNotes.Clear();
            var (ax, ay) = selectionAnchor.Value;
            var (cx, cy) = selectionCursor.Value;
            int colMin = Math.Min(ax, cx), colMax = Math.Max(ax, cx);
            int rowMin = Math.Min(ay, cy), rowMax = Math.Max(ay, cy);

            double beat0 = Timeline.Viewport.FirstVisibleBeat + Math.Min(ax, cx) * Timeline.BeatsPerColumn;
            double beat1 = Timeline.Viewport.FirstVisibleBeat + Math.Max(ax, cx) * Timeline.BeatsPerColumn;
            int midi0 = Timeline.Viewport.FirstVisibleMidi + Timeline.Viewport.MidisOnScreen - 1 - Math.Max(ay, cy);
            int midi1 = Timeline.Viewport.FirstVisibleMidi + Timeline.Viewport.MidisOnScreen - 1 - Math.Min(ay, cy);

            // Swap if needed to ensure low <= high
            if (beat0 > beat1) (beat0, beat1) = (beat1, beat0);
            if (midi0 > midi1) (midi0, midi1) = (midi1, midi0);

            // Select notes from the underlying note source, not the UI
            var notesInRange = Timeline.NoteSource
                .Where(n => n.Velocity > 0
                    && n.StartBeat + (n.DurationBeats >= 0 ? n.DurationBeats : Timeline.Player.CurrentBeat - n.StartBeat) >= beat0
                    && n.StartBeat <= beat1
                    && n.MidiNote >= midi0
                    && n.MidiNote <= midi1)
                .ToList();

            // Colorize any NoteCells that are currently visible and selected (optional, for user feedback)
            var selectedSet = new HashSet<NoteExpression>(notesInRange);
            foreach (var cell in Timeline.Descendents.OfType<NoteCell>())
            {
                if (selectedSet.Contains(cell.Note))
                    cell.Background = SelectedNoteColor;
            }
            var noteSingularOrPlural = selectedNotes.Count == 1 ? "note" : "notes";
            Timeline.StatusChanged.Fire(ConsoleString.Parse($"[White]Selected [Cyan]{selectedNotes.Count}[White] {noteSingularOrPlural}."));
            Timeline.NextMode();
            selectionRectangle?.Dispose();
            selectionRectangle = null;
            selectionPhase = SelectionPhase.PickingAnchor;
            selectionAnchor = null;
            selectionCursor = null;
            selectionAnchorBeatMidi = null;
            selectionCursorBeatMidi = null;
            return;
        }
        else if (k.Key == ConsoleKey.Escape)
        {
            Timeline.NextMode();
            selectionRectangle?.Dispose();
            selectionRectangle = null;
            selectionPhase = SelectionPhase.PickingAnchor;
            selectionAnchor = null;
            selectionCursor = null;
            selectionAnchorBeatMidi = null;
            selectionCursorBeatMidi = null;
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

        int left = Math.Min(ax, cx) * VirtualTimelineGrid.ColWidthChars;
        int top = Math.Min(ay, cy) * VirtualTimelineGrid.RowHeightChars;
        int width = (Math.Abs(ax - cx) + 1) * VirtualTimelineGrid.ColWidthChars;
        int height = (Math.Abs(ay - cy) + 1) * VirtualTimelineGrid.RowHeightChars;

        if (selectionRectangle == null)
        {
            selectionRectangle = Timeline.AddPreviewControl();
            selectionRectangle.Background = SelectionModeColor;
            selectionRectangle.ZIndex = 0;
        }
        selectionRectangle.MoveTo(left, top);
        selectionRectangle.ResizeTo(width, height);
    }

    // -- Beat/Midi to X/Y cell mapping and sync --
    public void SyncCursorToCurrentZoom()
    {
        if (selectionAnchorBeatMidi != null)
        {
            var (beat, midi) = selectionAnchorBeatMidi.Value;
            selectionAnchor = BeatMidiToXY(beat, midi);
        }
        if (selectionCursorBeatMidi != null)
        {
            var (beat, midi) = selectionCursorBeatMidi.Value;
            selectionCursor = BeatMidiToXY(beat, midi);
        }
        if (selectionPreviewCursorBeatMidi != null)
        {
            var (beat, midi) = selectionPreviewCursorBeatMidi.Value;
            selectionPreviewCursor = BeatMidiToXY(beat, midi);
        }
        UpdateSelectionRectangle();
        if (anchorPreviewControl?.IsStillValid(anchorPreviewControl.Lease) == true && selectionPreviewCursor.HasValue)
        {
            UpdateAnchorPreview(selectionPreviewCursor.Value);
        }
    }

    private (int X, int Y) BeatMidiToXY(double beat, int midi)
    {
        int x = (int)Math.Round((beat - Timeline.Viewport.FirstVisibleBeat) / Timeline.BeatsPerColumn);
        int y = Timeline.Viewport.FirstVisibleMidi + Timeline.Viewport.MidisOnScreen - 1 - midi;
        x = Math.Max(0, Math.Min(Timeline.Width / VirtualTimelineGrid.ColWidthChars - 1, x));
        y = Math.Max(0, Math.Min(Timeline.Height / VirtualTimelineGrid.RowHeightChars - 1, y));
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

