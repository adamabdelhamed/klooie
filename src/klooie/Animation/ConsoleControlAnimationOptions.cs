﻿namespace klooie;

public sealed class ConsoleControlAnimationOptions : CommonAnimationOptions
{
    public Func<RectF> Destination { get; set; }
    public void Setter(ConsoleControl target, in RectF bounds) => target.Bounds = bounds;
}

