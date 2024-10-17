using System.Reflection;

namespace ScrollSucker;

public class Runtime : World
{ 
    private Player player;
    private CameraOperator cameraOperator;
    public Event Lost { get; private init; } = new Event();
    public Event Won { get; private init; } = new Event();
    public Runtime(LevelSpec spec) : base(spec) { }

    protected override async Task Startup()
    {
        await base.Startup();
        AddPlayer();
        PopulateWorld();
        Invoke(SetupCutScenes);
    }

    private async Task SetupCutScenes()
    {
        var knownCutScenes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsSubclassOf(typeof(CutScene)) && t.IsAbstract == false)
            .ToDictionary(t => t.Name, t => t);
        var cutScenesForThisLevel = new Queue<CutScene>(spec.CutScenes.Select(typeName => Activator.CreateInstance(knownCutScenes[typeName]) as CutScene).OrderBy(cs => cs.StartLocation));

        if (cutScenesForThisLevel.None()) return;

        var nextCutSceneLifetime = player.CreateChildLifetime();
        var cutSceneInProgress = false;
        while (nextCutSceneLifetime != null && nextCutSceneLifetime.ShouldContinue)
        {
            player.Subscribe(nameof(player.Bounds), async () =>
            {
                if (cutSceneInProgress) return;

                if(player.Left >= cutScenesForThisLevel.Peek().StartLocation)
                {
                    cutSceneInProgress = true;
                    player.InputEnabled = false;
                    player.Velocity.Stop();
                    this.cameraOperator?.Dispose();
                    var toExecute = cutScenesForThisLevel.Dequeue();
                    await toExecute.Execute();
                    var toDispose = nextCutSceneLifetime;

                    try
                    {
                        nextCutSceneLifetime = null;
                        if (player.ShouldContinue == false) return;

                        SetPlayModeCameraOperator();
                        player.InputEnabled = true;
                        player.Resume();
                        if (cutScenesForThisLevel.None()) return;

                        nextCutSceneLifetime = player.CreateChildLifetime();
                    }
                    finally
                    {
                        cutSceneInProgress = false;
                        toDispose.Dispose();
                    }
                }
            }, nextCutSceneLifetime);

            await nextCutSceneLifetime.AsTask();
        }
    }

    private void AddPlayer()
    {
        player = camera.Add(new Player(spec.PlayerSpeed, spec.PlayerHP));
        player.Velocity.OnCollision.Subscribe(CheckForDefeat, this);
        player.Velocity.OnVelocityEnforced.Subscribe(CheckForVictory, player);
        player.MoveTo(camera.BigBounds.Left + 2, camera.BigBounds.Top + 2);
        SetPlayModeCameraOperator();
    }

    private void SetPlayModeCameraOperator() => SetCameraOperator(new CameraOperator(camera, player, player.Velocity, this, new CameraMovement[] { new CustomCameraMovement() }));

    private void SetCameraOperator(CameraOperator cameraOperator)
    {
        this.cameraOperator?.Dispose();
        this.cameraOperator = cameraOperator;
    }

    private void PopulateWorld()
    {
        spec.Directives().ForEach(d => d.Render(this));

        GamePanel.Children
            .WhereAs<Obstacle>()
            .Where(o =>  IsTop(o) == IsTop(player))
            .ForEach(o => o.AddTag(nameof(Enemy)));
    }
  
    private void CheckForVictory()
    {
        if (camera.BigBounds.OverlapPercentage(player.Bounds) == 1) return;

        player.Dispose();
        Won.Fire();
    }

    private void CheckForDefeat(Collision c)
    {
        if (c.ColliderHit is Enemy == false && c.ColliderHit is Obstacle == false) return;

        player.Dispose();
        Lost.Fire();
    }
}
