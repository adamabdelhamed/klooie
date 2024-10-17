using ScrollSucker;

namespace ScrollSucker;
public class MissileHeadElement : WeaponElement
{
    public MissileHeadElement(Weapon w) : base(w)
    {
        AddTag(nameof(Explosive));
        CompositionMode = CompositionMode.BlendBackground;
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        var angle = Velocity.Angle;
        var c = angle.Arrow;
        context.Fill(new ConsoleCharacter(c, RGB.Red));
    }
}


public class MissileLauncher : Weapon
{
    public const string MeteorBoomEventId = "MeteorBoom";
    public float Spread { get; set; } = 4;
    public override WeaponStyle Style => WeaponStyle.Explosive;

    public bool RoundTo90 { get; set; }

    private TimeSpan lastFireTime = TimeSpan.Zero;
    private int sequenceCount;

    public int MissilesPerFire { get; set; } = 2;

    public override void FireInternal(bool alt)
    {
        var delta = Game.Current.MainColliderGroup.Now - lastFireTime;

        if (delta < TimeSpan.FromSeconds(.75))
        {
            sequenceCount++;
        }
        else
        {
            sequenceCount = 1;
            if (alt)
            {
                sequenceCount++;
            }
        }
        lastFireTime = Game.Current.MainColliderGroup.Now;

        var clearLeft = IsClear(true);
        var clearRight = IsClear(false);

        Func<RectF> myLoc = () => Holder.Bounds;
        var myAngle = Holder.Velocity.Angle;
        var targetAngle = Holder.TargetAngle;
        var myTarget = Holder.Target;
        var iDelay = 200;
        if (sequenceCount == 1)
        {
            if (clearLeft)
            {
                for (var i = 0; i < MissilesPerFire; i++)
                {
                    var myI = i;
                    Game.Current.Invoke(async () =>
                    {
                        await Game.Current.DelayOrYield(iDelay * myI);
                        SlingLeft(MissilesPerFire - myI, myLoc, myAngle, myTarget);
                    });
                }
            }
            else if (clearRight)
            {
                for (var i = 0; i < MissilesPerFire; i++)
                {
                    var myI = i;
                    Game.Current.Invoke(async () =>
                    {
                        await Game.Current.DelayOrYield(iDelay * myI);
                        SlingRight(MissilesPerFire - myI, myLoc, myAngle, myTarget);
                    });
                }
            }
            else
            {
                for (var i = 0; i < MissilesPerFire; i++)
                {
                    var myI = i;
                    Game.Current.Invoke(async () =>
                    {
                        await Game.Current.DelayOrYield(iDelay * myI);
                        SlingStraight(MissilesPerFire - myI, myLoc, myAngle, myTarget);
                    });
                }
            }
        }
        else if (sequenceCount == 2)
        {
            if (clearRight)
            {
                for (var i = 0; i < MissilesPerFire; i++)
                {
                    var myI = i;
                    var effectiveDelay = Math.Max(50, iDelay * myI);
                    Game.Current.Invoke(async () =>
                    {
                        await Game.Current.Delay(effectiveDelay);
                        SlingRight(MissilesPerFire - myI, myLoc, myAngle, myTarget);
                    });
                }
            }
            else if (clearLeft)
            {
                for (var i = 0; i < MissilesPerFire; i++)
                {
                    var myI = i;
                    var effectiveDelay = Math.Max(50, iDelay * myI);
                    Game.Current.Invoke(async () =>
                    {
                        await Game.Current.Delay(effectiveDelay);
                        SlingLeft(MissilesPerFire - myI, myLoc, myAngle, myTarget);
                    });
                }
            }
            else
            {
                for (var i = 0; i < MissilesPerFire; i++)
                {
                    var myI = i;
                    Game.Current.Invoke(async () =>
                    {
                        await Game.Current.DelayOrYield(iDelay * myI);
                        SlingStraight(MissilesPerFire - myI, myLoc, myAngle, myTarget);
                    });
                }
            }
            sequenceCount = 0;
        }

    }


    public MissileHeadElement CreateMissleHead(float x, float y, int z)
    {
        var head = Game.Current.GamePanel.Add(new MissileHeadElement(this));

        OnWeaponElementEmitted.Fire(head);
        head.AddTag(WeaponTag);
        Game.Current.Invoke(async () =>
        {
            await Game.Current.Delay(3500);
            head.TryDispose();
        });

        head.MoveTo(x, y, z);

        return head;
    }

    private void LaunchAtTarget(MissileHeadElement head, Angle angle)
    {
        Game.Current.Sound.Play("missilelaunch");
        head.Velocity.Angle = angle;
        head.Velocity.Speed = 50 + Holder.Velocity.Speed;

        Game.Current.Invoke(async () =>
        {
            await Game.Current.Delay(50);
            head.Velocity.Speed += 50;
            await Game.Current.Delay(50);
            head.Velocity.Speed += 25;
            await Game.Current.Delay(50);
            head.Velocity.Speed += 25;
            await Game.Current.Delay(50);
            head.Velocity.Speed += 25;
        });


        head.Velocity.OnCollision.Subscribe((collision) =>
        {
            HP.Current.ReportCollision(collision);
            head.Dispose();
            var ex = Game.Current.GamePanel.Add(new Explosive(this));
            ex.MoveTo(head.Left, head.Top, head.ZIndex);
            ex.Explode();

        }, head);



    }

    private void EnableSeeking(MissileHeadElement head)
    {
        Game.Current.InvokeNextCycle(async () =>
        {
            while (head.IsExpired == false)
            {
                var toSeek = Game.Current.GamePanel.Controls
                    .WhereAs<GameCollider>()
                    .Where(e => Holder?.Target == null || e == Holder.Target)
                    .OrderBy(e => head.Velocity.Angle.DiffShortest(head.CalculateAngleTo(e)))
                    .ThenBy(e => head.CalculateDistanceTo(e))
                    .FirstOrDefault();


                if (toSeek != null)
                {
                    if (head.CalculateDistanceTo(toSeek) < Math.Pow(head.CalculateAngleTo(toSeek).DiffShortest(head.Velocity.Angle), .33f))
                    {
                        head.Velocity.OnCollision.Fire(new Collision()
                        {
                            Angle = head.CalculateAngleTo(toSeek),
                            MovingObject = head,
                            ColliderHit = toSeek,
                        });
                        return;
                    }
                }

                await Task.Yield();
            }
        });
    }

    private bool IsClear(bool left)
    {
        var rotation = left ? 90 : -90;
        var startAngle = Holder.TargetAngle.Add(180);
        var toAngle = startAngle.Add(rotation);
        toAngle = toAngle != 0 ? toAngle : 360;

        var slingSpot = Holder.RadialOffset(toAngle, Spread);
        var targetAngle = Holder.Target != null ?
            slingSpot.CalculateAngleTo(Holder.Target.Bounds) : 4;

        IEnumerable<GameCollider> obstacles = Holder.GetObstacles();

        for (var d = 0; d < Spread; d++)
        {
            var whatIfSpot = slingSpot.RadialOffset(targetAngle, d);
            obstacles = obstacles
                .Where(e => e.CalculateDistanceTo(whatIfSpot) < Spread / 2)
                .Where(e => e is MissileHeadElement == false);

            if (obstacles.Any())
            {
                return false;
            }
        }
        return true;
    }

    private void SlingLeft(int i, Func<RectF> mcLoc, Angle mcAngle, GameCollider target) => Sling(i, 90, mcLoc, mcAngle, false, target);
    private void SlingRight(int i, Func<RectF> mcLoc, Angle mcAngle, GameCollider target) => Sling(i, -90, mcLoc, mcAngle, false, target);
    private void SlingStraight(int i, Func<RectF> mcLoc, Angle mcAngle, GameCollider target) => Sling(i, -90, mcLoc, this.Holder.TargetAngle, true, target);

    private void Sling(int offset, float rotation, Func<RectF> mcLoc, Angle mcAngle, bool straight, GameCollider target)
    {
        Game.Current.Sound.Play("thump");
        if (RoundTo90)
        {
            mcAngle = mcAngle.RoundAngleToNearest(90);
        }
        var startAngle = mcAngle.Add(180);
        var toAngle = startAngle.Add(rotation);
        toAngle = toAngle != 0 ? toAngle : 360;

        var initialCenter = mcLoc().Center.RadialOffset(startAngle, Spread);
        var head = CreateMissleHead(initialCenter.Left, initialCenter.Top, Holder.ZIndex);

        LocF latestCenter = default;
        var to = Spread + (4 * offset);
        if (straight == false)
        {
            var swingAnimation = Animator.AnimateAsync(new FloatAnimationOptions()
            {
                From = 0,
                To = to,
                EasingFunction = EasingFunctions.Linear,
                Duration = 50,
                Setter = dNow =>
                {
                    latestCenter = mcLoc().Center.RadialOffset(toAngle, dNow);


                    head.MoveTo(latestCenter.Left, latestCenter.Top);


                    if (dNow == to)
                    {
                        var targetAngle2 = target != null ? latestCenter.CalculateAngleTo(target.Center()) : mcAngle;


                        if (Holder.Target == null && rotation == 90)
                        {
                            targetAngle2 = targetAngle2.Add(3);
                        }
                        else if (Holder.Target == null && rotation == -90)
                        {
                            targetAngle2 = targetAngle2.Add(-3);
                        }

                        if (offset == 0)
                        {
                            LaunchAtTarget(head, targetAngle2);
                        }
                        else
                        {
                            var delay = 50 * ((Spread - 3) + offset - 2);
                            if (delay <= 0)
                            {
                                LaunchAtTarget(head, targetAngle2);
                            }
                            else
                            {
                                Game.Current.Invoke(async () =>
                                {
                                    await Game.Current.Delay(delay);
                                    LaunchAtTarget(head, targetAngle2);
                                });
                            }
                        }
                    }
                }
            });
        }
        else
        {
            var headSpot = mcLoc().RadialOffset(mcAngle, 1, false);
            head.MoveTo(headSpot.Left, headSpot.Top);
            LaunchAtTarget(head, mcAngle);
        }

        var lastKnownPosition = head.Bounds;
        head.Subscribe(nameof(head.Bounds), () =>
        {
            var traveled = head.Bounds.CalculateNormalizedDistanceTo(lastKnownPosition);
            if (traveled >= 3)
            {
                var smoke = Game.Current.GamePanel.Add(new SmokeElement(TimeSpan.FromSeconds(.25)));
                smoke.MoveTo(head.Left, head.Top);
                smoke.ResizeTo(1, 1);
                lastKnownPosition = head.Bounds;
            }
        }, head);
    }
}



