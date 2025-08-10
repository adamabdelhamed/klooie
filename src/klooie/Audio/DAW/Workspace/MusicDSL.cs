using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class MusicDSL
{
    private const double Epsilon = 1e-9;

    public static string WriteSongDsl_Aligned(SongInfo song)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"#TITLE: {song.Title}");
        sb.AppendLine($"#BPM: {song.BeatsPerMinute}");
        sb.AppendLine();

        foreach (var track in song.Tracks)
        {
            sb.AppendLine($"#TRACK: {track.Name}  INSTRUMENT: {track.Instrument.Name}");

            // Track-local registry of first fully-specified clip by name -> fingerprint
            var firstClipFingerprintByName = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var clip in track.Melodies.Where(m => m.Melody.Count > 0).OrderBy(m => m.StartBeat))
            {
                sb.AppendLine($"#CLIP: {clip.Name}  START: {clip.StartBeat}");

                // Sort notes for predictable grouping and fingerprinting
                var notes = clip.Melody.OrderBy(n => n.StartBeat).ToList();

                string fingerprint = ClipFingerprint(track, notes);

                // If we already saw a clip with the same name and identical content, compress to INHERITED
                if (firstClipFingerprintByName.TryGetValue(clip.Name, out var priorFp) && string.Equals(priorFp, fingerprint, StringComparison.Ordinal))
                {
                    sb.AppendLine("INHERITED");
                    sb.AppendLine();
                    continue;
                }

                // Otherwise, emit the full table and remember this as the template for the name
                if (!firstClipFingerprintByName.ContainsKey(clip.Name))
                {
                    firstClipFingerprintByName[clip.Name] = fingerprint;
                }

                // Decide if the instrument column is needed
                bool hasCustomInstrument = notes.Any(n => n.Instrument != null && n.Instrument.Name != track.Instrument.Name);

                // Precompute string columns for width (base values only; repeat suffix printed after columns)
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

                // Write rows with compression: X<count>@<step>
                int i = 0;
                while (i < notes.Count)
                {
                    var n0 = notes[i];
                    string startBeatStr = n0.StartBeat.ToString("0.###");
                    string durationStr = n0.DurationBeats.ToString("0.###");
                    string midiStr = n0.MidiNote.ToString();
                    string velocityStr = n0.Velocity.ToString();
                    string? instrStr = hasCustomInstrument ? (n0.Instrument?.Name ?? track.Instrument.Name) : null;

                    // Identify run
                    int runLen = 1;
                    double? step = null;

                    int j = i + 1;
                    while (j < notes.Count)
                    {
                        var prev = notes[j - 1];
                        var cur = notes[j];

                        // Same attributes?
                        if (!NearlyEquals(prev.DurationBeats, cur.DurationBeats)) break;
                        if (prev.MidiNote != cur.MidiNote) break;
                        if (prev.Velocity != cur.Velocity) break;
                        if (hasCustomInstrument)
                        {
                            var pInst = prev.Instrument?.Name ?? track.Instrument.Name;
                            var cInst = cur.Instrument?.Name ?? track.Instrument.Name;
                            if (!string.Equals(pInst, cInst, StringComparison.Ordinal)) break;
                        }

                        // Constant step?
                        double thisStep = cur.StartBeat - prev.StartBeat;
                        if (step == null)
                        {
                            step = thisStep;
                            if (step.Value < 0) break;
                        }
                        else
                        {
                            if (!NearlyEquals(thisStep, step.Value)) break;
                        }

                        runLen++;
                        j++;
                    }

                    // Emit one line, optionally with compression suffix
                    sb.Append(startBeatStr.PadRight(wStartBeat + 2));
                    sb.Append(durationStr.PadRight(wDuration + 2));
                    sb.Append(midiStr.PadRight(wMidiNote + 2));
                    sb.Append(velocityStr.PadRight(wVelocity + 2));
                    if (hasCustomInstrument)
                        sb.Append((instrStr ?? "").PadRight(wInstr + 2));

                    if (runLen > 1 && step.HasValue)
                    {
                        sb.Append($"X{runLen}@{step.Value:0.###}");
                    }
                    sb.AppendLine();

                    i += runLen;
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
        MelodyClip? previousClip = null;
        bool inNotesTable = false;
        int? instrColIdx = null;

        // Per-track templates: first fully-specified clip content by clip name
        Dictionary<string, List<(double start, double dur, int midi, int vel, string inst)>>? templatesByName = null;

        Action finalizePreviousClipIfNeeded = () =>
        {
            if (previousClip != null && templatesByName != null)
            {
                if (!templatesByName.ContainsKey(previousClip.Name) && previousClip.Melody.Count > 0)
                {
                    templatesByName[previousClip.Name] = previousClip.Melody
                        .OrderBy(n => n.StartBeat)
                        .Select(n => (
                            start: n.StartBeat,
                            dur: n.DurationBeats,
                            midi: n.MidiNote,
                            vel: n.Velocity,
                            inst: n.Instrument?.Name ?? currentTrack?.Instrument.Name ?? "Piano"
                        ))
                        .ToList();
                }
            }
        };

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.StartsWith("#TITLE:"))
            {
                finalizePreviousClipIfNeeded();
                song.Title = line.Substring(7).Trim();
                inNotesTable = false;
            }
            else if (line.StartsWith("#BPM:"))
            {
                finalizePreviousClipIfNeeded();
                song.BeatsPerMinute = double.Parse(line.Substring(5).Trim());
                inNotesTable = false;
            }
            else if (line.StartsWith("#TRACK:"))
            {
                finalizePreviousClipIfNeeded();
                var parts = line.Substring(7).Trim().Split(new[] { "  INSTRUMENT:" }, StringSplitOptions.None);
                var name = parts[0].Trim();
                var instName = parts.Length > 1 ? parts[1].Trim() : "Piano";
                currentTrack = new ComposerTrack(name, resolveInstrument(instName));
                song.Tracks.Add(currentTrack);

                // Reset per-track template registry
                templatesByName = new Dictionary<string, List<(double, double, int, int, string)>>(StringComparer.Ordinal);

                inNotesTable = false;
                previousClip = null;
            }
            else if (line.StartsWith("#CLIP:"))
            {
                finalizePreviousClipIfNeeded();

                var parts = line.Substring(6).Trim().Split(new[] { "  START:" }, StringSplitOptions.None);
                var name = parts[0].Trim();
                double startBeat = parts.Length > 1 ? double.Parse(parts[1].Trim()) : 0;
                currentClip = new MelodyClip { Name = name, StartBeat = startBeat, Melody = new ListNoteSource() };
                currentTrack?.Melodies.Add(currentClip);
                inNotesTable = false;
                previousClip = currentClip;
            }
            else if (string.Equals(line, "INHERITED", StringComparison.Ordinal))
            {
                // Expand from the first template with the same clip name on this track
                if (currentClip != null && templatesByName != null && templatesByName.TryGetValue(currentClip.Name, out var tmpl))
                {
                    foreach (var (start, dur, midi, vel, inst) in tmpl)
                    {
                        var instrument = resolveInstrument(inst);
                        var note = NoteExpression.Create(midi, start, dur, vel, instrument);
                        currentClip.Melody.Add(note);
                    }
                }
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
                // Note row (possibly with compression suffix)
                var tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 4) continue;

                // Optional repeat spec like X100@.5
                int repeatIdx = -1;
                int repeatCount = 1;
                double repeatStep = 0;

                for (int t = 4; t < tokens.Length; t++)
                {
                    if (TryParseRepeatSpec(tokens[t], out var c, out var s))
                    {
                        repeatIdx = t;
                        repeatCount = c;
                        repeatStep = s;
                        break;
                    }
                }

                double startBeat = double.Parse(tokens[0]);
                double duration = double.Parse(tokens[1]);
                int midi = int.Parse(tokens[2]);
                int velocity = int.Parse(tokens[3]);

                string instrName = (instrColIdx != null && instrColIdx >= 0 && tokens.Length > instrColIdx)
                    ? tokens[instrColIdx.Value]
                    : currentTrack?.Instrument.Name ?? "Piano";

                var instrument = resolveInstrument(instrName);

                int count = Math.Max(1, repeatCount);
                double step = (repeatIdx >= 0) ? repeatStep : 0.0;

                for (int k = 0; k < count; k++)
                {
                    var note = NoteExpression.Create(midi, startBeat + k * step, duration, velocity, instrument);
                    currentClip?.Melody.Add(note);
                }
            }
            else
            {
                inNotesTable = false;
            }
        }

        // Finalize last clip at EOF
        finalizePreviousClipIfNeeded();

        return song;
    }

    private static bool TryParseRepeatSpec(string token, out int count, out double step)
    {
        // Format: X<count>@<step>
        count = 1;
        step = 0;
        if (token.Length < 4) return false;
        if (token[0] != 'X' && token[0] != 'x') return false;

        int at = token.IndexOf('@');
        if (at <= 1 || at == token.Length - 1) return false;

        var countPart = token.Substring(1, at - 1);
        var stepPart = token.Substring(at + 1);

        if (!int.TryParse(countPart, out var c) || c < 1) return false;
        if (!double.TryParse(stepPart, out var s)) return false;

        count = c;
        step = s;
        return true;
    }

    private static bool NearlyEquals(double a, double b) => Math.Abs(a - b) <= Epsilon;

    private static string ClipFingerprint(ComposerTrack track, List<NoteExpression> notes)
    {
        // Normalize to a deterministic string independent of clip StartBeat.
        // Includes StartBeat (relative within clip), Duration, Midi, Velocity, and per-note instrument name.
        var sb = new StringBuilder(notes.Count * 24);
        foreach (var n in notes.OrderBy(n => n.StartBeat))
        {
            var inst = n.Instrument?.Name ?? track.Instrument.Name;
            sb.Append(n.StartBeat.ToString("0.###")).Append('|')
              .Append(n.DurationBeats.ToString("0.###")).Append('|')
              .Append(n.MidiNote).Append('|')
              .Append(n.Velocity).Append('|')
              .Append(inst).Append(';');
        }
        return sb.ToString();
    }
}
