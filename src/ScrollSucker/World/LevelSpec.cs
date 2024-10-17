using Newtonsoft.Json;

namespace ScrollSucker;

public class LevelSpec
{
    [JsonIgnore]
    public string Path { get; set; }
    public int SceneWidth { get; set; }
    public float PlayerSpeed { get; set; }
    public float PlayerHP { get; set; }
    public List<string> CutScenes { get; set; } = new List<string>();
    public List<AmmoDirective> Ammo { get; set; } = new List<AmmoDirective>();
    public List<EnemyDirective> Enemies { get; set; } = new List<EnemyDirective>();
    public List<EnemyWaveDirective> EnemyWaves { get; set; } = new List<EnemyWaveDirective>();
    public List<WeaponDirective> Weapons { get; set; } = new List<WeaponDirective>();
    public List<ObstacleDirective> Obstacles { get; set; } = new List<ObstacleDirective>();

    public IEnumerable<SpawnDirective> Directives()
    {
        foreach (var ammo in Ammo) yield return ammo;
        foreach (var enemy in Enemies) yield return enemy;
        foreach (var weapon in Weapons) yield return weapon;
        foreach (var obstacle in Obstacles) yield return obstacle;
        foreach (var enemyWave in EnemyWaves) yield return enemyWave;
    }

    public LevelSpec() 
    {
        PlayerSpeed = 25;
        PlayerHP = 25;
    }
}