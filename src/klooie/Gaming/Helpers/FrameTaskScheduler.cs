using System;
namespace klooie.Gaming;

public class FrameTaskScheduler : Recyclable
{
    private Event<int>? _taskIsLate;
    public Event<int> TaskIsLate => _taskIsLate ??= Event<int>.Create();
    private RecyclableList<LeaseState<Recyclable>> pendingForCurrentFrequencyPeriod;
    private RecyclableList<LeaseState<Recyclable>> readyForNextFrequencyPeriod;

    public TimeSpan Frequency { get; private set; }

    private static LazyPool<FrameTaskScheduler> pool = new LazyPool<FrameTaskScheduler>(() => new FrameTaskScheduler());
    TimeSpan? currentPassStartTime;
    private FrameTaskScheduler() { }
    public static FrameTaskScheduler Create(TimeSpan frequency)
    {
        var sched = pool.Value.Rent();
        sched.Init(frequency);
        return sched;
    }

    private void Init(TimeSpan frequency)
    {
        Frequency = frequency;
        pendingForCurrentFrequencyPeriod = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent();
        readyForNextFrequencyPeriod = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent();
        ConsoleApp.Current.AfterPaint.Subscribe(this, static (me) => me.Process(), this);
    }

    public void Enqueue<T>(T task) where T : Recyclable, IFrameTask
    {
        readyForNextFrequencyPeriod.Items.Add(LeaseHelper.Track((Recyclable)task));
    }


    private void Process()
    {
        var now = Game.Current.MainColliderGroup.Now;
        if (!currentPassStartTime.HasValue || now - currentPassStartTime.Value > Frequency || pendingForCurrentFrequencyPeriod.Count == 0)
        {
            if (readyForNextFrequencyPeriod.Count == 0) return;

            var grace = .025 * (pendingForCurrentFrequencyPeriod.Count + readyForNextFrequencyPeriod.Count);
            if (pendingForCurrentFrequencyPeriod.Count > grace)
            {
                _taskIsLate?.Fire(pendingForCurrentFrequencyPeriod.Count);
            }
            currentPassStartTime = now;
            pendingForCurrentFrequencyPeriod.Items.AddRange(readyForNextFrequencyPeriod.Items);
            readyForNextFrequencyPeriod.Items.Clear();
        }

        // Now check if there is work to do
        if (pendingForCurrentFrequencyPeriod.Count == 0)
        {
            // No tasks left after replenish, nothing to do this frame
            return;
        }
        int processed = 0;
        var totalTasksPerFrequency = pendingForCurrentFrequencyPeriod.Count + readyForNextFrequencyPeriod.Count;
        var framesPerFrequency = LayoutRootPanel.MaxPaintRate * (Frequency.TotalMilliseconds / 1000.0);
        var tasksPerFrame = (int)Math.Ceiling(.98f * totalTasksPerFrequency / framesPerFrequency); // err on the side of being a little late to make sure we maximize the use of every frame

        var remainingAfterThisFrame = pendingForCurrentFrequencyPeriod.Count - tasksPerFrame;
        if(remainingAfterThisFrame < tasksPerFrame * .1f)
        {
            tasksPerFrame += remainingAfterThisFrame;
        }

        while (processed < tasksPerFrame && pendingForCurrentFrequencyPeriod.Count > 0)
        {
            var lease = pendingForCurrentFrequencyPeriod[0];
            var task = (IFrameTask)lease.Recyclable!;
            pendingForCurrentFrequencyPeriod.Items.RemoveAt(0);
            if (!lease.IsRecyclableValid)
            {
                lease.Dispose();
                continue;
            }

            FrameDebugger.RegisterTask(task.Name);
            task.Execute();
            if (lease.IsRecyclableValid)
            {
                readyForNextFrequencyPeriod.Items.Add(lease);
            }
            else
            {
                lease.TryDispose(); 
            }
            processed++;
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        if (pendingForCurrentFrequencyPeriod != null)
        {
            for (int i = 0; i < pendingForCurrentFrequencyPeriod.Count; i++)
            {
                pendingForCurrentFrequencyPeriod[i].TryDispose();
            }

            for (int i = 0; i < readyForNextFrequencyPeriod.Count; i++)
            {
                readyForNextFrequencyPeriod[i].TryDispose();
            }

            pendingForCurrentFrequencyPeriod.Dispose();
            pendingForCurrentFrequencyPeriod = null;

            readyForNextFrequencyPeriod.Dispose();
            readyForNextFrequencyPeriod = null;

        }
        currentPassStartTime = null;
        _taskIsLate?.Dispose();
        _taskIsLate = null;
    }
}

