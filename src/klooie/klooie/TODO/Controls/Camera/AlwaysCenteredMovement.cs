namespace klooie.Gaming;
public class AlwaysCenteredMovement : CameraMovement
{
    public override void Init() => FocalElement.Subscribe(nameof(FocalElement.Bounds), Check, EarliestOf(this, FocalElement));
    private void Check()
    {
        SituationDetected.Fire(0);
    }

    public override Task Move() => Task.FromResult(Camera.CameraLocation = FocalPoint.Offset(-Camera.Width / 2f, -Camera.Height / 2f));
}
