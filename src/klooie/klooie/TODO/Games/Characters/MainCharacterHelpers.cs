namespace klooie.Gaming;
public static class MainCharacterHelpers
{
    public static void ApplyWhenMainCharacterEnters(Action syncAction)
    {
        if (MainCharacter.Current != null)
        {
            syncAction();
        }
        else
        {
            var waitForMcLt = Game.Current.CreateChildLifetime();
            Game.Current.GamePanel.Controls.Added.Subscribe(el =>
            {
                if (waitForMcLt.IsExpired) return;
                if (el is MainCharacter)
                {
                    Game.Current.InvokeNextCycle(syncAction);
                    waitForMcLt.Dispose();
                }

            }, waitForMcLt);
        }
    }


    public static void ConfigureNPCApproach(GameCollider thingBeingApproached, Action action, int maxApproaches = -1)
    {
        Game.Current.Invoke(async () =>
       {
           while (MainCharacter.Current == null)
           {
               await Game.Current.DelayFuzzy(333);
           }


           var approaches = 0;
           while (thingBeingApproached.IsExpired == false && MainCharacter.Current != null)
           {
               if (MainCharacter.Current.CalculateDistanceTo(thingBeingApproached.MassBounds) <= 1)
               {
                   action();
                   approaches++;

                   if (maxApproaches > 0 && approaches >= maxApproaches)
                   {
                       break;
                   }

                   while (MainCharacter.Current != null && thingBeingApproached.IsExpired == false && MainCharacter.Current.CalculateDistanceTo(thingBeingApproached.MassBounds) <= 10)
                   {
                       await Task.Yield();
                   }
               }
               await Task.Yield();
           }
       });
    }
}
