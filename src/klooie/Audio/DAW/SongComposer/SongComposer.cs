using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

namespace klooie;

public partial class SongComposer : Composer<MelodyClip>
{
    public List<ComposerTrack> Tracks => Session.CurrentSong.Tracks;
    public SongComposerEditor Editor { get; }
    public int SelectedTrackIndex { get; set; }
    public IMidiProvider MidiProvider { get; private set; }

    public SongComposer(WorkspaceSession session, IMidiProvider midiProvider) : base(session, session.CurrentSong.Tracks.SelectMany(t => t.Melodies).ToList(), session.CurrentSong.BeatsPerMinute)
    {
        this.MidiProvider = midiProvider;

        if (Tracks == null || Tracks.Count == 0)
        {
            Tracks.Add(new ComposerTrack("Track 1", null));
        }
        Editor = new SongComposerEditor(session.Commands) { Composer = this };
    
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
        RefreshVisibleCells();
    }

    public void InsertTrack(int trackIndex, ComposerTrack track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        if (trackIndex < 0 || trackIndex > Tracks.Count)
            throw new ArgumentOutOfRangeException(nameof(trackIndex));
        Tracks.Insert(trackIndex, track);
        RefreshVisibleCells();
    }

    // Remove a track by index
    public void RemoveTrack(int trackIndex)
    {
        if (trackIndex >= 0 && trackIndex < Tracks.Count)
        {
            Tracks.RemoveAt(trackIndex);
            RefreshVisibleCells();
        }
    }

    protected override ComposerInputMode<MelodyClip>[] GetAvailableModes() => [new SongComposerNavigationMode() { Composer = this }, new SongComposerSelectionMode() { Composer = this }];

    protected override RGB GetColor(MelodyClip value)
    {
        var track = Tracks.FirstOrDefault(t => t.Melodies.Contains(value));
        var index = track != null ? Tracks.IndexOf(track) : 0;
        return ComposerCell<object>.GetColor(index);
    }

    public override Song Compose()
    {
        var notes = new ListNoteSource() { BeatsPerMinute = this.BeatsPerMinute };

        for (var i = 0; i < Tracks.Count; i++)
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


    public void OpenMelody(MelodyClip melody)
    {
        var maxFocusDepth = Math.Max(ConsoleApp.Current.LayoutRoot.FocusStackDepth, ConsoleApp.Current.LayoutRoot.Descendents.Select(d => d.FocusStackDepth).Max());
        var newFocusDepth = maxFocusDepth + 1;
        var panel = ConsoleApp.Current.LayoutRoot.Add(new ConsolePanel() { FocusStackDepth = newFocusDepth }).Fill();
        var commandBar = new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both, Margin = 2, Orientation = Orientation.Horizontal };
        var pianoWithTimeline = panel.Add(new PianoWithTimeline(WorkspaceSession.Current, melody.Melody, commandBar)).Fill();
        pianoWithTimeline.Timeline.Color = GetColor(melody);
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
            RefreshVisibleCells();
        });
    }

    protected override Viewport CreateViewport() => new SongComposerViewport();
    protected override CellPositionInfo GetCellPositionInfo(MelodyClip value) => new CellPositionInfo() {  BeatStart = value.StartBeat, BeatEnd = value.StartBeat + value.DurationBeats, IsHidden = false, Row = Tracks.IndexOf(Tracks.FirstOrDefault(t => t.Melodies.Contains(value))), };
    protected override double CalculateMaxBeat() => Tracks.SelectMany(t => t.Melodies.Select(m => m.StartBeat + m.DurationBeats)).DefaultIfEmpty(0).Max();

    protected override void OnPaint(ConsoleBitmap context)
    {
        base.OnPaint(context);
        CurrentMode?.Paint(context);
    }

    public override void HandleKeyInput(ConsoleKeyInfo k)
    { 
        if (k.Key == ConsoleKey.M)
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

