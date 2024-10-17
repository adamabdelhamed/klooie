﻿namespace ScrollSucker;
public class SmartTrigger
{
    private float burstWindow;

    private int minShotsBeforeEnforced;
    private int maxShotsInBurstWindow;
    private LinkedList<TimeSpan> burstRecord = new LinkedList<TimeSpan>();
    public SmartTrigger(float burstWindow = 1000, int minShotsBeforeEnforced = 3, int maxShotsInBurstWindow = 5)
    {
        this.burstWindow = burstWindow;
        this.minShotsBeforeEnforced = minShotsBeforeEnforced;
        this.maxShotsInBurstWindow = maxShotsInBurstWindow;
    }

    public bool AllowFire()
    {
        Prune();
        if (minShotsBeforeEnforced > burstRecord.Count || burstRecord.Count < maxShotsInBurstWindow)
        {
            burstRecord.AddLast(Game.Current.MainColliderGroup.Now);
            return true;
        }
        else
        {
            return false;
        }
    }

    private void Prune()
    {
        var current = burstRecord.First;
        while (current != null && Game.Current.MainColliderGroup.Now - current.Value > TimeSpan.FromMilliseconds(burstWindow))
        {
            burstRecord.RemoveFirst();
            current = burstRecord.First;
        }
    }
}