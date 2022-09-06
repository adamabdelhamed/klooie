namespace klooie.Gaming;

public static class DramaticImpact
{
    public static async Task KnockBackAsync(this GameCollider el, Angle angle, float speed, RGB highlightColor, string sound, bool selfKnock = false)
    {
        /*
        if (selfKnock == false && CodeGame.Current.Grasp.IsGrasping(el))
        {
            return;
        }
        */
        if (el is IAmMass)
        {
            el = (el as IAmMass).Parent as GameCollider;
        }
        /*
        if (el is Character)
        {
            (el as Character).Get<DuckAndCoverState>(nameof(DuckAndCoverState))?.Dispose();
            if (el is MainCharacter)
            {
                new DetachedState(el as Character);
            }
        }
        */
        new Force2(el.Velocity, 10, angle, TimeSpan.FromSeconds(.3f));
    }
}
