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

    private PauseManager? pauseManager;

    public int TotalTasks => pendingForCurrentFrequencyPeriod.Count + readyForNextFrequencyPeriod.Count;

    private FrameTaskScheduler() { }
    public static FrameTaskScheduler Create(TimeSpan frequency, PauseManager? pauseManager = null)
    {
        var sched = pool.Value.Rent();
        sched.pauseManager = pauseManager;
        sched.Init(frequency);
        return sched;
    }

    private void Init(TimeSpan frequency)
    {
        Frequency = frequency;
        pendingForCurrentFrequencyPeriod = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent();
        readyForNextFrequencyPeriod = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent();
        if (pauseManager != null)
        {
            ConsoleApp.Current.AfterPaint.SubscribePaused(pauseManager, this, static (me) => me.Process(), this);
            pauseManager.OnPaused.Subscribe(this, static (me, lt) =>
            {
                me.currentPassStartTime = null;
                if (me.pendingForCurrentFrequencyPeriod.Count > 0)
                {
                    me.readyForNextFrequencyPeriod.Items.AddRange(me.pendingForCurrentFrequencyPeriod.Items);
                    me.pendingForCurrentFrequencyPeriod.Items.Clear();
                }
            }, this);
        }
        else
        {
            ConsoleApp.Current.AfterPaint.Subscribe(this, static (me) => me.Process(), this);
        }
    }

    public void Enqueue<T>(T task) where T : Recyclable, IFrameTask
    {
        readyForNextFrequencyPeriod.Items.Add(LeaseHelper.Track((Recyclable)task));
    }


    private void Process()
    {
        var now = Game.Current.MainColliderGroup.Now;

        // Only proceed if it's the start of a new frequency period
        if (!currentPassStartTime.HasValue || now - currentPassStartTime.Value >= Frequency)
        {
            currentPassStartTime = now;

            // Check lateness
            var grace = .25 * TotalTasks;
            if (pendingForCurrentFrequencyPeriod.Count > grace)
            {
                _taskIsLate?.Fire(pendingForCurrentFrequencyPeriod.Count);
            }

            // Move all ready tasks to pending for this period
            if (readyForNextFrequencyPeriod.Count > 0)
            {
                pendingForCurrentFrequencyPeriod.Items.AddRange(readyForNextFrequencyPeriod.Items);
                readyForNextFrequencyPeriod.Items.Clear();
            }
        }

        // No processing if there are no tasks to run
        if (pendingForCurrentFrequencyPeriod.Count == 0)
            return;

        // Compute number of tasks to run *this frame*
        int processed = 0;
        var totalTasksPerFrequency = pendingForCurrentFrequencyPeriod.Count;
        var framesPerFrequency = LayoutRootPanel.MaxPaintRate * (Frequency.TotalMilliseconds / 1000.0);
        var tasksPerFrame = (int)Math.Ceiling(.98f * totalTasksPerFrequency / framesPerFrequency);

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

