using System;
namespace klooie.Gaming;

public class FrameTaskScheduler : Recyclable
{
    private Event<(IFrameTask,TimeSpan)>? _taskIsLate;
    public Event<(IFrameTask, TimeSpan)> TaskIsLate => _taskIsLate ??= Event<(IFrameTask, TimeSpan)>.Create();
    private RecyclableList<LeaseState<Recyclable>> queue;
    private double taskAccumulator = 0;

    public float Frequency { get; private set; }
    private TimeSpan frequencyTimeSpan;
    public TimeSpan GracePeriod { get; set; }

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
        frequencyTimeSpan = TimeSpan.FromMilliseconds(Frequency);
        GracePeriod = TimeSpan.FromMilliseconds(Frequency * 0.1); // 10% grace period
        queue = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent();
        ConsoleApp.Current.AfterPaint.Subscribe(this, static (me) => me.Process(), this);
    }

    public void Enqueue<T>(T task) where T : Recyclable, IFrameTask
    {
        task.LastExecutionTime = Game.Current.MainColliderGroup.Now;
        queue.Items.Add(LeaseHelper.Track((Recyclable)task));
    }

    private void Process()
    {
        if (queue.Count == 0) return;

        double frameDurationMs = GetCurrentFrameDurationMs();
        double framesPerCycle = Frequency / frameDurationMs; // Recalculated each frame


        double tasksPerFrame = queue.Count / framesPerCycle;
        taskAccumulator += tasksPerFrame;

        int tasksToRun = Math.Min((int)taskAccumulator, queue.Count);
        taskAccumulator -= tasksToRun;

        if (tasksToRun == 0) return;


        for (int i = 0; i < tasksToRun && queue.Count > 0; i++)
        {
            var lease = queue[0];
            queue.Items.RemoveAt(0);
            if (lease.IsRecyclableValid)
            {
                var task = (IFrameTask)lease.Recyclable!;
                var now = Game.Current.MainColliderGroup.Now;
                var elapsed = now - task.LastExecutionTime;
                if (elapsed > frequencyTimeSpan + GracePeriod)
                {
                    _taskIsLate?.Fire((task,elapsed-frequencyTimeSpan));
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

    private double GetCurrentFrameDurationMs()
    {
        double fps;
        // Avoid divide by zero in edge cases
        if (Game.Current.MainColliderGroup.Now < TimeSpan.FromSeconds(2))
        {
            fps = LayoutRootPanel.MaxPaintRate * .85;
        }
        else
        {
            fps = Math.Max(ConsoleApp.Current.FramesPerSecond, 1);
        }
        return 1000.0 / fps;
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

