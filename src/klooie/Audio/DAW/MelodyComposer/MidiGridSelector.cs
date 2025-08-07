using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie;

public class MidiGridSelector : BeatGridInputMode<NoteExpression>
{
    public MidiGrid MelodyComposer => this.Composer as MidiGrid;
    public RGB SelectionModeColor { get; set; } = RGB.Blue;
    public static readonly RGB SelectedNoteColor = RGB.Cyan;
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

    public override void Paint(ConsoleBitmap context)
    {
        base.Paint(context);
        (int X, int Y)? cursor = selectionPhase == SelectionPhase.PickingAnchor ? selectionPreviewCursor : selectionCursor;

        int x = cursor.Value.X * Composer.Viewport.ColWidthChars;
        if (cursor.HasValue && (x >= 0 && x < Composer.Width))
        {
            for (int y = 0; y < Composer.Height; y++)
            {
                var existingPixel = context.GetPixel(x, y);
                context.DrawString("|".ToConsoleString(SelectionModeColor, existingPixel.BackgroundColor), x, y);
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
        MelodyComposer.Editor.ClearAddPreview();
        selectionPhase = SelectionPhase.PickingAnchor;
        selectionAnchorBeatMidi = null;
        selectionCursorBeatMidi = null;
        selectionAnchor = null;
        selectionCursor = null;
        selectionPreviewCursor = null;
        selectionPreviewCursorBeatMidi = null;
        HandleKeyInput(ConsoleKey.F5.KeyInfo());
        MelodyComposer.ModeChanging.SubscribeOnce((m) =>
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
        if (selectionPreviewCursorBeatMidi == null)
        {
            int midBase = Composer.Viewport.FirstVisibleRow + Composer.Viewport.RowsOnScreen / 2;

            var closest = Composer.Descendents
                .OfType<ComposerCell<NoteExpression>>()
                .Where(c => c.Value.Velocity > 0)
                .OrderBy(c => Math.Abs(c.Value.StartBeat - Composer.Player.CurrentBeat))
                .FirstOrDefault();

            int initMidi = closest?.Value.MidiNote ?? midBase;
            selectionPreviewCursorBeatMidi = (Composer.Player.CurrentBeat, initMidi);
            SyncCursorToCurrentZoom();
            UpdateAnchorPreview(selectionPreviewCursor.Value);
            return;
        }

        var (beat, midi) = selectionPreviewCursorBeatMidi.Value;
        bool handled = false;

        if (IsLeft(k))
        {
            selectionPreviewCursorBeatMidi = (beat - Composer.BeatsPerColumn, midi);
            handled = true;
        }
        else if (IsRight(k))
        {
            selectionPreviewCursorBeatMidi = (beat + Composer.BeatsPerColumn, midi);
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
            Composer.StatusChanged.Fire(ConsoleString.Parse("[White]Anchor point set. Use [B=Cyan][Black] arrows or WASD [D][White] to refine the selected area. Press [B=Cyan][Black] enter [D][White] to finalize selection."));
            return;
        }
        else if (k.Key == ConsoleKey.Escape)
        {
            MelodyComposer.NextMode();
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
        int left = anchor.X * Composer.Viewport.ColWidthChars;
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
        selectionPreviewCursorBeatMidi = null;
    }

    private void HandleExpandSelectionPhase(ConsoleKeyInfo k)
    {
        if (selectionCursorBeatMidi == null) return;
        var (beat, midi) = selectionCursorBeatMidi.Value;
        bool handled = false;

        if (IsLeft(k))
        {
            selectionCursorBeatMidi = (beat - Composer.BeatsPerColumn, midi);
            handled = true;
        }
        else if (IsRight(k))
        {
            selectionCursorBeatMidi = (beat + Composer.BeatsPerColumn, midi);
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
            Composer.SelectedValues.Clear();
            var (ax, ay) = selectionAnchor.Value;
            var (cx, cy) = selectionCursor.Value;
            int colMin = Math.Min(ax, cx), colMax = Math.Max(ax, cx);
            int rowMin = Math.Min(ay, cy), rowMax = Math.Max(ay, cy);

            double beat0 = Composer.Viewport.FirstVisibleBeat + Math.Min(ax, cx) * Composer.BeatsPerColumn;
            double beat1 = Composer.Viewport.FirstVisibleBeat + Math.Max(ax, cx) * Composer.BeatsPerColumn;
            int row0 = Math.Min(ay, cy), row1 = Math.Max(ay, cy);
            int midi0 = 127 - (Composer.Viewport.FirstVisibleRow + row0);
            int midi1 = 127 - (Composer.Viewport.FirstVisibleRow + row1);

            // Swap if needed to ensure low <= high
            if (beat0 > beat1) (beat0, beat1) = (beat1, beat0);
            if (midi0 > midi1) (midi0, midi1) = (midi1, midi0);

            // Select notes from the underlying note source, not the UI
            Composer.SelectedValues.AddRange(MelodyComposer.Notes
                .Where(n => n.Velocity > 0
                    && n.StartBeat + (n.DurationBeats >= 0 ? n.DurationBeats : Composer.Player.CurrentBeat - n.StartBeat) >= beat0
                    && n.StartBeat <= beat1
                    && n.MidiNote >= midi0
                    && n.MidiNote <= midi1));

            bool canAddNote = Composer.SelectedValues.Count == 0 && midi0 == midi1;
            int colStart = Math.Min(ax, cx);
            int colEnd = Math.Max(ax, cx);
            double addStartBeat = Composer.Viewport.FirstVisibleBeat + colStart * Composer.BeatsPerColumn;
            double addDuration = (colEnd - colStart + 1) * Composer.BeatsPerColumn;

            // Colorize any NoteCells that are currently visible and selected (optional, for user feedback)
            var selectedSet = new HashSet<NoteExpression>(Composer.SelectedValues);
            foreach (var cell in Composer.Descendents.OfType<ComposerCell<NoteExpression>>())
            {
                if (selectedSet.Contains(cell.Value))
                    cell.Background = SelectedNoteColor;
            }
            var noteSingularOrPlural = Composer.SelectedValues.Count == 1 ? "note" : "notes";
            if (canAddNote)
            {
                MelodyComposer.Editor.BeginAddPreview(addStartBeat, addDuration, midi0);
            }
            Composer.StatusChanged.Fire(ConsoleString.Parse($"[White]Selected [Cyan]{Composer.SelectedValues.Count}[White] {noteSingularOrPlural}."));
            MelodyComposer.NextMode();
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
            MelodyComposer.NextMode();
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
        int x = (int)Math.Round((beat - Composer.Viewport.FirstVisibleBeat) / Composer.BeatsPerColumn);
        int row = 127 - midi;
        int y = row - Composer.Viewport.FirstVisibleRow;
        x = Math.Max(0, Math.Min(Composer.Width / Composer.Viewport.ColWidthChars - 1, x));
        y = Math.Max(0, Math.Min(Composer.Height / Composer.Viewport.RowHeightChars - 1, y));
        return (x, y);
    }


    private bool IsLeft(ConsoleKeyInfo k) => k.Key == ConsoleKey.LeftArrow || k.Key == ConsoleKey.A;
    private bool IsRight(ConsoleKeyInfo k)  => k.Key == ConsoleKey.RightArrow || k.Key == ConsoleKey.D;
    private bool IsUp(ConsoleKeyInfo k) => k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.W;
    private bool IsDown(ConsoleKeyInfo k) => k.Key == ConsoleKey.DownArrow || k.Key == ConsoleKey.S;
}

