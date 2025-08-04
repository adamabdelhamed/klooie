using System;
using System.Collections.Generic;

namespace klooie;

public class ComposerWithTracks : ProtectedConsolePanel
{
    private GridLayout layout;
    public TrackHeadersPanel TrackHeaders { get; private init; }
    public Composer Composer { get; private init; }
    public StatusBar StatusBar { get; private init; }

    // Expose player if desired
    public ComposerPlayer Player => Composer.Player;
    public IMidiProvider MidiProvider { get; private set; }

    public ComposerWithTracks(WorkspaceSession session, List<ComposerTrack> tracks, ConsoleControl commandBar, IMidiProvider midiProvider)
    {
        this.MidiProvider = midiProvider ?? throw new ArgumentNullException(nameof(midiProvider));
        var rowSpecPrefix = commandBar == null ? "1r" : "1p;1r";
        var rowOffset = commandBar == null ? 0 : 1;
        // 16p for track headers, rest for grid
        layout = ProtectedPanel.Add(new GridLayout($"{rowSpecPrefix};{StatusBar.Height}p", "16p;1r")).Fill();

        // Add the track headers (left, all rows except command/status)
        TrackHeaders = layout.Add(new TrackHeadersPanel(this, session, tracks), 0, rowOffset);
        Composer = layout.Add(new Composer(session, tracks), 1, rowOffset);

        // Wire up selection highlighting
        TrackHeaders.TrackSelected.Subscribe(idx => Composer.SelectedTrackIndex = idx, this);

        StatusBar = layout.Add(new StatusBar(), column: 0, row: rowOffset + 1, columnSpan: 2);

        if (commandBar != null)
        {
            layout.Add(commandBar, 0, 0, columnSpan: 2);
        }

        Composer.StatusChanged.Subscribe(message => StatusBar.Message = message, this);
        // For bidirectional feedback: When track index changes, update highlight
        Composer.ModeChanging.Subscribe(_ => TrackHeaders.SelectedTrackIndex = Composer.SelectedTrackIndex, this);
    }
}

// Panel that renders all track names/colors and "add" button
public class TrackHeadersPanel : ConsoleControl
{
    private WorkspaceSession session;
    public List<ComposerTrack> Tracks => composer.Composer.Tracks;
    public int SelectedTrackIndex { get; set; }
    public Event<int> TrackSelected = Event<int>.Create();
    private ComposerWithTracks composer;

    public TrackHeadersPanel(ComposerWithTracks composer, WorkspaceSession session, List<ComposerTrack>? tracks = null)
    {
        this.composer = composer ?? throw new ArgumentNullException(nameof(composer));
        this.session = session;
        CanFocus = true;
        this.KeyInputReceived.Subscribe(OnKey, this);
    }

    private void OnKey(ConsoleKeyInfo info)
    {
        if(info.Key == ConsoleKey.T)
        {
            AddTrack();
        }
        else if(info.Key == ConsoleKey.Delete && Tracks.Count > 1)
        {
            DeleteSelectedTrack();
        }
        else if(info.Key == ConsoleKey.UpArrow || info.Key == ConsoleKey.W)
        {
            SelectedTrackIndex = Math.Max(0, SelectedTrackIndex - 1);
            TrackSelected.Fire(SelectedTrackIndex);
        }
        else if(info.Key == ConsoleKey.DownArrow || info.Key == ConsoleKey.S)
        {
            SelectedTrackIndex = Math.Min(Tracks.Count - 1, SelectedTrackIndex + 1);
            TrackSelected.Fire(SelectedTrackIndex);
        }
        else if(info.Key == ConsoleKey.A)
        {
            AddMelodyToTrack();
        }
        else if (info.Key == ConsoleKey.Z && info.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            session.Commands.Undo();
        }
        else if (info.Key == ConsoleKey.Y && info.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            session.Commands.Redo();
        }
    }

    private void AddMelodyToTrack()
    {
        var notes = new ListNoteSource();
        var startBeat = Tracks[SelectedTrackIndex].Melodies.Count > 0 ? Tracks[SelectedTrackIndex].Melodies.Max(m => m.StartBeat + m.DurationBeats) : 0;
        var newMelody = new MelodyClip(startBeat, notes);
        Tracks[SelectedTrackIndex].Melodies.Add(newMelody);

        OpenMelody(newMelody);
    }

    private void OpenMelody(MelodyClip melody)
    {
        var maxFocusDepth = Math.Max(ConsoleApp.Current.LayoutRoot.FocusStackDepth, ConsoleApp.Current.LayoutRoot.Descendents.Select(d => d.FocusStackDepth).Max());
        var newFocusDepth = maxFocusDepth + 1;
        var panel = ConsoleApp.Current.LayoutRoot.Add(new ConsolePanel() { FocusStackDepth = newFocusDepth }).Fill();
        var commandBar = new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both, Margin = 2, Orientation = Orientation.Horizontal };
        var pianoWithTimeline = panel.Add(new PianoWithTimeline(session, melody.Melody, commandBar)).Fill();
        var midi = DAWMidi.Create(composer.MidiProvider, pianoWithTimeline);
        commandBar.Add(midi.CreateMidiProductDropdown());

        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Escape, () => panel.Dispose(), panel);
        panel.OnDisposed(() =>
        {
            if (melody.Melody.Count == 0)
            {
                Tracks[SelectedTrackIndex].Melodies.Remove(melody);
            }
            composer.Composer.RefreshVisibleSet();
        });
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        int y = 1;
        for (int i = 0; i < Tracks.Count; i++)
        {
            bool selected = i == SelectedTrackIndex;
            var color = selected ? RGB.Black : GetTrackColor(i);
            var label = selected ? $"> {Tracks[i].Name}" : $"  {Tracks[i].Name}";
            context.DrawString(label, color, selected ? HasFocus ? RGB.Cyan : RGB.DarkGray : RGB.Black, 1, y);
            y += Composer.RowHeightChars; // keep in sync with composer track height
        }

        // Draw "+" add button at the bottom
        var hintBG = HasFocus ? "Cyan" : "DarkGray";
        context.DrawString(ConsoleString.Parse($"[Black][B={hintBG}] T [D][White] Add Track"), 1, y);
    }

    private void AddTrack()
    {
        var name = $"Track {Tracks.Count + 1}";
        session.Commands.Execute(new AddTrackCommand(composer, name));
    }

    private void DeleteSelectedTrack()
    {
        if (SelectedTrackIndex < 0 || SelectedTrackIndex >= Tracks.Count)
            return;
        var track = Tracks[SelectedTrackIndex];
        session.Commands.Execute(new DeleteTrackCommand(composer, track));
        SelectedTrackIndex = Math.Max(0, Math.Min(SelectedTrackIndex, Tracks.Count - 1));
        TrackSelected.Fire(SelectedTrackIndex);
    }

    private static RGB GetTrackColor(int index)
    {
        // Copy from Composer track color logic as needed
        RGB[] BaseTrackColors = new[]
        {
            new RGB(220, 60, 60),
            new RGB(60, 180, 90),
            new RGB(65, 105, 225),
            new RGB(240, 200, 60),
            new RGB(200, 60, 200),
            new RGB(50, 220, 210),
            new RGB(245, 140, 30),
        };
        float[] PaleFractions = { 0.0f, 0.35f, 0.7f };
        int baseCount = BaseTrackColors.Length;
        int shade = index / baseCount;
        int colorIdx = index % baseCount;
        float pale = PaleFractions[Math.Min(shade, PaleFractions.Length - 1)];
        RGB color = BaseTrackColors[colorIdx];
        return color.ToOther(RGB.White, pale);
    }
}
