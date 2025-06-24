using System;
namespace klooie.Gaming;

public class FrameTaskScheduler : Recyclable
{

    // Assertion: The lower this number, the more evenly I will distribute tasks across frames, but with an increased risk of late execution.
    // Assertion: 10 would give me a max throughput of 600 tasks per second at FPS 60 with little risk of late execution.
    // Assertion: Hard coding this number guarantees that I won't have unbounded CPU usage per frame.
    // Observation: I am testing now with a value of 10 and ~800 tasks in the queue (~40 % more than what it can handle), which is giving executions that are consistently ~40% late.
    // Hypothesis: If I hard code a total max # of tasks per scheduler then I can have my cake and eat it too. I'll cap the CPU usage and still guarantee on time delivery.
    //             If I allow the caller to increase the grace period then that will let them have more tasks in the scheduler with an understanding that they will be late.
    private int MaxTasksPerFrame { get; set; }
    private int TargetFPS { get; set; }
    private Event<(IFrameTask,TimeSpan)>? _taskIsLate;
    public Event<(IFrameTask, TimeSpan)> TaskIsLate => _taskIsLate ??= Event<(IFrameTask, TimeSpan)>.Create();
    private RecyclableList<LeaseState<Recyclable>> queue;
    private double taskAccumulator = 0;

    public float Frequency { get; private set; }
    private TimeSpan frequencyTimeSpan;
    public TimeSpan GracePeriod { get; set; }

    public int TaskCount => queue.Count;

    public int MaximumTaskCapacity
    {
        get
        {
            var maxTasksCapcityWithNoGracePeriodAt60FPS = MaxTasksPerFrame * TargetFPS;
            var gracePeriodPercentage = GracePeriod.TotalMilliseconds / frequencyTimeSpan.TotalMilliseconds;
            var maxTasksWithGracePeriod = (int)(maxTasksCapcityWithNoGracePeriodAt60FPS * (1 + gracePeriodPercentage));
            return maxTasksWithGracePeriod;
        }
    }


    private static LazyPool<FrameTaskScheduler> pool = new LazyPool<FrameTaskScheduler>(() => new FrameTaskScheduler());

    private readonly Comparison<LeaseState<Recyclable>> _dueComparer;
    private static TimeSpan comparerFrequency;
    private FrameTaskScheduler() 
    {
        _dueComparer = static (a, b) =>
        {
            if (!a.IsRecyclableValid && !b.IsRecyclableValid) return 0;
            if (!a.IsRecyclableValid) return 1;
            if (!b.IsRecyclableValid) return -1;

            var now = Game.Current.MainColliderGroup.Now;
            var at = (IFrameTask)a.Recyclable!;
            var bt = (IFrameTask)b.Recyclable!;
            // Lower remaining time until due gets priority (overdue is negative)
            double aDue = (at.LastExecutionTime + comparerFrequency - now).TotalMilliseconds;
            double bDue = (bt.LastExecutionTime + comparerFrequency - now).TotalMilliseconds;
            return aDue.CompareTo(bDue);
        };
    }

    public static FrameTaskScheduler Create(float frequency)
    {
        var sched = pool.Value.Rent();
        sched.Init(frequency);
        return sched;
    }

    private void Init(float frequency)
    {
        MaxTasksPerFrame = 10;
        TargetFPS = LayoutRootPanel.MaxPaintRate;
        Frequency = frequency;
        frequencyTimeSpan = TimeSpan.FromMilliseconds(Frequency);
        GracePeriod = TimeSpan.FromMilliseconds(Frequency * 0.1); // 10% grace period
        queue = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent();
        ConsoleApp.Current.AfterPaint.Subscribe(this, static (me) => me.Process(), this);
    }

    public void Enqueue<T>(T task) where T : Recyclable, IFrameTask
    {
        if(queue.Count >= MaximumTaskCapacity) throw new InvalidOperationException($"Cannot enqueue task {task.Name} because the scheduler has reached its maximum capacity of {MaximumTaskCapacity} tasks. Consider increasing the grace period or reducing the number of tasks in the queue.");
        task.LastExecutionTime = Game.Current.MainColliderGroup.Now; // phase will be reset if resequencing
        queue.Items.Add(LeaseHelper.Track((Recyclable)task));
    }


    private void Process()
    {
        if (queue.Count == 0) return;

        comparerFrequency = frequencyTimeSpan;
        //queue.Items.Sort(_dueComparer);
        int processed = 0, checkedTasks = 0, queueSize = queue.Count;
        var now = Game.Current.MainColliderGroup.Now;

        while (processed < MaxTasksPerFrame && checkedTasks < queueSize && queue.Count > 0)
        {
            var lease = queue[0];
            var task = (IFrameTask)lease.Recyclable!;
            queue.Items.RemoveAt(0);
            if (!lease.IsRecyclableValid)
            {
                lease.Dispose();
                checkedTasks++;
                continue;
            }


            var elapsed = now - task.LastExecutionTime;

            if (elapsed < frequencyTimeSpan)
            {
                queue.Items.Add(lease);
                checkedTasks++;
                continue;
            }

            if (elapsed > frequencyTimeSpan + GracePeriod)
            {
                _taskIsLate?.Fire((task, elapsed - (frequencyTimeSpan+GracePeriod)));
            }
            FrameDebugger.RegisterTask(task.Name);
            task.Execute();
            task.LastExecutionTime = now;
            if (lease.IsRecyclableValid)
            {
                queue.Items.Add(lease);
            }
            else
            {
                lease.TryDispose(); // Defensive, if not already disposed
            }
            processed++;
            checkedTasks++;
        }
    }

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
        _taskIsLate?.Dispose();
        _taskIsLate = null;
    }
}

