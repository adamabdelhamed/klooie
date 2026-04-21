using System.Text.Json;

namespace klooie;

public sealed class ConsoleRecordingManifest
{
    public int Version { get; set; } = 1;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public long ChunkDurationTicks { get; set; }
    public List<ConsoleRecordingChunkInfo> Chunks { get; set; } = new List<ConsoleRecordingChunkInfo>();
}

public sealed class ConsoleRecordingChunkInfo
{
    public int ChunkIndex { get; set; }
    public string VideoPath { get; set; } = "";
    public string AudioPath { get; set; } = "";
    public long ChunkStartTicks { get; set; }
    public long DurationTicks { get; set; }
    public long FrameCount { get; set; }
    public long FirstFrameTicks { get; set; }
    public long LastFrameTicks { get; set; }
    public int FirstFrameWidth { get; set; }
    public int FirstFrameHeight { get; set; }
    public int LastFrameWidth { get; set; }
    public int LastFrameHeight { get; set; }
    public long FirstAudioSampleFrame { get; set; } = -1;
    public long AudioSampleCount { get; set; }
    public int AudioSampleRate { get; set; }
    public int AudioChannels { get; set; }
    public bool Finalized { get; set; }
}

public static class ConsoleRecordingManifestStore
{
    public const string ManifestExtension = ".krec";
    public const string ManifestFileName = "recording.krec";

    public static void Write(DirectoryInfo sessionDirectory, ConsoleRecordingManifest manifest)
    {
        if (sessionDirectory == null) throw new ArgumentNullException(nameof(sessionDirectory));
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));

        sessionDirectory.Create();
        var path = GetManifestFile(sessionDirectory).FullName;
        var temp = path + ".tmp";
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temp, json);
        if (File.Exists(path)) File.Delete(path);
        File.Move(temp, path);
    }

    public static FileInfo GetManifestFile(DirectoryInfo sessionDirectory)
    {
        if (sessionDirectory == null) throw new ArgumentNullException(nameof(sessionDirectory));
        return new FileInfo(Path.Combine(sessionDirectory.FullName, ManifestFileName));
    }

    public static bool IsManifestFile(FileInfo file)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        return string.Equals(file.Extension, ManifestExtension, StringComparison.OrdinalIgnoreCase);
    }

    public static ConsoleRecordingManifest Read(FileInfo manifestFile)
    {
        if (manifestFile == null) throw new ArgumentNullException(nameof(manifestFile));
        if (manifestFile.Exists == false) throw new FileNotFoundException("Recording manifest was not found", manifestFile.FullName);
        return JsonSerializer.Deserialize<ConsoleRecordingManifest>(File.ReadAllText(manifestFile.FullName)) ?? new ConsoleRecordingManifest();
    }

    public static ConsoleRecordingManifest RebuildFromChunks(DirectoryInfo sessionDirectory)
    {
        if (sessionDirectory == null) throw new ArgumentNullException(nameof(sessionDirectory));

        var manifest = new ConsoleRecordingManifest();
        var chunksDirectory = ConsoleRecordingSession.GetChunksDirectory(sessionDirectory);
        if (chunksDirectory.Exists == false) return manifest;

        foreach (var file in chunksDirectory.EnumerateFiles("chunk-*.cv").OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (ConsoleVideoChunkWriter.TryReadFinalizedChunkInfo(file, out var chunk))
            {
                chunk.VideoPath = Path.Combine("chunks", file.Name);
                ApplyRecoveredAudioInfo(sessionDirectory, chunk);
                manifest.Chunks.Add(chunk);
            }
        }

        if (manifest.Chunks.Count > 0)
        {
            var durations = manifest.Chunks.Select(c => c.DurationTicks).Where(t => t > 0).ToArray();
            manifest.ChunkDurationTicks = durations.Length == 0 ? 0 : durations.Max();
        }
        return manifest;
    }

    private static void ApplyRecoveredAudioInfo(DirectoryInfo sessionDirectory, ConsoleRecordingChunkInfo chunk)
    {
        var audioFile = new FileInfo(Path.Combine(ConsoleRecordingSession.GetAudioDirectory(sessionDirectory).FullName, $"chunk-{chunk.ChunkIndex:D6}.wav"));
        if (audioFile.Exists == false) return;
        if (TryReadWavInfo(audioFile, out var sampleRate, out var channels, out var sampleCount) == false) return;

        chunk.AudioPath = Path.Combine("audio", audioFile.Name);
        chunk.FirstAudioSampleFrame = -1;
        chunk.AudioSampleCount = sampleCount;
        chunk.AudioSampleRate = sampleRate;
        chunk.AudioChannels = channels;
    }

    private static bool TryReadWavInfo(FileInfo file, out int sampleRate, out int channels, out long sampleCount)
    {
        sampleRate = 0;
        channels = 0;
        sampleCount = 0;

        try
        {
            using var stream = File.OpenRead(file.FullName);
            using var reader = new BinaryReader(stream);
            if (ReadAscii(reader, 4) != "RIFF") return false;
            reader.ReadInt32();
            if (ReadAscii(reader, 4) != "WAVE") return false;

            var bitsPerSample = 0;
            var dataBytes = 0L;
            while (stream.Position + 8 <= stream.Length)
            {
                var chunkId = ReadAscii(reader, 4);
                var chunkSize = reader.ReadInt32();
                var chunkStart = stream.Position;
                if (chunkId == "fmt ")
                {
                    reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt16();
                    bitsPerSample = reader.ReadInt16();
                }
                else if (chunkId == "data")
                {
                    dataBytes = chunkSize;
                }

                stream.Position = chunkStart + chunkSize;
            }

            if (sampleRate <= 0 || channels <= 0 || bitsPerSample <= 0 || dataBytes <= 0) return false;
            sampleCount = dataBytes / (bitsPerSample / 8);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ReadAscii(BinaryReader reader, int count)
    {
        var chars = new char[count];
        for (var i = 0; i < count; i++) chars[i] = (char)reader.ReadByte();
        return new string(chars);
    }
}
