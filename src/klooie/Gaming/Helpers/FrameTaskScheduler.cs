using System;
namespace klooie.Gaming;

public class FrameTaskScheduler : Recyclable
{
    public const int IdealFrameRate = 50;

    private RecyclableList<LeaseState<Recyclable>> queue;
    private double delayMs;
    private double framesPerCycle;

    public float Frequency { get; private set; }

    private static LazyPool<FrameTaskScheduler> pool = new LazyPool<FrameTaskScheduler>(() => new FrameTaskScheduler());

    private FrameTaskScheduler() { }

    public static FrameTaskScheduler Create(float frequency)
    {
        var sched = pool.Value.Rent();
        sched.Init(frequency);
        return sched;
    }

    private void Init(float frequency)
    {
        Frequency = frequency;
        queue = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent();
        CalculateTiming();
        Game.Current.InnerLoopAPIs.DelayIfValid(delayMs, FrameTaskSchedulerState.Create(this), LoopBody);
    }

    public void Enqueue(IFrameTask task)
    {
        queue.Items.Add(LeaseHelper.Track((Recyclable)task));
    }

    private void CalculateTiming()
    {
        delayMs = 1000.0 / IdealFrameRate;
        if (Frequency < delayMs)
        {
            throw new ArgumentException("Frequency cannot be faster than the configured frame rate");
        }
        framesPerCycle = Math.Max(1, Frequency / delayMs);
    }

    private static void LoopBody(object obj)
    {
        var state = (FrameTaskSchedulerState)obj;
        state.Scheduler.Process();
        Game.Current.InnerLoopAPIs.DelayIfValid(state.Scheduler.delayMs, state, LoopBody);
    }

    private void Process()
    {
        if (queue.Count == 0) return;
        int tasksPerTick = Math.Max(1, (int)Math.Ceiling(queue.Count / framesPerCycle));
        for (int i = 0; i < tasksPerTick && queue.Count > 0; i++)
        {
            var lease = queue[0];
            queue.Items.RemoveAt(0);
            if (lease.IsRecyclableValid)
            {
                var task = (IFrameTask)lease.Recyclable!;
                var now = Game.Current.MainColliderGroup.Now;
                if (now - task.LastExecutionTime > TimeSpan.FromMilliseconds(Frequency))
                {
                    TaskIsLate(task);
                }
                FrameDebugger.RegisterTask(task.Name);
                task.Execute();
                task.LastExecutionTime = now;
                queue.Items.Add(lease);
            }
            else
            {
                lease.TryDispose();
            }
        }
    }

    protected virtual void TaskIsLate(IFrameTask task) { }

    protected override void OnReturn()
    {
        base.OnReturn();
        if (queue != null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                queue[i].TryDispose();
            }
            queue.Dispose();
            queue = null;
        }
    }
}

public class FrameTaskSchedulerState : DelayState
{
    public FrameTaskScheduler Scheduler { get; private set; }

    public static FrameTaskSchedulerState Create(FrameTaskScheduler scheduler)
    {
        var state = FrameTaskSchedulerStatePool.Instance.Rent();
        state.Scheduler = scheduler;
        state.AddDependency(scheduler);
        return state;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Scheduler = null!;
    }
}

