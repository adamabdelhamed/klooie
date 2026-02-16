using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace klooie;

public sealed class FrameBridge
{
    public readonly CompositionOwnerCapture OwnerCapture;

    private readonly ConcurrentQueue<SceneSnapshot> queue = new ConcurrentQueue<SceneSnapshot>();

    public FrameBridge(Func<ConsoleControl, int> idProvider)
    {
        OwnerCapture = new CompositionOwnerCapture { IdProvider = idProvider };
    }

    public void Enqueue(SceneSnapshot frame) => queue.Enqueue(frame);
    public bool TryDequeue(out SceneSnapshot frame) => queue.TryDequeue(out frame);
}

public sealed class ControlSnapshot
{
    public required int Id { get; init; }  
    public required float X { get; init; }
    public required float Y { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required CompositionMode CompositionMode { get; init; }
    public required ConsoleCharacter[] Pixels { get; init; } // row-major Width*Height
}

public sealed class SceneSnapshot
{
    public required int ViewWidth { get; init; }
    public required int ViewHeight { get; init; }

    // Final composed pixels (row-major ViewWidth*ViewHeight)
    public required ConsoleCharacter[] Pixels { get; init; }

    // Top-most owner per cell (row-major ViewWidth*ViewHeight)
    public required int[] OwnerIds { get; init; }

    // Per-control float info for wobble/sub-cell offsets (keep it tiny for now)
    public required ControlState[] Controls { get; init; }
}

public readonly struct ControlState
{
    public readonly int Id;
    public readonly float Left;
    public readonly float Top;

    public ControlState(int id, float left, float top)
    {
        Id = id;
        Left = left;
        Top = top;
    }
}

public sealed class FrameSnapshot
{
    public required int Width { get; init; }
    public required int Height { get; init; }

    // Flattened row-major pixels: index = y * Width + x
    public required ConsoleCharacter[] Pixels { get; init; }
}