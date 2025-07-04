﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie.Gaming;

public interface IMovement
{
    Vision Vision { get; }
    GameCollider Eye { get; }
    Velocity Velocity { get; }
    Func<Movement, RectF?> CuriosityPoint { get; set; }
    public float BaseSpeed { get; set; }
}

public abstract class Movement : DelayState, IMovement
{
    public float BaseSpeed { get; set; }
    public Vision Vision { get; private set; }
    public Velocity Velocity => Eye.Velocity;
    public GameCollider Eye => Vision?.Eye;
    public Func<Movement, RectF?> CuriosityPoint { get; set; }
    protected Movement() { }

    protected virtual void Construct(Vision vision, Func<Movement, RectF?> curiosityPoint, float baseSpeed)
    {
        AddDependency(this);
        AddDependency(vision);
        AddDependency(vision.Eye);
        AddDependency(vision.Eye.Velocity);
        Vision = vision;
        CuriosityPoint = curiosityPoint;
        BaseSpeed = baseSpeed;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Vision = null;
        CuriosityPoint = null;
        BaseSpeed = 0;
    }
}