﻿namespace klooie.Gaming;

/// <summary>
/// An exception that is thrown when a camera movement needs to be short circuited
/// </summary>
public sealed class CameraOperationShortCircuitException : Exception { }

/// <summary>
/// A utility for operating a camera
/// </summary>
public sealed class CameraOperator : Recyclable
{
    private int currentPri = int.MaxValue;
    private Recyclable moveLt;

    /// <summary>
    /// Creates a camera operator
    /// </summary>
    /// <param name="camera">the camera to operate</param>
    /// <param name="focalElement">the control to focus on</param>
    /// <param name="focalVelocity">the velocity of the control to focus on</param>
    /// <param name="delayProvider">the delay provider to use for animated movements</param>
    /// <param name="movements">the movements this operator knows how to perform</param>
    public CameraOperator(Camera camera, ConsoleControl focalElement, Velocity focalVelocity, params CameraMovement[] movements)
    {
        if (focalElement == null || focalVelocity == null) throw new ArgumentNullException("focalElement and focalVelocity cannot be null");

        foreach (CameraMovement m in movements)
        {
            m.SituationDetected.Subscribe(async (p) => await OnSituationDetected(m, p), this);
            OnDisposed(m, TryDisposeMe);
            m.Camera = camera;
            m.FocalElement = focalElement;
            m.FocalVelocity = focalVelocity;
            m.Init();
        }
    }

    private async Task OnSituationDetected(CameraMovement detector, int newPri)
    {
        if (newPri < currentPri)
        {
            moveLt?.TryDispose();
            currentPri = newPri;
            moveLt = this.CreateChildRecyclable();
            detector.MovementLifetime = moveLt;
            try
            {
                await detector.Move();
            }
            catch (CameraOperationShortCircuitException) { }
            finally
            {
                moveLt?.TryDispose();
                moveLt = null;
                currentPri = int.MaxValue;
                detector.MovementLifetime = null;
            }
        }
    }
}