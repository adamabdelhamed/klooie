using System.Runtime.CompilerServices;

namespace klooie.Gaming;
public partial class TargetingQueue : Recyclable, IObservableObject
{
    private const double Delay = 2;
    private List<TargetingQueueEntry> evaluationQueue;
    public partial int PendingEvaluations { get; set; }
    public partial int Enqueues { get; set; }
    public partial int Dequeues { get; set; }
    public partial int Evaluations { get; set; }

    protected override void OnInit()
    {
        base.OnInit();
        evaluationQueue = new List<TargetingQueueEntry>();
        ConsoleApp.Current.InnerLoopAPIs.Delay(Delay, this, OnEndOfCycle);
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        foreach (var entry in evaluationQueue)
        {
            entry.TryDispose();
        }

        evaluationQueue.Clear();
        evaluationQueue = null;
    }

    private static void OnEndOfCycle(object me)
    {
        var _this = (TargetingQueue)me;
        int maxToDequeueOnEachCycle = 5;
        int actuallyDequeuedOnThisCycle = 0;
        while (_this.evaluationQueue.Count > 0)
        {
            var queuedJobRecord = _this.evaluationQueue[_this.evaluationQueue.Count - 1];
            _this.evaluationQueue.RemoveAt(_this.evaluationQueue.Count - 1);
            _this.Dequeues++;
            _this.PendingEvaluations--;
            if (queuedJobRecord.IsTargetingStillValid)
            {
                queuedJobRecord.Evaluate();
                _this.Evaluations++;
                actuallyDequeuedOnThisCycle++;
                _this.evaluationQueue.Insert(0, queuedJobRecord);
                _this.Enqueues++;
                _this.PendingEvaluations++;

                if (actuallyDequeuedOnThisCycle >= maxToDequeueOnEachCycle)
                {
                    break;
                }
            }
            else
            {
                queuedJobRecord.TryDispose();
            }
        }
        ConsoleApp.Current.InnerLoopAPIs.Delay(Delay, me, OnEndOfCycle);
    }

    public void Add(Targeting target)
    {
        var entry = TargetingQueueEntryPool.Instance.Rent();
        entry.Bind(target);
        evaluationQueue.Insert(0, entry);
        Enqueues++;
        PendingEvaluations++;
    }
}

public class TargetingQueueEntry : Recyclable
{
    public Targeting Target { get; private set; }
    public int Lease { get; private set; }

    public bool IsTargetingStillValid => Target != null && Target.IsStillValid(Lease);

    public int EvaluationCount { get; private set; }

    public void Evaluate()
    {
        EvaluationCount++;
        Target.Evaluate();
    }

    public void Bind(Targeting target)
    {
        Target = target;
        Lease = target.Lease;
    }

    protected override void OnReturn()
    {
        Target = null;
        Lease = 0;
        EvaluationCount = 0;
    }

    public override string ToString()
    {
        return $"EvaluationCount: {EvaluationCount}";
    }
}

