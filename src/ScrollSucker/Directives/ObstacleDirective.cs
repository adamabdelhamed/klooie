namespace ScrollSucker;

public class ObstacleDirective : SpawnDirective
{
    public float HP { get => observable.Get<float>(); set => observable.Set(value); }

    public override GameCollider Preview(World w)
    {
        var obstacle = Game.Current.GamePanel.Add(new Obstacle(HP));
        obstacle.SetHP(HP);
        w.Place(obstacle, X, Top);
        return obstacle;
    }
    public override void Render(World w)
    {
        Preview(w);
    }

    public override void ValidateUserInput()
    {
        if (HP <= 0) HP = 10;
    }

    public override void RemoveFrom(LevelSpec spec) => spec.Obstacles.Remove(this);
    public override void AddTo(LevelSpec spec) => spec.Obstacles.Add(this);
}

public class Obstacle : Character
{
    public float HP { get => Get<float>(); set => Set(value); }
    public Obstacle(float hP)
    {
        Foreground = RGB.White;
        Background = RGB.Orange;
        ResizeTo(10, 3);
        HP = hP;
        this.SetHP(HP, HP);
        ScrollSucker.HP.Current?.OnDamageEnforced.Subscribe(h =>
        {
            if (h.RawArgs.Damagee != this) return;
            HP += h.DamageAmount;
        }, this);
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        context.FillRect(Background, new Rect(0, 0, Width, Height));
        var stringVal = Math.Ceiling(HP) + "";
        context.DrawString(stringVal.ToConsoleString(Foreground, Background), ConsoleMath.Round((Width / 2f) - (stringVal.Length / 2f)), ConsoleMath.Round((Height / 2f) - .5f));
    }
}