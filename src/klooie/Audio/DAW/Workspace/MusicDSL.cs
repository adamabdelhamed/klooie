using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class MusicDSL
{
    public static string WriteSongDsl_Aligned(SongInfo song)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"#TITLE: {song.Title}");
        sb.AppendLine($"#BPM: {song.BeatsPerMinute}");
        sb.AppendLine();

        foreach (var track in song.Tracks)
        {
            sb.AppendLine($"#TRACK: {track.Name}  INSTRUMENT: {track.Instrument.Name}");
            foreach (var clip in track.Melodies)
            {
                sb.AppendLine($"#CLIP: {clip.Name}  START: {clip.StartBeat}");

                var notes = clip.Melody;
                // Decide if the instrument column is needed
                bool hasCustomInstrument = notes.Any(n => n.Instrument != null && n.Instrument.Name != track.Instrument.Name);

                // Build columns as string arrays
                var colStartBeat = notes.Select(n => n.StartBeat.ToString("0.###")).ToList();
                var colDuration = notes.Select(n => n.DurationBeats.ToString("0.###")).ToList();
                var colMidiNote = notes.Select(n => n.MidiNote.ToString()).ToList();
                var colVelocity = notes.Select(n => n.Velocity.ToString()).ToList();
                var colInstr = hasCustomInstrument
                                    ? notes.Select(n => n.Instrument?.Name ?? track.Instrument.Name).ToList()
                                    : null;

                // Compute column widths
                int wStartBeat = Math.Max("StartBeat".Length, colStartBeat.Max(s => s.Length));
                int wDuration = Math.Max("Duration".Length, colDuration.Max(s => s.Length));
                int wMidiNote = Math.Max("MidiNote".Length, colMidiNote.Max(s => s.Length));
                int wVelocity = Math.Max("Velocity".Length, colVelocity.Max(s => s.Length));
                int wInstr = hasCustomInstrument
                                    ? Math.Max("Instrument".Length, colInstr!.Max(s => s.Length))
                                    : 0;

                // Write header
                sb.Append("StartBeat".PadRight(wStartBeat + 2));
                sb.Append("Duration".PadRight(wDuration + 2));
                sb.Append("MidiNote".PadRight(wMidiNote + 2));
                sb.Append("Velocity".PadRight(wVelocity + 2));
                if (hasCustomInstrument)
                    sb.Append("Instrument".PadRight(wInstr + 2));
                sb.AppendLine();

                // Write rows
                for (int i = 0; i < notes.Count; i++)
                {
                    sb.Append(colStartBeat[i].PadRight(wStartBeat + 2));
                    sb.Append(colDuration[i].PadRight(wDuration + 2));
                    sb.Append(colMidiNote[i].PadRight(wMidiNote + 2));
                    sb.Append(colVelocity[i].PadRight(wVelocity + 2));
                    if (hasCustomInstrument)
                        sb.Append(colInstr![i].PadRight(wInstr + 2));
                    sb.AppendLine();
                }

                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    public static SongInfo ParseSongDsl_Aligned(string dsl, Func<string, InstrumentExpression> resolveInstrument)
    {
        var lines = dsl.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var song = new SongInfo();
        ComposerTrack? currentTrack = null;
        MelodyClip? currentClip = null;
        bool inNotesTable = false;
        int? instrColIdx = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("#TITLE:"))
            {
                song.Title = line.Substring(7).Trim();
                inNotesTable = false;
            }
            else if (line.StartsWith("#BPM:"))
            {
                song.BeatsPerMinute = double.Parse(line.Substring(5).Trim());
                inNotesTable = false;
            }
            else if (line.StartsWith("#TRACK:"))
            {
                var parts = line.Substring(7).Trim().Split(new[] { "  INSTRUMENT:" }, StringSplitOptions.None);
                var name = parts[0].Trim();
                var instName = parts.Length > 1 ? parts[1].Trim() : "Piano";
                currentTrack = new ComposerTrack(name, resolveInstrument(instName));
                song.Tracks.Add(currentTrack);
                inNotesTable = false;
            }
            else if (line.StartsWith("#CLIP:"))
            {
                var parts = line.Substring(6).Trim().Split(new[] { "  START:" }, StringSplitOptions.None);
                var name = parts[0].Trim();
                double startBeat = parts.Length > 1 ? double.Parse(parts[1].Trim()) : 0;
                currentClip = new MelodyClip { Name = name, StartBeat = startBeat, Melody = new ListNoteSource() };
                currentTrack?.Melodies.Add(currentClip);
                inNotesTable = false;
            }
            else if (line.StartsWith("StartBeat"))
            {
                // Parse header to know if "Instrument" is present and its index
                var cols = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                instrColIdx = Array.IndexOf(cols, "Instrument");
                inNotesTable = true;
            }
            else if (inNotesTable && !line.StartsWith("#") && !string.IsNullOrWhiteSpace(line))
            {
                // Note row
                var tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 4) continue;
                double startBeat = double.Parse(tokens[0]);
                double duration = double.Parse(tokens[1]);
                int midi = int.Parse(tokens[2]);
                int velocity = int.Parse(tokens[3]);
                string instrName = (instrColIdx != null && instrColIdx >= 0 && tokens.Length > instrColIdx)
                    ? tokens[instrColIdx.Value]
                    : currentTrack?.Instrument.Name ?? "Piano";
                var instrument = resolveInstrument(instrName);
                var note = NoteExpression.Create(midi, startBeat, duration, velocity, instrument);
                currentClip?.Melody.Add(note);
            }
            else
            {
                inNotesTable = false;
            }
        }
        return song;
    }


}
