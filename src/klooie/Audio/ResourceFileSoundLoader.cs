using System.Reflection;

namespace klooie;

public static class ResourceFileSoundLoader
{
    public static Dictionary<string, Func<Stream>> LoadSounds<T>() where T : class
    {
        var ret = new Dictionary<string, Func<Stream>>(StringComparer.OrdinalIgnoreCase);
        var flags = BindingFlags.Public | BindingFlags.Static;
        var soundType = typeof(UnmanagedMemoryStream);

        // What you would do here is create a resource file with a bunch of MP3 files in it stored as UnmanagedMemoryStream properties.
        // This way, the MP3 files are embedded within your assembly as opposed to loose files on disk. 
        var sounds = typeof(T).GetProperties(flags).Where(p => p.PropertyType == soundType).ToArray();


        foreach (var sound in sounds)
        {
            var key = sound.Name;
            ret.Add(key, () => (UnmanagedMemoryStream)sound.GetValue(null));
        }
        return ret;
    }
}
