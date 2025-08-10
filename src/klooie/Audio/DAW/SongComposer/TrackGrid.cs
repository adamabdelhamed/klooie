using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using static System.Collections.Specialized.BitVector32;

namespace klooie;

public partial class TrackGrid : BeatGrid<MelodyClip>
{
    public List<ComposerTrack> Tracks => Session.CurrentSong.Tracks;
    public IMidiProvider MidiProvider { get; private set; }

    private TrackGridEditor editor;

    protected override IEnumerable<MelodyClip> EnumerateValues() => Session.CurrentSong.Tracks.SelectMany(t => t.Clips);
    protected override BeatCell<MelodyClip> BeatCellFactory(MelodyClip value) => new MelodyClipCell(value, this, Viewport);
    public TrackGrid(WorkspaceSession session, TrackGridEditor editor, IMidiProvider midiProvider) : base(session)
    {
        Viewport.RowHeightChars = 3;
        ConsoleApp.Current.InvokeNextCycle(() => Viewport.SetFirstVisibleRow(0));
        this.MidiProvider = midiProvider;
        this.editor = editor;
        if (Tracks == null || Tracks.Count == 0)
        {
            Tracks.Add(new ComposerTrack("Track 1", null));
        }
 
    }

    // Add a new track with default instrument
    public void AddTrack(string name, InstrumentExpression? instrument = null)
    {
        instrument ??= InstrumentPicker.GetAllKnownInstruments().FirstOrDefault();
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

    protected override BeatGridInputMode<MelodyClip>[] GetAvailableModes() => [new TrackGridNavigator() { Composer = this }, new TrackGridSelector() { Composer = this }];

    protected override RGB GetColor(MelodyClip value)
    {
        var track = Tracks.FirstOrDefault(t => t.Clips.Contains(value));
        var index = track != null ? Tracks.IndexOf(track) : 0;
        return BeatCell<object>.GetColor(index);
    }

    public override Song Compose()
    {
        var notes = new ListNoteSource() { BeatsPerMinute = WorkspaceSession.Current.CurrentSong.BeatsPerMinute };

        for (var i = 0; i < Tracks.Count; i++)
        {
            for (int j = 0; j < Tracks[i].Clips.Count; j++)
            {
                var melody = Tracks[i].Clips[j];
                for (int k = 0; k < melody.Notes.Count; k++)
                {
                    var originalNote = melody.Notes[k];
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
        var track = Tracks.FirstOrDefault(t => t.Clips.Contains(melody));
        var melodyComposer = panel.Add(new MelodyComposer(WorkspaceSession.Current, track, melody.Notes, MidiProvider)).Fill();
        melodyComposer.Grid.Color = GetColor(melody);
        melodyComposer.Grid.Focus();

        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Escape, () => panel.Dispose(), panel);
        panel.OnDisposed(() =>
        {
            if (melody.Notes.Count == 0)
            {
                var track = Tracks.FirstOrDefault(t => t.Clips.Contains(melody));
                track.Clips.Remove(melody);
            }
            RefreshVisibleCells();
        });
    }


    protected override CellPositionInfo GetCellPositionInfo(MelodyClip value) => new CellPositionInfo() {  BeatStart = value.StartBeat, BeatEnd = value.StartBeat + value.DurationBeats, IsHidden = false, Row = Tracks.IndexOf(Tracks.FirstOrDefault(t => t.Clips.Contains(value))), };
    protected override double CalculateMaxBeat() => Tracks.SelectMany(t => t.Clips.Select(m => m.StartBeat + m.DurationBeats)).DefaultIfEmpty(0).Max();

 

    public override void HandleKeyInput(ConsoleKeyInfo k)
    { 
        if (k.Key == ConsoleKey.M)
        {
            NextMode();
        }
        else if (!editor.HandleKeyInput(k))
        {
            CurrentMode.HandleKeyInput(k);
        }
    }
}

// Represents a melody "clip" in the composer, with a position and reference to the source melody
public class MelodyClip : IEquatable<MelodyClip>
{
    public double StartBeat { get; set; }

    [JsonIgnore]
    public double DurationBeats => Notes.Select(n => n.EndBeat).MaxOrDefault(0);
    public ListNoteSource Notes { get; set; }
    public string Name { get; set; } = "Melody Clip";

    public MelodyClip(double startBeat, ListNoteSource melody)
    {
        StartBeat = startBeat;
        Notes = melody;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as MelodyClip);
    }

    public bool Equals(MelodyClip? other)
    {
        if (other is null) return false;
        if(other.StartBeat != StartBeat) return false;
        for(var i = 0; i < Notes.Count; i++)
        {
            if (i >= other.Notes.Count || !Notes[i].Equals(other.Notes[i]))
                return false;
        }
        return true;
    }

    public MelodyClip() { }
}

// A composer track holds a list of non-overlapping melody clips
public class ComposerTrack
{
    public string Name { get; set; }
    public InstrumentExpression Instrument { get; set; }
    public List<MelodyClip> Clips { get; set; } = new();

    public ComposerTrack() { }

    public ComposerTrack(string name, InstrumentExpression instrument)
    {
        Name = name;
        Instrument = instrument;
    }
}

