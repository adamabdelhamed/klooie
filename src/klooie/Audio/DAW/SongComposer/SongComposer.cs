using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

namespace klooie;

// Represents a song with multiple tracks. Each track holds melody clips.
public partial class SongComposer : ProtectedConsolePanel
{
    public const double MaxBeatsPerColumn = 1.0;
    public const double MinBeatsPerColumn = 1.0 / 128;

    public Event<ConsoleString> StatusChanged { get; } = Event<ConsoleString>.Create();
    public SongComposerViewport Viewport { get; private init; }

    public List<ComposerTrack> Tracks => Session.CurrentSong.Tracks;

    // Selection: selected melody clips (never notes)
    public List<MelodyClip> SelectedMelodies { get; } = new();

    // For UI management: track live MelodyCells
    private readonly Dictionary<MelodyClip, MelodyCell> live = new();
    private HashSet<MelodyClip> visibleNow = new HashSet<MelodyClip>();
    private AlternatingBackgroundGrid backgroundGrid;
    public const int ColWidthChars = 1;
    public const int RowHeightChars = 3;
    private Recyclable? focusLifetime;
    public SongComposerPlayer Player { get; }
    private double beatsPerColumn = 1 / 8.0;

    public SongComposerEditor Editor { get; }
    public InstrumentExpression? Instrument { get; set; } = new InstrumentExpression() { Name = "Default", PatchFunc = SynthLead.Create };

    public double BeatsPerColumn
    {
        get => beatsPerColumn;
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(BeatsPerColumn));
            if (Math.Abs(beatsPerColumn - value) > 0.0001)
            {
                beatsPerColumn = value;
                UpdateViewportBounds();
                RefreshVisibleSet();
            }
        }
    }

    public Song Compose()
    {
        var notes = new ListNoteSource() { BeatsPerMinute = Tracks.First().Melodies.FirstOrDefault()?.Melody.BeatsPerMinute ?? 60 };

        for(var i = 0; i < Tracks.Count; i++)
        {
            for (int j = 0; j < Tracks[i].Melodies.Count; j++)
            {
                var melody = Tracks[i].Melodies[j];
                for (int k = 0; k < melody.Melody.Count; k++)
                {
                    var originalNote = melody.Melody[k];
                    var noteWithOffset = NoteExpression.Create(originalNote.MidiNote, melody.StartBeat + originalNote.StartBeat, originalNote.DurationBeats, originalNote.BeatsPerMinute, originalNote.Velocity, originalNote.Instrument);
                    notes.Add(noteWithOffset);
                }
            }
        }
        var song = new Song(notes, notes.BeatsPerMinute);
        return song;
    }

    public double CurrentBeat => Player.CurrentBeat;
    public double MaxBeat { get; private set; }

    private Dictionary<string, RGB> trackColorMap = new();

    private readonly SongComposerInputMode[] userCyclableModes;
    public SongComposerInputMode CurrentMode { get; private set; }
    public Event<SongComposerInputMode> ModeChanging { get; } = Event<SongComposerInputMode>.Create();

    // Which track (by index) is currently selected for editing
    public int SelectedTrackIndex { get; set; }

    public WorkspaceSession Session { get; private init; }

    public IMidiProvider MidiProvider { get; private set; }

    public SongComposer(WorkspaceSession session, IMidiProvider midiProvider)
    {
        this.Session = session;
        this.MidiProvider = midiProvider;
        this.userCyclableModes = [new SongComposerNavigationMode() { Composer = this }, new SongComposerSelectionMode() { Composer = this }];
        Viewport = new SongComposerViewport(this);
        Player = new SongComposerPlayer(this);

        CanFocus = true;
        ProtectedPanel.Background = new RGB(240, 240, 240);
        BoundsChanged.Sync(UpdateViewportBounds, this);
        Focused.Subscribe(EnableKeyboardInput, this);
        backgroundGrid = ProtectedPanel.Add(new AlternatingBackgroundGrid(0, RowHeightChars, new RGB(240, 240, 240), new RGB(220, 220, 220), RGB.Cyan.ToOther(RGB.Gray.Brighter, .95f), () => HasFocus)).Fill();
        Viewport.SubscribeToAnyPropertyChange(backgroundGrid, _ => UpdateAlternatingBackgroundOffset(), backgroundGrid);
        ConsoleApp.Current.InvokeNextCycle(RefreshVisibleSet);

        // Load provided tracks or make a blank one if none given
        if (Tracks == null || Tracks.Count == 0)
        {
            Tracks.Add(new ComposerTrack("Track 1", Instrument!));
        }

        BuildTrackColorMap();
        UpdateMaxBeat();

        Player.BeatChanged.Subscribe(this, static (me, b) => me.RefreshVisibleSet(), this);
        CurrentMode = this.userCyclableModes[0];
        Editor = new SongComposerEditor(session.Commands) { Composer = this };
        Player.Stopped.Subscribe(this, static (me) => me.StatusChanged.Fire(ConsoleString.Parse("[White]Stopped.")), this);

        RefreshVisibleSet();
    }

    // Add a new track with default instrument
    public void AddTrack(string name, InstrumentExpression? instrument = null)
    {
        instrument ??= new InstrumentExpression() { Name = "Default", PatchFunc = SynthLead.Create };
        AddTrack(new ComposerTrack(name, instrument));
    }

    public void AddTrack(ComposerTrack track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        Tracks.Add(track);
        BuildTrackColorMap();
        UpdateMaxBeat();
        RefreshVisibleSet();
    }

    public void InsertTrack(int trackIndex, ComposerTrack track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        if (trackIndex < 0 || trackIndex > Tracks.Count)
            throw new ArgumentOutOfRangeException(nameof(trackIndex));
        Tracks.Insert(trackIndex, track);
        BuildTrackColorMap();
        UpdateMaxBeat();
        RefreshVisibleSet();
    }

    // Remove a track by index
    public void RemoveTrack(int trackIndex)
    {
        if (trackIndex >= 0 && trackIndex < Tracks.Count)
        {
            Tracks.RemoveAt(trackIndex);
            BuildTrackColorMap();
            UpdateMaxBeat();
            RefreshVisibleSet();
        }
    }

    private void BuildTrackColorMap()
    {
        for (int i = 0; i < Tracks.Count; i++)
        {
            trackColorMap[Tracks[i].Name] = GetTrackColor(i);
        }
    }

    private static readonly RGB[] BaseTrackColors = new[]
    {
        new RGB(220, 60, 60),
        new RGB(60, 180, 90),
        new RGB(65, 105, 225),
        new RGB(240, 200, 60),
        new RGB(200, 60, 200),
        new RGB(50, 220, 210),
        new RGB(245, 140, 30),
    };

    private static readonly float[] PaleFractions = new[]
    {
        0.0f,
        0.35f,
        0.7f,
    };

    private RGB GetTrackColor(int index)
    {
        int baseCount = BaseTrackColors.Length;
        int shade = index / baseCount;
        int colorIdx = index % baseCount;
        float pale = PaleFractions[Math.Min(shade, PaleFractions.Length - 1)];
        RGB color = BaseTrackColors[colorIdx];
        return color.ToOther(RGB.White, pale);
    }

    private void UpdateMaxBeat()
    {
        MaxBeat = Tracks
            .SelectMany(t => t.Melodies.Select(m => m.StartBeat + m.DurationBeats))
            .DefaultIfEmpty(0)
            .Max();
    }

    public void SetMode(SongComposerInputMode mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        ModeChanging.Fire(mode);
        CurrentMode.Enter();
    }

    public void NextMode()
    {
        int i = Array.IndexOf(userCyclableModes, CurrentMode);
        SetMode(userCyclableModes[(i + 1) % userCyclableModes.Length]);
    }

    public void StopPlayback() => Player.Stop();

    protected override void OnPaint(ConsoleBitmap context)
    {
        base.OnPaint(context);
        CurrentMode?.Paint(context);
    }


    private void UpdateAlternatingBackgroundOffset()
    {
        backgroundGrid.CurrentOffset = ConsoleMath.Round(Viewport.TracksOnScreen / (double)RowHeightChars);
    }

    private void UpdateViewportBounds()
    {
        Viewport.BeatsOnScreen = Math.Max(1, Width * BeatsPerColumn / ColWidthChars);
        Viewport.TracksOnScreen = Math.Max(1, Height / RowHeightChars);
    }

    public void EnableKeyboardInput()
    {
        focusLifetime?.TryDispose();
        focusLifetime = DefaultRecyclablePool.Instance.Rent();
        Unfocused.SubscribeOnce(() => focusLifetime.TryDispose());
        ConsoleApp.Current.GlobalKeyPressed.Subscribe(async k =>
        {
            if (k.Key == ConsoleKey.Spacebar)
            {
                if (Player.IsPlaying) Player.Pause();
                else Player.Play();
            }
            else if (k.Key == ConsoleKey.OemPlus || k.Key == ConsoleKey.Add)
            {
                if (BeatsPerColumn / 2 >= MinBeatsPerColumn)
                    BeatsPerColumn /= 2;
            }
            else if (k.Key == ConsoleKey.OemMinus || k.Key == ConsoleKey.Subtract)
            {
                if (BeatsPerColumn * 2 <= MaxBeatsPerColumn)
                    BeatsPerColumn *= 2;
            }
            else if (k.Key == ConsoleKey.M)
            {
                NextMode();
            }
            else if (k.Key == ConsoleKey.UpArrow)
            {
                SelectedTrackIndex = Math.Max(0, SelectedTrackIndex - 1);
            }
            else if (k.Key == ConsoleKey.DownArrow)
            {
                SelectedTrackIndex = Math.Min(Tracks.Count - 1, SelectedTrackIndex + 1);
            }
            else if (!Editor.HandleKeyInput(k))
            {
                CurrentMode.HandleKeyInput(k);
            }
        }, focusLifetime);
    }

    // Shows only melody clips in visible range, for all tracks.
    public void RefreshVisibleSet()
    {
        visibleNow.Clear();

        int trackTop = Viewport.FirstVisibleTrack;
        int trackBot = trackTop + Viewport.TracksOnScreen - 1;

        for (int t = 0; t < Tracks.Count; t++)
        {
            if (t < trackTop || t > trackBot) continue;
            var track = Tracks[t];

            foreach (var melody in track.Melodies)
            {
                double melodyStart = melody.StartBeat;
                double melodyEnd = melody.StartBeat + melody.DurationBeats;

                bool isVisible = (melodyEnd >= Viewport.FirstVisibleBeat) && (melodyStart <= Viewport.LastVisibleBeat);
                if (!isVisible) continue;
                visibleNow.Add(melody);

                if (!live.TryGetValue(melody, out MelodyCell cell))
                {
                    cell = ProtectedPanel.Add(new MelodyCell(melody) { ZIndex = 1 });
                    live[melody] = cell;
                }

                // Determine color (by track, by selection, etc)
                cell.Background = SelectedMelodies.Contains(melody)
                    ? SongComposerSelectionMode.SelectedMelodyColor
                    : trackColorMap.TryGetValue(track.Name, out var color) ? color
                    : RGB.Orange;

                // Position & size cell
                PositionCell(cell, t);
            }
        }

        // Remove cells that are no longer visible
        foreach (var kvp in live.ToArray())
        {
            if (!visibleNow.Contains(kvp.Key))
            {
                kvp.Value.Dispose();
                live.Remove(kvp.Key);
            }
        }

        Editor.PositionAddClipPreview();
    }

    private void PositionCell(MelodyCell cell, int trackIndex)
    {
        double beatsFromLeft = cell.Melody.StartBeat - Viewport.FirstVisibleBeat;

        int x = ConsoleMath.Round((cell.Melody.StartBeat - Viewport.FirstVisibleBeat) / BeatsPerColumn) * ColWidthChars;
        int y = (trackIndex - Viewport.FirstVisibleTrack) * RowHeightChars;

        double durBeats = cell.Melody.DurationBeats;
        int w = (int)Math.Max(1, ConsoleMath.Round(durBeats / BeatsPerColumn) * ColWidthChars);
        int h = RowHeightChars;

        cell.MoveTo(x, y);
        cell.ResizeTo(w, h);
    }

    internal ConsoleControl AddPreviewControl() => ProtectedPanel.Add(new ConsoleControl());

    public void OpenMelody(MelodyClip melody)
    {
        var maxFocusDepth = Math.Max(ConsoleApp.Current.LayoutRoot.FocusStackDepth, ConsoleApp.Current.LayoutRoot.Descendents.Select(d => d.FocusStackDepth).Max());
        var newFocusDepth = maxFocusDepth + 1;
        var panel = ConsoleApp.Current.LayoutRoot.Add(new ConsolePanel() { FocusStackDepth = newFocusDepth }).Fill();
        var commandBar = new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both, Margin = 2, Orientation = Orientation.Horizontal };
        var pianoWithTimeline = panel.Add(new PianoWithTimeline(WorkspaceSession.Current, melody.Melody, commandBar)).Fill();
        pianoWithTimeline.Timeline.Focus();
        var midi = DAWMidi.Create(MidiProvider, pianoWithTimeline);
        commandBar.Add(midi.CreateMidiProductDropdown());

        var instrumentPicker = InstrumentPicker.CreatePickerDropdown();
        commandBar.Add(instrumentPicker);
        instrumentPicker.ValueChanged.Subscribe(() =>
        {
            melody.Melody.ForEach(n => n.Instrument = instrumentPicker.Value.Value as InstrumentExpression);
            pianoWithTimeline.Timeline.Instrument = instrumentPicker.Value.Value as InstrumentExpression;
        }, instrumentPicker);

        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Escape, () => panel.Dispose(), panel);
        panel.OnDisposed(() =>
        {
            midi.Dispose();
            if (melody.Melody.Count == 0)
            {
                Tracks[SelectedTrackIndex].Melodies.Remove(melody);
            }
            RefreshVisibleSet();
        });
    }
}

// Represents a melody "clip" in the composer, with a position and reference to the source melody
public class MelodyClip
{
    public double StartBeat { get; set; }

    [JsonIgnore]
    public double DurationBeats => Melody.Select(n => n.EndBeat).MaxOrDefault(0);
    public ListNoteSource Melody { get; set; }
    public string Name { get; set; } = "Melody Clip";

    public MelodyClip(double startBeat, ListNoteSource melody)
    {
        StartBeat = startBeat;
        Melody = melody;
    }

    public MelodyClip() { }
}

// A composer track holds a list of non-overlapping melody clips
public class ComposerTrack
{
    public string Name { get; set; }
    public InstrumentExpression Instrument { get; set; }
    public List<MelodyClip> Melodies { get; set; } = new();

    public ComposerTrack() { }

    public ComposerTrack(string name, InstrumentExpression instrument)
    {
        Name = name;
        Instrument = instrument;
    }
}

// For UI: Represents a cell for a melody clip (implement as needed)
public class MelodyCell : ConsoleControl
{
    public MelodyClip Melody { get; }
 
    public MelodyCell(MelodyClip melody)
    {
        CanFocus = false;
        Melody = melody;
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        base.OnPaint(context);

        var referenceBackgroundColor = context.GetPixel(0, 0).BackgroundColor;
        var borderForeground = referenceBackgroundColor.ToOther(RGB.Black,.3f);
        context.DrawRect(new ConsoleCharacter('#', borderForeground, referenceBackgroundColor), 0, 0, Width, Height);
    }
}
