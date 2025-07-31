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


    public static async Task<Workspace> Bootstrap()
    {
        var rootDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\klooie.DAW";
        if (Directory.Exists(rootDirectory) == false) Directory.CreateDirectory(rootDirectory);
        var mru = MRUWorkspaceSettings.Load(rootDirectory);
        var workspace = mru.LastOpenedWorkspace != null ? await Workspace.LoadAsync(mru.LastOpenedWorkspace) : await Workspace.CreateNewAsync(rootDirectory);
        mru.LastOpenedWorkspace = workspace.RootDirectory;
        mru.Save(rootDirectory);
        return workspace;
    }

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

    private async Task SaveAsync()
    {
        // Synchronously serialize all JSON
        var instrumentsJson = JsonSerializer.Serialize(Instruments, new JsonSerializerOptions { WriteIndented = true });
        var settingsJson = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });

        // Prepare song file paths and JSON
        var songFilePaths = new List<string>(Songs.Count);
        var songJsons = new List<string>(Songs.Count);

        foreach (var song in Songs)
        {
            if (song.Filename == null)
            {
                song.Filename = Path.Combine(SongsDirectory, MakeSafeFileName(song.Title ?? "Untitled") + ".song.json");
            }
            songFilePaths.Add(song.Filename);
            songJsons.Add(JsonSerializer.Serialize(song, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Asynchronously write all files
        var fileWriteTasks = new List<Task>
    {
        File.WriteAllTextAsync(InstrumentsPath, instrumentsJson),
        File.WriteAllTextAsync(WorkspaceSettingsPath, settingsJson)
    };

        for (int i = 0; i < songFilePaths.Count; i++)
        {
            fileWriteTasks.Add(File.WriteAllTextAsync(songFilePaths[i], songJsons[i]));
        }

        await Task.WhenAll(fileWriteTasks);
    }


    // --- Consistent auto-saving mutations! ---

    public void AddSong(SongInfo song)
    {
        if (song.Filename == null)
        {
            song.Filename = Path.Combine(SongsDirectory, MakeSafeFileName(song.Title ?? "Untitled") + ".song.json");
        }
        Songs.Add(song);
        SaveDebounced();
    }

    public void RemoveSong(SongInfo song)
    {
        if (song.Filename != null && File.Exists(song.Filename))
        {
            File.Delete(song.Filename);
        }
        Songs.Remove(song);
        SaveDebounced();
    }

    public void UpdateSong(SongInfo song)
    {
        // Replace the in-memory song with the new one (by reference)
        var idx = Songs.FindIndex(s => s.Filename == song.Filename);
        if (idx >= 0)
        {
            Songs[idx] = song;
        }
        SaveDebounced();
    }

    public void AddInstrument(InstrumentInfo instrument)
    {
        Instruments.Add(instrument);
        SaveDebounced();
    }

    public void InsertInstrument(int index, InstrumentInfo instrument)
    {
        if (index < 0 || index > Instruments.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be within the range of the Instruments list.");
        
        Instruments.Insert(index, instrument);
        SaveDebounced();
    }

    public void RemoveInstrument(InstrumentInfo instrument)
    {
        Instruments.Remove(instrument);
        SaveDebounced();
    }

    public void UpdateInstrument(InstrumentInfo instrument)
    {
        var idx = Instruments.FindIndex(i => i.Name == instrument.Name);
        if (idx >= 0)
        {
            Instruments[idx] = instrument;
        }
        SaveDebounced();
    }

    public void UpdateSettings(Action<WorkspaceSettings> update)
    {
        update(Settings);
        SaveDebounced();
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

    private Recyclable? saveLifetime;
    private bool isSaving;
    private void SaveDebounced()
    {
        if(isSaving)
        {
            ConsoleApp.Current.Scheduler.Delay(1000, SaveDebounced);
            return;
        }

        saveLifetime?.Dispose();
        saveLifetime = DefaultRecyclablePool.Instance.Rent();
        ConsoleApp.Current.Scheduler.DelayIfValid(1000, DelayState.Create(saveLifetime), async (s) =>
        {
            isSaving = true;
            await SaveAsync();
            isSaving = false;
            s.DisposeAllValidDependencies();
            s.Dispose();
            saveLifetime = null;
        });
    }
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
    public ListNoteSource Notes { get; set; } = new();
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

public class MRUWorkspaceSettings
{
    public string? LastOpenedWorkspace { get; set; }

    public static MRUWorkspaceSettings Load(string rootDirectory)
    {
        var path = Path.Combine(rootDirectory, "mru_workspace.json");
        
        if (!File.Exists(path))
        {
            return new MRUWorkspaceSettings();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MRUWorkspaceSettings>(json) ?? new MRUWorkspaceSettings();
    }

    public void Save(string rootDirectory)
    {
        var path = Path.Combine(rootDirectory, "mru_workspace.json");
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}