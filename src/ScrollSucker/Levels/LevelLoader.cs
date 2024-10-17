using Newtonsoft.Json;
using System.Reflection;

namespace ScrollSucker;

public static class LevelLoader
{
    public static string LevelsDir 
    {
        get
        {
            var levelsDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Levels");


            var binDebugIndex = levelsDir.IndexOf(@"bin\debug", StringComparison.OrdinalIgnoreCase);

            if (binDebugIndex >= 0)
            {
                levelsDir = levelsDir.Substring(0, binDebugIndex) + "Levels";
            }

            var binReleaseIndex = levelsDir.IndexOf(@"bin\release", StringComparison.OrdinalIgnoreCase);

            if (binReleaseIndex >= 0)
            {
                levelsDir = levelsDir.Substring(0, binReleaseIndex) + "Levels";
            }

            return levelsDir;
        }
    }

    public static LevelSpec[] LoadLevels()
    {
        var levelsList = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(Path.Combine(LevelsDir, "Levels.json")));
        var levels = levelsList
          .Select(l =>
          {
              var fullPath = Path.Combine(LevelsDir, l);
              var ret = JsonConvert.DeserializeObject<LevelSpec>(File.ReadAllText(fullPath));
              ret.Path = fullPath;
              return ret;
          })
          .ToArray();
        return levels;
    }
}
