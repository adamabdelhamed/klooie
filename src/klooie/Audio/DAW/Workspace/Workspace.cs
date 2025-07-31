using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace klooie;

public class Workspace
{
    public string RootDirectory { get; }
    public string PatchesDirectory => Path.Combine(RootDirectory, "patches");
    public string SongsDirectory => Path.Combine(RootDirectory, "songs");
    public string InstrumentsPath => Path.Combine(RootDirectory, "instruments.json");
    public string WorkspaceSettingsPath => Path.Combine(RootDirectory, "workspace.json");

    public List<InstrumentInfo> Instruments { get; } = new();
    public List<SongInfo> Songs { get; } = new();
    public List<PatchFactoryInfo> PatchFactories { get; } = new();
    public WorkspaceSettings Settings { get; private set; } = new();

    public Workspace(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    // --- Load workspace (from existing directory) ---
    public static async Task<Workspace> LoadAsync(string directory)
    {
        var ws = new Workspace(directory);
        await ws.LoadAllAsync();
        return ws;
    }

    // --- Create workspace (new directory) ---
    public static async Task<Workspace> CreateNewAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "patches"));
        Directory.CreateDirectory(Path.Combine(directory, "songs"));

        var ws = new Workspace(directory);
        await File.WriteAllTextAsync(ws.InstrumentsPath, "[]");
        await File.WriteAllTextAsync(ws.WorkspaceSettingsPath, "{}");
        ws.Instruments.Clear();
        ws.Songs.Clear();
        ws.PatchFactories.Clear();
        ws.Settings = new WorkspaceSettings();
        return ws;
    }

    public async Task LoadAllAsync()
    {
        // Instruments
        if (File.Exists(InstrumentsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(InstrumentsPath);
                var instruments = JsonSerializer.Deserialize<List<InstrumentInfo>>(json);
                Instruments.Clear();
                if (instruments != null)
                    Instruments.AddRange(instruments);
            }
            catch
            {
                Instruments.Clear();
            }
        }

        // Songs
        Songs.Clear();
        if (Directory.Exists(SongsDirectory))
        {
            foreach (var file in Directory.GetFiles(SongsDirectory, "*.song.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var song = JsonSerializer.Deserialize<SongInfo>(json);
                    if (song != null)
                    {
                        song.Filename = file;
                        Songs.Add(song);
                    }
                }
                catch { /* skip failed */ }
            }
        }

        // Patch factories: you must call DiscoverPatchFactories() after loading/compiling patches.
        PatchFactories.Clear();
        // e.g. after compilation: ws.PatchFactories.AddRange(SynthTweaker.GetAllPatchFactories());

        // Settings
        if (File.Exists(WorkspaceSettingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(WorkspaceSettingsPath);
                var settings = JsonSerializer.Deserialize<WorkspaceSettings>(json);
                if (settings != null)
                    Settings = settings;
            }
            catch { }
        }
    }

    // --- Save all data (except patches, which are hot-reloadable .cs files) ---
    public async Task SaveAsync()
    {
        var instrumentsJson = JsonSerializer.Serialize(Instruments, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(InstrumentsPath, instrumentsJson);

        foreach (var song in Songs)
        {
            if (song.Filename == null)
            {
                song.Filename = Path.Combine(SongsDirectory, MakeSafeFileName(song.Title ?? "Untitled") + ".song.json");
            }
            var songJson = JsonSerializer.Serialize(song, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(song.Filename, songJson);
        }

        var settingsJson = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(WorkspaceSettingsPath, settingsJson);
    }

    // --- Consistent auto-saving mutations! ---

    public async Task AddSongAsync(SongInfo song)
    {
        if (song.Filename == null)
        {
            song.Filename = Path.Combine(SongsDirectory, MakeSafeFileName(song.Title ?? "Untitled") + ".song.json");
        }
        Songs.Add(song);
        await SaveAsync();
    }

    public async Task RemoveSongAsync(SongInfo song)
    {
        if (song.Filename != null && File.Exists(song.Filename))
        {
            File.Delete(song.Filename);
        }
        Songs.Remove(song);
        await SaveAsync();
    }

    public async Task UpdateSongAsync(SongInfo song)
    {
        // Replace the in-memory song with the new one (by reference)
        var idx = Songs.FindIndex(s => s.Filename == song.Filename);
        if (idx >= 0)
        {
            Songs[idx] = song;
        }
        await SaveAsync();
    }

    public async Task AddInstrumentAsync(InstrumentInfo instrument)
    {
        Instruments.Add(instrument);
        await SaveAsync();
    }

    public async Task RemoveInstrumentAsync(InstrumentInfo instrument)
    {
        Instruments.Remove(instrument);
        await SaveAsync();
    }

    public async Task UpdateInstrumentAsync(InstrumentInfo instrument)
    {
        var idx = Instruments.FindIndex(i => i.Name == instrument.Name);
        if (idx >= 0)
        {
            Instruments[idx] = instrument;
        }
        await SaveAsync();
    }

    public async Task UpdateSettingsAsync(Action<WorkspaceSettings> update)
    {
        update(Settings);
        await SaveAsync();
    }

    private static string MakeSafeFileName(string input)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            input = input.Replace(c, '_');
        return input;
    }

    // --- Instrument Resolver ---

    public InstrumentExpression? ResolveInstrument(InstrumentInfo info)
    {
        var factory = PatchFactories.FirstOrDefault(f => f.QualifiedName == info.PatchFactoryQualifiedName);
        if (factory != null)
        {
            return InstrumentExpression.Create(info.Name, factory.Factory);
        }
        return null;
    }

    public List<(InstrumentInfo Info, InstrumentExpression? Expression)> GetResolvedInstruments()
        => Instruments.Select(i => (i, ResolveInstrument(i))).ToList();
}

// --- InstrumentInfo: persisted in instruments.json ---
public class InstrumentInfo
{
    public string Name { get; set; } = "";
    public string PatchFactoryQualifiedName { get; set; } = ""; // "klooie.DrumKit.Kick"
    public string? PatchSourceFilename { get; set; }
}

// --- SongInfo: persisted in songs/*.song.json ---
public class SongInfo
{
    public string? Title { get; set; }
    public string? Filename { get; set; }
    public double BeatsPerMinute { get; set; }
    public List<NoteExpression> Notes { get; set; } = new();
}

// --- PatchFactoryInfo: one per static ISynthPatch factory method ---
public class PatchFactoryInfo
{
    public string QualifiedName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public Func<ISynthPatch> Factory { get; set; } = null!;
    public string? Category { get; set; }
    public string? Documentation { get; set; }
}

// --- WorkspaceSettings: workspace.json ---
public class WorkspaceSettings
{
    public string? LastOpenedSong { get; set; }
    public string? LastMidiDevice { get; set; }
}
