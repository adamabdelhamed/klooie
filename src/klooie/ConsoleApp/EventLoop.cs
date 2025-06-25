using System.Runtime.ExceptionServices;
namespace klooie;
public class EventLoop : Recyclable
{
    private class SynchronizedEventPool
    {
        private Lock lockObject = new Lock();
        private SynchronizedEvent[] pool;
        private int count;
        public SynchronizedEventPool()
        {
            pool = new SynchronizedEvent[5];
        }

        public SynchronizedEvent Get()
        {
            lock (lockObject)
            {
                for (var i = 0; i < pool.Length; i++)
                {
                    if (pool[i] != null)
                    {
                        var ret = pool[i];
                        pool[i] = null;
                        count--;
                        MaybeShrink();
                        return ret;
                    }
                }
                return new SynchronizedEvent();
            }
        }

        public void Return(SynchronizedEventBase done)
        {
            if(done is not SynchronizedEvent se)
            {
                done.Clean();
                return; // don't pool generic events
            }

            lock (lockObject)
            {
                se.Clean();
                for (var i = 0; i < pool.Length; i++)
                {
                    if (pool[i] == null)
                    {
                        pool[i] = se;
                        count++;
                        MaybeShrink();
                        return;
                    }
                }

                Grow();
                pool[count++] = se;
            }
        }

        private void Grow()
        {
            var tmp = pool;
            pool = new SynchronizedEvent[tmp.Length * 2];
            Array.Copy(tmp, pool, tmp.Length);
        }

        private void MaybeShrink()
        {
            if (count == 0) return;
            if(count <= pool.Length * .15)
            {
                var tmp = pool;
                pool = new SynchronizedEvent[count * 2];
                var newI = 0;
                for(var i = 0; i < tmp.Length; i++)
                {
                    if(tmp[i] != null)
                    {
                        pool[newI++] = tmp[i];
                    }
                }
            }
        }
    }

    private abstract class SynchronizedEventBase
    {
        public Task Task { get; protected set; }
        public bool IsFinished => Task == null || (Task.IsCompleted || Task.IsFaulted || Task.IsCanceled);
        public bool IsFailed => Task?.Exception != null;
        public Exception Exception => Task?.Exception;
        public abstract void Run();
        public abstract void Clean();
    }

    private class SynchronizedEvent : SynchronizedEventBase
    {
        public object WorkState { get; set; }
        public Func<object,Task> AsyncWork { get;  set; }
        public SendOrPostCallback Callback { get; set; }
        public object CallbackState { get; set; }

        public override void Clean()
        {
            AsyncWork = null;
            Callback = null;
            Task = null;
            WorkState = null;
            CallbackState = null;
        }

        public override void Run()
        {
            Task = AsyncWork?.Invoke(WorkState);
            Callback?.Invoke(CallbackState);
        }
    }

    private class SynchronizedEvent<TScope> : SynchronizedEventBase
    {
        public TScope WorkState { get; set; }
        public Func<TScope, Task> AsyncWork { get; set; }
        public SendOrPostCallback Callback { get; set; }
        public TScope CallbackState { get; set; }

        public override void Clean()
        {
            AsyncWork = null;
            Callback = null;
            WorkState = default;
            CallbackState = default;
            Task = null;
        }

        public override void Run()
        {
            Task = AsyncWork?.Invoke(WorkState);
            Callback?.Invoke(CallbackState);
        }
    }

    public enum EventLoopExceptionHandling
    {
        Throw,
        Stop,
        Swallow,
    }

    private class StopLoopException : Exception { }

    private class CustomSyncContext : SynchronizationContext
    {
        private EventLoop loop;

        public long Posts;
        public long Sends;

        public CustomSyncContext(EventLoop loop)
        {
            this.loop = loop;
            loop.OnDisposed(() => this.loop = null);
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            if (loop != null && loop.IsRunning && loop.IsDrainingOrDrained == false)
            {
                Posts++;
                loop.InvokeNextCycle(d, state);
            }
        }
            
        public override void Send(SendOrPostCallback d, object state)
        {
            if (Thread.CurrentThread != loop?.Thread && loop != null && loop.IsRunning && loop.IsDrainingOrDrained == false)
            {
                Sends++;
                loop.InvokeNextCycle(() => d.Invoke(state));
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }

    private SynchronizedEventPool pool = new SynchronizedEventPool();

    private Event startOfCycle;
    private Event endOfCycle;
    private Event loopStarted;
    private Event loopStopped;
    public Event StartOfCycle => startOfCycle ??= Event.Create();
    public Event EndOfCycle => endOfCycle ??= Event.Create();
    public Event LoopStarted => loopStarted ??= Event.Create();
    public Event LoopStopped => loopStopped ??= Event.Create();
    public Thread Thread { get; private set; }
    public long Posts => syncContext.Posts;
    public long Sends => syncContext.Sends;

    public long AsyncContinuations => Posts + Sends;

    public ThreadPriority Priority { get; set; } = ThreadPriority.AboveNormal;
    public bool IsRunning => runDeferred != null;
    public long Cycle { get; private set; }
    protected string Name { get; set; }
    private List<SynchronizedEventBase> workQueue = new List<SynchronizedEventBase>();
    private List<SynchronizedEventBase> pendingWorkItems = new List<SynchronizedEventBase>();
    private TaskCompletionSource<bool> runDeferred;
    private bool stopRequested;
    private CustomSyncContext syncContext;
    public bool IsDrainingOrDrained { get; private set; }


    private bool runMode;
    private Task runTask;

    public EventLoop() { }

    /// <summary>
    /// Runs the event loop using the current thread
    /// </summary>
    public virtual void Run()
    {
        runMode = true;
        Thread = System.Threading.Thread.CurrentThread;
        runDeferred = new TaskCompletionSource<bool>();
        RunCommon();
        try
        {
            runTask.Wait();
        }
        catch(Exception ex)
        {
            if (ex is AggregateException == false) throw;
            var cleaned = ex.Clean();
            if(cleaned.Count == 1)
            {
                ExceptionDispatchInfo.Capture(cleaned.First()).Throw();
            }
            else
            {
                throw;
            }
        }
    }

    private void RunCommon()
    {
        syncContext = new CustomSyncContext(this);
        SynchronizationContext.SetSynchronizationContext(syncContext);
        try
        {
            Loop();
            runDeferred.SetResult(true);
        }
        catch (Exception ex)
        {
            runDeferred.SetException(ex);
        }
        finally
        {
            if (runMode)
            {
                runTask = runDeferred.Task;
            }
            runDeferred = null;
        }
    }

    private void Loop()
    {
        try
        {
            stopRequested = false;
            Cycle = -1;
            LoopStarted.Fire();
            List<SynchronizedEventBase> todoOnThisCycle = new List<SynchronizedEventBase>();
            while (stopRequested == false)
            {
                if (Cycle == long.MaxValue)
                {
                    Cycle = 0;
                }
                else
                {
                    Cycle++;
                }

                try
                {
                    StartOfCycle.Fire();
                }
                catch (Exception ex)
                {
                    var handling = HandleWorkItemException(ex, null);
                    if (handling == EventLoopExceptionHandling.Throw)
                    {
                        throw;
                    }
                    else if (handling == EventLoopExceptionHandling.Stop)
                    {
                        return;
                    }
                    else if (handling == EventLoopExceptionHandling.Swallow)
                    {
                        // swallow
                    }
                }

                   
                lock (workQueue)
                {
                    while (workQueue.Count > 0)
                    {
                        var workItem = workQueue[0];
                        workQueue.RemoveAt(0);
                        todoOnThisCycle.Add(workItem);
                    }
                }

                for (var i = 0; i < pendingWorkItems.Count; i++)
                {
                    if (pendingWorkItems[i].IsFinished && pendingWorkItems[i].IsFailed)
                    {
                        var handling = HandleWorkItemException(pendingWorkItems[i].Exception, pendingWorkItems[i]);
                        if (handling == EventLoopExceptionHandling.Throw)
                        {
                            ExceptionDispatchInfo.Capture(pendingWorkItems[i].Exception).Throw();
                        }
                        else if (handling == EventLoopExceptionHandling.Stop)
                        {
                            return;
                        }
                        else if (handling == EventLoopExceptionHandling.Swallow)
                        {
                            // swallow
                        }

                        var wi = pendingWorkItems[i];
                        pendingWorkItems.RemoveAt(i--);
                        pool.Return(wi);
                        if (stopRequested)
                        {
                            return;
                        }
                    }
                    else if (pendingWorkItems[i].IsFinished)
                    {
                        var wi = pendingWorkItems[i];
                        pendingWorkItems.RemoveAt(i--);
                        pool.Return(wi);
                        if (stopRequested)
                        {
                            return;
                        }
                    }
                }

                foreach (var workItem in todoOnThisCycle)
                {
                    try
                    {
                        workItem.Run();
                        if (workItem.IsFinished == false)
                        {
                            pendingWorkItems.Add(workItem);
                        }
                        else if(workItem.Exception != null)
                        {
                            throw new AggregateException(workItem.Exception);
                        }
                        else
                        {
                            pool.Return(workItem);
                            if (stopRequested)
                            {
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var handling = HandleWorkItemException(ex, workItem);
                        if (handling == EventLoopExceptionHandling.Throw)
                        {
                            throw;
                        }
                        else if (handling == EventLoopExceptionHandling.Stop)
                        {
                            return;
                        }
                        else if (handling == EventLoopExceptionHandling.Swallow)
                        {
                            // swallow
                        }
                    }
                }

                todoOnThisCycle.Clear();

                try
                {
                    EndOfCycle.Fire();
                }
                catch (Exception ex)
                {
                    var handling = HandleWorkItemException(ex, null);
                    if (handling == EventLoopExceptionHandling.Throw)
                    {
                        throw;
                    }
                    else if (handling == EventLoopExceptionHandling.Stop)
                    {
                        return;
                    }
                    else if (handling == EventLoopExceptionHandling.Swallow)
                    {
                        // swallow
                    }
                }
            }
        }
        finally
        {
            IsDrainingOrDrained = true;
            LoopStopped.Fire();
            SynchronizationContext.SetSynchronizationContext(null);
        }
    }

    public void Stop()
    {
        if(IsRunning == false)
        {
            throw new Exception("Not running");
        }

        Invoke(() =>
        {
            throw new StopLoopException();
        });
    }

    public void Invoke(Action work) => Invoke(()=>
    {
        work();
        return Task.CompletedTask;
    });

    public void InvokeNextCycle(Action work) => InvokeNextCycle(() =>
    {
        work();
        return Task.CompletedTask;
    });

 
    public void InvokeNextCycle(Func<Task> work) => Invoke(async () =>
    {
        await Task.Yield();
        Invoke(work);
    });

    public void InvokeNextCycle(SendOrPostCallback callback, object state)
    {
        if (IsRunning == false && IsDrainingOrDrained)
        {
            return;
        }
        lock (workQueue)
        {
            var workItem = pool.Get();
            workItem.Callback = callback;
            workItem.CallbackState = state;
            workQueue.Add(workItem);
        }
    }

    public void InvokeNextCycle<TScope>(SendOrPostCallback callback, TScope state)
    {
        if (IsRunning == false && IsDrainingOrDrained)
        {
            return;
        }
        lock (workQueue)
        {
            var workItem = new SynchronizedEvent<TScope>();
            workItem.Callback = callback;
            workItem.CallbackState = state;
            workQueue.Add(workItem);
        }
    }

    public Task InvokeAsync(Func<Task> work)
    {
        var tcs = new TaskCompletionSource();
        Invoke(async () =>
        {
            try
            {
                await work();
                tcs.SetResult();
            }
            catch(Exception ex)
            {
                tcs.SetException(ex);
                throw;
            }
        });
        return tcs.Task;
    }

    /*
     * TODO: This Delay implementation uses more memory than Task.Delay so I've commented it out.
     * However, it might be more precise than Task.Delay. Future testing should be done.
    public Task Delay(int ms)
    {
        var tcs = new TaskCompletionSource();
        InnerLoopAPIs.Delay(ms, tcs, StaticSetResult);
        return tcs.Task;
    }
    private static void StaticSetResult(object obj) => ((TaskCompletionSource)obj).SetResult();
    */

    public void Invoke(Func<Task> work) => Invoke<Func<Task>>(work, StaticFuncTaskWork);
    

    private static Task StaticFuncTaskWork(object arg) => (arg as Func<Task>)();
    
    public void Invoke(object o, Action a) => Invoke(o, StaticActionTaskWork);

    public void Invoke<TScope>(TScope scope, Action<TScope> action)
        => Invoke(scope, (s) => { action(s); return Task.CompletedTask; });

    public void Invoke<TScope>(TScope workState, Func<TScope, Task> work)
    {
        if (IsRunning == false && IsDrainingOrDrained)
        {
            return;
        }

        if (Thread.CurrentThread == Thread)
        {
            var workItem = new SynchronizedEvent<TScope>();
            workItem.WorkState = workState;
            workItem.AsyncWork = work;
            workItem.Run();
            if (workItem.IsFinished == false)
            {
                pendingWorkItems.Add(workItem);
            }
            else if (workItem.IsFailed)
            {
                var handling = HandleWorkItemException(workItem.Exception, workItem);
                if (handling == EventLoopExceptionHandling.Throw)
                {
                    throw new AggregateException(workItem.Exception);
                }
                else if (handling == EventLoopExceptionHandling.Stop)
                {
                    return;
                }
                else if (handling == EventLoopExceptionHandling.Swallow)
                {
                    // swallow
                }
            }
        }
        else
        {
            lock (workQueue)
            {
                var workItem = new SynchronizedEvent<TScope>();
                workItem.WorkState = workState;
                workItem.AsyncWork = work;
                workQueue.Add(workItem);
            }
        }
    }
    

    private static Task StaticActionTaskWork(object arg)
    {
        (arg as Action)();
        return Task.CompletedTask;
    }

    public void Invoke(object workState, Func<object,Task> work)
    {
        if (IsRunning == false && IsDrainingOrDrained)
        {
            return;
        }

        if (Thread.CurrentThread == Thread)
        {
            var workItem = pool.Get();
            workItem.WorkState = workState;
            workItem.AsyncWork = work;
            workItem.Run();
            if (workItem.IsFinished == false)
            {
                pendingWorkItems.Add(workItem);
            }
            else if(workItem.IsFailed)
            {
                var handling = HandleWorkItemException(workItem.Exception, workItem);
                if (handling == EventLoopExceptionHandling.Throw)
                {
                    throw new AggregateException(workItem.Exception);
                }
                else if (handling == EventLoopExceptionHandling.Stop)
                {
                    return;
                }
                else if (handling == EventLoopExceptionHandling.Swallow)
                {
                    // swallow
                }
                pool.Return(workItem);
            }
            else
            {
                pool.Return(workItem);
            }
        }
        else
        {
            lock (workQueue)
            {
                var workItem = pool.Get();
                workItem.WorkState = workState; 
                workItem.AsyncWork = work;
                workQueue.Add(workItem);
            }
        }
    }

    private EventLoopExceptionHandling HandleWorkItemException(Exception ex, SynchronizedEventBase workItem)
    {
        var cleaned = ex.Clean();

        if(cleaned.Count == 1 && cleaned[0] is StopLoopException)
        {
            stopRequested = true;
            pendingWorkItems.Clear();
            workQueue.Clear();
            return EventLoopExceptionHandling.Stop;
        }

        if (IsDrainingOrDrained) return EventLoopExceptionHandling.Swallow;
        return EventLoopExceptionHandling.Throw;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        startOfCycle?.TryDispose();
        startOfCycle = null;
        endOfCycle?.TryDispose();
        endOfCycle = null;
        loopStarted?.TryDispose();
        loopStarted = null;
        loopStopped?.TryDispose();
        loopStopped = null;
    }
}
