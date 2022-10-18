namespace klooie;
public class KeyframedEase
{
    private float[] keyFrames;
    public KeyframedEase(float[] keyFrames)
    {
        this.keyFrames = keyFrames;
    }

    public float Ease(float percentage)
    {
        var rawIndex = (keyFrames.Length - 1) * percentage;
        if (rawIndex == (int)rawIndex)
        {
            return keyFrames[(int)rawIndex];
        }
        else
        {
            var splitLocation = rawIndex - (int)rawIndex;
            var previousKeyFrame = keyFrames[(int)rawIndex];
            var nextKeyFrame = keyFrames[(int)rawIndex + 1];
            var nextFrameDelta = nextKeyFrame - previousKeyFrame;
            var interpolationAmount = splitLocation * nextFrameDelta;
            return previousKeyFrame + interpolationAmount;
        }
    }
}