namespace ScrollSucker;

public sealed class CustomCameraMovement : CameraMovement
{
    public override void Init() => FocalElement.Subscribe(nameof(FocalElement.Bounds), Check, EarliestOf(this, FocalElement));
    private void Check() => SituationDetected.Fire(0);
    public override async Task Move() => Camera.PointAt(GetPointerForPlayer(FocalElement as Player));

    public static LocF GetPointerForPlayer(Player player) => player.TopLeft().Offset(50, 0).GetRounded();
}