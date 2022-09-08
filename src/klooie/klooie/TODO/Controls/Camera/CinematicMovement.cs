namespace klooie.Gaming;
public class CinematicMovement : CameraMovement
{
    private const float CollisionETAThreshold = 500f;
    private const float MovementDuration = 600f;

    public override void Init() => FocalElement.Subscribe(nameof(FocalElement.Bounds), Check, EarliestOf(this, FocalElement));
    
    private bool IsAboutToGoOutOfBounds()
    {
        var camBounds = Camera.CameraBounds;
        var obstacles = new List<ICollider>();
        obstacles.Add(new RectF(camBounds.Left, camBounds.Top - 1, Camera.Width, 1).Box()); // top boundary
        obstacles.Add(new RectF(camBounds.Left, camBounds.Top + Camera.Height, Camera.Width, 1).Box()); // bottom boundary
        obstacles.Add(new RectF(camBounds.Left - 1, camBounds.Top, 1, Camera.Height).Box()); // left boundary
        obstacles.Add(new RectF(camBounds.Left + Camera.Width, camBounds.Top, 1, Camera.Height).Box()); // right boundary

        var prediction = HitDetection.PredictHit(new HitDetectionOptions(FocalVelocity.Collider, obstacles)
        {
            Visibility = Camera.Bounds.Hypotenous,
            Angle = FocalVelocity.Angle,
            Mode = CastingMode.SingleRay,
        });
        var etaTs = CalculatePredictionETA(prediction, FocalVelocity);
        return prediction.Type != HitType.None && etaTs < TimeSpan.FromMilliseconds(CollisionETAThreshold);
    }
 
    private void Check()
    {
        if (IsAboutToGoOutOfBounds() && IsMoving == false)
        {
            SituationDetected.Fire(1);
        }
    }

    private TimeSpan CalculatePredictionETA(HitPrediction collision, Velocity v)
    {
        if (collision == null || v.Speed == 0 || collision.ColliderHit == null) return TimeSpan.MaxValue;
        var d = collision.LKGD;
        var seconds = d / v.Speed;
        return TimeSpan.FromSeconds(seconds);
    }

    private RectF DesiredCameraBounds => EnsureWithinBigBounds(FocalElement.Center().OffsetByAngleAndDistance(FocalVelocity.Angle, Camera.Bounds.Hypotenous *.25f).ToRect(Camera.Width, Camera.Height));

    public override async Task Move()
    {
        await Camera.AnimateTo(DesiredCameraBounds.TopLeft, MovementDuration, new CustomEase(new float[]
        {
            .01f, .02f,.04f, .07f, .11f, .16f, .22f,
            .28f, .38f, .50f, .60f, .70f, .80f,
            .86f, .92f, .95f, .96f, .97f, .98f, .985f, .99f, .995f, 1f,
        }).Ease, this, this.DelayProvider);
        await AutoFollow();
    }

    private async Task AutoFollow()
    {
        var v = FocalVelocity;
        if (v.Speed == 0) return;
        var lt = Lifetime.EarliestOf(ConsoleApp.Current, FocalVelocity.OnSpeedChanged.CreateNextFireLifetime());

        var camXDelta = FocalElement.Left - Camera.CameraBounds.Left;
        var camYDelta = FocalElement.Top - Camera.CameraBounds.Top;
        while (lt != null && lt.IsExpired == false)
        {
            Camera.CameraLocation = EnsureWithinBigBounds(new RectF(FocalElement.Left - camXDelta, FocalElement.Top - camYDelta, Camera.Width, Camera.Height)).TopLeft;
            await DelayProvider.DelayOrYield(0);
        }
    }
}
