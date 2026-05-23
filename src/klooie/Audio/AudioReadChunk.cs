using System;
using System.Collections.Generic;
using System.Text;

namespace klooie;

public readonly struct AudioReadChunk
{
    public readonly string TrackId;
    public readonly float[] Buffer;
    public readonly int Offset;
    public readonly int Count;              // float samples (interleaved)
    public readonly int SampleRate;
    public readonly int Channels;
    public readonly long FramePosition;     // in sample-frames (not float samples)
    public readonly long FramesInChunk;     // Count / Channels

    public AudioReadChunk(string trackId, float[] buffer, int offset, int count, int sampleRate, int channels, long framePosition)
    {
        TrackId = trackId;
        Buffer = buffer;
        Offset = offset;
        Count = count;
        SampleRate = sampleRate;
        Channels = channels;
        FramePosition = framePosition;
        FramesInChunk = channels <= 0 ? 0 : (count / channels);
    }
}