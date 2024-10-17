﻿namespace ScrollSucker;

public class ProximityMineDropper : Weapon
{
    public override WeaponStyle Style => WeaponStyle.Explosive;
    public string TargetTag { get; set; } = "enemy";

    public override void FireInternal(bool alt)
    {
        var mine = new ProximityMine(this) { TargetTag = TargetTag };
        PlaceMineSafe(mine, Holder, !alt);
        Game.Current.GamePanel.Add(mine);
        OnWeaponElementEmitted.Fire(mine);
    }

    public static void PlaceMineSafe(GameCollider mine, Character holder, bool throwElement, float speed = 50f)
    {
        if (throwElement == false)
        {
            var buffer = 2f;
            if (holder.Velocity.Angle.Value >= 315 || holder.Velocity.Angle.Value < 45)
            {
                mine.MoveTo(holder.Left - buffer * mine.Width, holder.Top, holder.ZIndex);
            }
            else if (holder.Velocity.Angle.Value < 135)
            {
                mine.MoveTo(holder.Left, holder.Top - buffer * mine.Height, holder.ZIndex);
            }
            else if (holder.Velocity.Angle.Value < 225)
            {
                mine.MoveTo(holder.Left + buffer * mine.Width, holder.Top, holder.ZIndex);
            }
            else
            {
                mine.MoveTo(holder.Left, holder.Top + buffer * mine.Height, holder.ZIndex);
            }
        }
        else
        {
            var initialPlacement = holder.TopLeft().RadialOffset(holder.Velocity.Angle, 1f);
            mine.MoveTo(initialPlacement.Left, initialPlacement.Top);
            var v = mine.Velocity;
            v.Speed = holder.Velocity.Speed + speed;
            v.Angle = holder.TargetAngle;
            new Friction(v);
        }
    }
}
