namespace klooie.Gaming;
/// <summary>
/// A camera movement that will detect a high priority situation if the focal element
/// is out of bounds and will try to center the camera on that element immediately
/// </summary>
public class OutOfBoundsMovement : CameraMovement
{
    public override void Init() => FocalElement.Subscribe(nameof(FocalElement.Bounds), Check, EarliestOf(this, FocalElement));
    private void Check()
    {
        if (IsOutOfBounds == false || IsMoving) return;
        SituationDetected.Fire(0);
    }

    public override Task Move() => Task.FromResult(Camera.CameraLocation = FocalPoint.Offset(-Camera.Width / 2f, -Camera.Height / 2f));
}
