namespace ScrollSucker;

public class EnemyWaveDirective : SpawnDirective
{
    public string Display { get => observable.Get<string>(); set => observable.Set(value); }
    [FormSlider(Increment = 10, Min = 1, Max = 1000)]
    public float HP { get => observable.Get<float>(); set => observable.Set(value); }

    public float Period { get => observable.Get<float>(); set => observable.Set(value); } 
    public float StartDistance { get => observable.Get<float>(); set => observable.Set(value); } 
    public float EndDistance { get => observable.Get<float>(); set => observable.Set(value); }
    public float Speed { get => observable.Get<float>(); set => observable.Set(value); }

    public EnemyWaveDirective()
    {
        HP = 10;
        Period = 1000;
        StartDistance = 100;
        EndDistance = 20;
    }

    public override GameCollider Preview(World w)
    {
        return new EnemyDirective()
        {
            X = X,
            HP = HP,
            Display = Display + "(w)",
            Top = Top
        }.Preview(w);
    }

    public override void Render(World w) => Game.Current.Invoke(async () =>
    {
        Player? player = null;

        // make sure the player is in the world
        while(player == null)
        {
            await Game.Current.Delay(100);
            player = Game.Current.GamePanel.Children.WhereAs<Player>().SingleOrDefault();
        }

        // wait for the player to be within the starting distance
        while (player.Left < X - StartDistance)
        {
            await Game.Current.Delay(250);
        }

        while (w.ShouldContinue && player.Left < X - EndDistance)
        {
            new EnemyDirective()
            {
                X = X,
                HP = HP,
                Display = Display,
                Top = Top,
                StartWhenVisible = false,
                Speed = Speed,
            }.Render(w);
            await Game.Current.Delay(Period);
        }
    });

    public override void ValidateUserInput()
    {
        if (string.IsNullOrWhiteSpace(Display)) Display = "[Red]enemy";
        if (HP <= 0) HP = 10;
        if (StartDistance <= 0) StartDistance = 100;
        if (EndDistance <= 0) EndDistance = 20;
    }

    public override void RemoveFrom(LevelSpec spec) => spec.EnemyWaves.Remove(this);
    public override void AddTo(LevelSpec spec) => spec.EnemyWaves.Add(this);
}
