using System.Reflection;

namespace klooie;

public static class ResourceFileSoundLoader
{
    public static Dictionary<string, byte[]> LoadSounds<T>() where T : class
    {
        var ret = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var flags = BindingFlags.Public | BindingFlags.Static;
        var soundType = typeof(UnmanagedMemoryStream);

        // What you would do here is create a resource file with a bunch of MP3 files in it stored as UnmanagedMemoryStream properties.
        // This way, the MP3 files are embedded within your assembly as opposed to loose files on disk. 
        var sounds = typeof(T).GetProperties(flags).Where(p => p.PropertyType == soundType).ToArray();

        var buffer = new byte[1024 * 1024];
        foreach (var sound in sounds)
        {
            var key = sound.Name;
            var bytes = new List<byte>();
            using (var stream = (UnmanagedMemoryStream)sound.GetValue(null))
            {
                var lastRead = -1;
                while (lastRead != 0)
                {
                    lastRead = stream.Read(buffer, 0, buffer.Length);
                    bytes.AddRange(lastRead == buffer.Length ? buffer : buffer.Take(lastRead));
                }
            }

            var wavBytes = bytes.ToArray();
            ret.Add(key, wavBytes);
        }
        return ret;
    }
}
