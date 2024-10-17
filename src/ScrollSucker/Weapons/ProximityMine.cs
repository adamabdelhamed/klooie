namespace ScrollSucker;
public enum ProximityMineState
{
    NoNearbyThreats,
    ThreatApproaching,
    ThreatNearby
}

public class ProximityMineWatcher
{
    internal static ProximityMineWatcher current;
    public ProximityMineWatcher()
    {
        Game.Current.Invoke(ExecuteAsync);
    }

    private async Task ExecuteAsync()
    {
        List<GameCollider> colliderBuffer = new List<GameCollider>();
        var lt = Game.Current.CreateChildLifetime();
        lt.OnDisposed(() => current = null);
        while (lt.ShouldContinue)
        {
            var mines = Game.Current.GamePanel.Controls.WhereAs<ProximityMine>().ToArray();

            var targets = Game.Current.MainColliderGroup
                .EnumerateCollidersSlow(colliderBuffer)
                .WhereAs<Character>()
                .Where(e => e is Player == false).ToArray();
            foreach (var mine in mines)
            {

                if (mine.IsExpired) continue;
                var obs = mine.GetObstacles();
                var closest = targets.Where(t => t.ShouldContinue && mine.CalculateNormalizedDistanceTo(t) < mine.Range*2 && mine.HasLineOfSight(t, obs)).OrderBy(t => mine.CalculateNormalizedDistanceTo(t)).FirstOrDefault();

                if (closest == null)
                {
                    mine.State = ProximityMineState.NoNearbyThreats;
                    continue;
                }

                var d = closest.CalculateNormalizedDistanceTo(mine);

                if (d < mine.Range * .9f)
                {
                    Game.Current.Invoke(async () =>
                    {
                        await mine.AnimateAsync(new ConsoleControlAnimationOptions()
                        {
                            Destination = () => closest.Bounds,
                            Duration = 200,
                        });
                        mine.Explode();
                    });
                }
                else if (d < mine.Range * 3f)
                {
                    mine.State = ProximityMineState.ThreatNearby;
                }
                else if (d < mine.Range * 6f)
                {
                    mine.State = ProximityMineState.ThreatApproaching;
                }
                else
                {
                    mine.State = ProximityMineState.NoNearbyThreats;
                }
                await Game.Current.Delay(5);

            }
            await Game.Current.Delay(10);
        }
    }
}

public class ProximityMine : Explosive
{
    public string TargetTag { get; set; } = "enemy";
    public ProximityMineState State { get; set; } = ProximityMineState.NoNearbyThreats;

 
    public ProximityMine(Weapon w) : base(w)
    {
        ProximityMineWatcher.current = ProximityMineWatcher.current ?? new ProximityMineWatcher();
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        if (State == ProximityMineState.NoNearbyThreats)
        {
            context.FillRect(new ConsoleCharacter('#', RGB.DarkGray), 0, 0, Width, Height);
        }
        else if (State == ProximityMineState.ThreatApproaching)
        {
            context.FillRect(new ConsoleCharacter('#', RGB.Black, RGB.DarkYellow), 0, 0, Width, Height);
        }
        else if (State == ProximityMineState.ThreatNearby)
        {
            context.FillRect(new ConsoleCharacter('#', RGB.Black, RGB.Red), 0, 0, Width, Height);
        }
    }
}
