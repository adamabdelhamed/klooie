namespace klooie;

public interface IControllerProvider
{
    Controller Controller { get; }
    bool IsConnected { get; }

    void Update();
    void RumblePulse(float left, float right, ILifetime duration);
    void RumblePulse(float left, float right, float durationms);
    void ForceStopRumble();
}

