namespace klooie;

public class ADSREnvelope
{
    public double Attack;   // seconds
    public double Decay;    // seconds
    public double Sustain;  // 0.0–1.0
    public double Release;  // seconds

    private double noteOnTime;
    private double? noteOffTime;
    private double sampleRate;
    private bool isReleased;

    public void Trigger(double currentTime, double sampleRate)
    {
        this.noteOnTime = currentTime;
        this.sampleRate = sampleRate;
        this.noteOffTime = null;
        this.isReleased = false;
    }

    public void ReleaseNote(double currentTime)
    {
        if (!noteOffTime.HasValue)
        {
            noteOffTime = currentTime;
            isReleased = true;
        }
    }

    public float GetLevel(double currentTime)
    {
        double tSinceNoteOn = currentTime - noteOnTime;

        if (!isReleased)
        {
            if (tSinceNoteOn < Attack) return (float)(tSinceNoteOn / Attack);
            if (tSinceNoteOn < Attack + Decay)
            {
                double decayTime = tSinceNoteOn - Attack;
                return (float)(1.0 - (1.0 - Sustain) * (decayTime / Decay));
            }
            return (float)Sustain;
        }
        else
        {
            double tSinceNoteOff = currentTime - noteOffTime.Value;
            double tAtRelease = noteOffTime.Value - noteOnTime;

            float startLevel;

            if (tAtRelease < Attack)
            {
                startLevel = (float)(tAtRelease / Attack);
            }
            else if (tAtRelease < Attack + Decay)
            {
                double decayTime = tAtRelease - Attack;
                startLevel = (float)(1.0 - (1.0 - Sustain) * (decayTime / Decay));
            }
            else
            {
                startLevel = (float)Sustain;
            }

            float releaseLevel = (float)(startLevel * (1.0 - (tSinceNoteOff / Release)));
            return Math.Max(0f, releaseLevel);
        }
    }


    public bool IsDone(double currentTime)
    {
        if (!isReleased) return false;
        return currentTime - noteOffTime.Value >= Release;
    }
}
