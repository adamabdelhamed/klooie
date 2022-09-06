using klooie.Gaming.Code;
using PowerArgs;

using System;
using System.Threading.Tasks;

namespace klooie.Gaming;

    public class OffenseOptions
    {
        public string TargetTag { get; set; }
        public float Sleep { get; set; } = 1000;
        public float Visibility { get; set; } = 1000;
        public float ReactionTime { get; set; } = 1000;
        public int Burst { get; set; } = 1;
        public float Range { get; set; } = -1f;
        public bool Alt { get; set; }

        public Action OnSleep { get; set; }
        public Action AfterBurst { get; set; }
    }

public class OffenseHost
{
    private Character c;
    public OffenseOptions Options { get; private set; }

    public OffenseHost(Character c, OffenseOptions options)
    {
        this.c = c;
        this.Options = options;
        Game.Current.Invoke(async () => await DoOffense());
    }

    private async Task DoOffense()
    {
        var preAlertsEnabled = DynamicArg.FromVariable("PreAlertHintEnabled").BooleanValue;

        c.DisableTargeting();
        var targetingOptions = new AutoTargetingOptions()
        {
            Source = c,
            TargetTag = Options.TargetTag,
            AngularVisibility = 360,
        };

        c.OverrideTargeting(new AutoTargetingFunction(targetingOptions));

        c.Targeting.TargetAcquired.Subscribe(async (target) =>
        {
            var targetLifetime = c.Targeting.CurrentTargetLifetime;
            while (Options.Range > 0 && target.CalculateDistanceTo(c) > Options.Range)
            {
                await Task.Yield();
                if (targetLifetime.IsExpired) return;
            }

            if (Options.ReactionTime > 0)
            {
                await Game.Current.Delay(Options.ReactionTime);
                if (targetLifetime.IsExpired) return;
            }


            while (targetLifetime.IsExpired == false)
            {

                for (var i = 0; i < Options.Burst; i++)
                {
                    while (Options.Range > 0 && target.CalculateDistanceTo(c) > Options.Range)
                    {
                        await Task.Yield();
                        if (targetLifetime.IsExpired) return;
                    }

                    //if (targetLifetime.IsExpired || Game.Current.Grasp.IsInGrasp(c)) return;

                    //if (Game.Current.CameraPanel.CameraBounds.Contains(c.Bounds) == false) return;

                    if (c.Inventory.PrimaryWeapon != null)
                    {
                        c.Inventory.PrimaryWeapon.TryFire(Options.Alt);
                    }
                    else if (c.Inventory.ExplosiveWeapon != null)
                    {
                        c.Inventory.ExplosiveWeapon.TryFire(Options.Alt);
                    }

                    await Game.Current.Delay(50);
                }

                Options.AfterBurst?.Invoke();

                if (Options.Sleep > 0)
                {
                    Options.OnSleep?.Invoke();
                    await Game.Current.Delay(Options.Sleep);
                }
                else
                {
                    await Task.Yield();
                }

            }
        }, c);
    }
}
