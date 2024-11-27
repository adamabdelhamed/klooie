namespace klooie.Gaming;
/// <summary>
/// A camera movement that always keeps the focal element in the center
/// </summary>
public sealed class AlwaysCenteredMovement : CameraMovement
{
    public override void Init() => FocalElement.BoundsChanged.Subscribe( Check, EarliestOf(this, FocalElement));
    private void Check() => SituationDetected.Fire(0);
    public override async Task Move() => Camera.PointAt(FocalElement.Center());
}
