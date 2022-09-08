namespace klooie.Gaming;
public class CameraOperationShortCircuitException : Exception { }
public class CameraOperator : Lifetime
{
    private int currentMovementPriority = int.MaxValue;
    private ILifetime currentMovementLifetime;


    public CameraOperator(Camera camera, ConsoleControl focalElement, Velocity focalVelocity, IDelayProvider delayProvider, params CameraMovement[] movements)
    {
        camera.CameraLocation = new LocF();
        camera.CameraLocation = focalElement.Center().Offset(-camera.Width/2, -camera.Height/2);
       

        foreach (CameraMovement m in movements)
        {
            m.DelayProvider = delayProvider;
            m.SituationDetected.Subscribe(async (p) => await OnSituationDetected(m, p), this);
            OnDisposed(m.Dispose);
            m.Camera = camera;
            m.FocalElement = focalElement;
            m.FocalVelocity = focalVelocity;
            m.Init();
        }
    }

    private async Task OnSituationDetected(CameraMovement interruptingMovement, int newSituationPriority)
    {
        if (newSituationPriority < currentMovementPriority)
        {
            currentMovementLifetime?.TryDispose();
            currentMovementPriority = newSituationPriority;
            currentMovementLifetime = this.CreateChildLifetime();
            interruptingMovement.MovementLifetime = currentMovementLifetime;
            try
            {
                await interruptingMovement.Move();
            }
            catch (CameraOperationShortCircuitException)
            {

            }
            finally
            {
                currentMovementLifetime?.TryDispose();
                currentMovementLifetime = null;
                currentMovementPriority = int.MaxValue;
                interruptingMovement.MovementLifetime = null;
            }
        }
    }
}