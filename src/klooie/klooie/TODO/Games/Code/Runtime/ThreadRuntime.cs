using PowerArgs;
namespace klooie.Gaming.Code;

public class ThreadFunctionOptions
{
    public Function EntryPoint { get; set; }
    public CodeControl InitialDestination { get; set; }
    public string LocalGroupId { get; set; }
}

public class ThreadRuntime : Lifetime
{
    public static Event OnThreadStart { get; private set; } = new Event();
    public ThreadFunctionOptions Options { get; private set; }
    public TimeThread Thread { get; private set; }
    public ThreadRuntime(ThreadFunctionOptions options)
    {
        this.Options = options;

        Thread = new TimeThread(new TimeThreadOptions()
        {
            AST = options.EntryPoint.AST,
            EntryPoint = options.EntryPoint,
        });

        if (options.LocalGroupId != null)
        {
            LocalDirective.InitializeGroup(options.LocalGroupId, Thread);
        }

        OnDisposed(() => Thread.TryDispose());

        Game.Current.Invoke(async () =>
        {
            while (this.IsExpired == false)
            {
                await ExecuteAsync();
                TryDispose();
            }
        });
    }
    private async Task ExecuteAsync()
    {
        OnThreadStart.Fire();
        
        var returnValue = "$".ToGreen();
        IStatement returnStatement = null;
        var openBlockPaths = new List<string>();
        ThrowResult throwResult = null;
        while (true)
        {
            var noop = false;
            if (IsExpired) return;
            var threadResult = Thread.Execute();
            if (IsExpired) return;

            if (threadResult is ThreadFinishedResult || Thread.IsExpired)
            {
                Dispose();
                break;
            }
            else if (threadResult is ReturnStatementResult)
            {
                await HandleRunningStatementAsync((threadResult as ReturnStatementResult).Statement);
                if (IsExpired) return;
                if (Thread.TryResolve(ReturnDirective.ReturnValueKey, out ConsoleString ret))
                {
                    returnValue = ret;
                }
                returnStatement = (threadResult as ReturnStatementResult).Statement;
                break;
            }
            else if (threadResult is ThrowResult)
            {
                if (FunctionSoundsDirective.IsSoundEnabled(Options.EntryPoint))
                {
                    Game.Current.Publish("ThrowStatementStart");
                }
                throwResult = threadResult as ThrowResult;
                var runningCodeStatement = throwResult.Statement;
                var message = throwResult.Message ?? "Unhandled exception";
                await HandleRunningStatementAsync(runningCodeStatement, message);
                if (IsExpired) return;
                break;
            }
            else if (threadResult is RunningCodeExecutionResult)
            {
                var runningCodeStatement = (threadResult as RunningCodeExecutionResult).Statement;
                await HandleRunningStatementAsync(runningCodeStatement);
                if (IsExpired) return;
            }
            else if (threadResult is BlockEnteredExecutionResult)
            {
                var block = (threadResult as BlockEnteredExecutionResult).Block;

                if (block is If)
                {
                    var preCurlyTokens = (block as If).GetTokensFromIfToOpenCurlyTrimmed();
                    await ActivateThenDeactivateAsync(preCurlyTokens.ToList());
                    if (IsExpired) return;
                    await ShortCPUDelay();
                    if (IsExpired) return;
                }

                openBlockPaths.Add(block.Path);
                await ActivateThenDeactivateAsync(new List<CodeToken>() { block.OpenCurly });
                if (IsExpired) return;
            }
            else if (threadResult is BlockExitedExecutionResult)
            {
                var block = (threadResult as BlockExitedExecutionResult).Block;

                if (openBlockPaths.Contains(block.Path) == false)
                {
                    if (block is If)
                    {
                        var preCurlyTokens = (block as If).GetTokensFromIfToOpenCurlyTrimmed();
                        await CPUDelay();
                        if (IsExpired) return;
                        await ActivateThenDeactivateAsync(preCurlyTokens.ToList());
                        if (IsExpired) return;
                    }
                    await ShortCPUDelay();
                    continue;
                }
                else
                {
                    openBlockPaths.Remove(block.Path);
                }

                var matchingItem = CodeControl.CodeElements
                    .Where(c => c is MaliciousCodeElement == false)
                    .Where(c => c.Token == block.CloseCurly).SingleOrDefault();

                await CPUDelay();
                if (IsExpired) return;
                await ActivateThenDeactivateAsync(new List<CodeToken>() { block.CloseCurly });
                if (IsExpired) return;
            }
            else if (threadResult is LoopIteratedExecutionResult)
            {
                var loop = (threadResult as LoopIteratedExecutionResult).Loop;
                var firstStop = CodeControl.CompiledCodeElements.Where(c => c.Token == loop.CloseCurly).SingleOrDefault();
                if (firstStop == null) return;

                // Activate the closed curly brace of the loop then delay
                await ActivateThenDeactivateAsync(new List<CodeToken>() { loop.CloseCurly });
                if (IsExpired) return;
                await CPUDelay();
                if (IsExpired) return;

                // Activate the loop evaluation statement (e.g. for, while, if)
                var loopEvalStatementIndex = loop.Parent.Statements.IndexOf(loop) - 1;
                var runningCodeStatement = loop.Parent.Statements[loopEvalStatementIndex];
                await HandleRunningStatementAsync(runningCodeStatement);
                if (IsExpired) return;
                await CPUDelay();
                if (IsExpired) return;

                // Activate the open curly
                await ActivateThenDeactivateAsync(new List<CodeToken>() { loop.OpenCurly });
                if (IsExpired) return;
            }
            else
            {
                noop = true;
            }

            if (!noop)
            {
                await ShortCPUDelay();
                if (IsExpired) return;
            }
        }
    }

    private async Task HandleRunningStatementAsync(IStatement statement, string throwMessage = null)
    {
        var lineElement = ActivateStatement(statement.Tokens, throwMessage);
        await CPUDelay();
        if (throwMessage != null)
        {
            await Game.Current.Delay(1000);
        }
        if (IsExpired) return;
        if (statement is RunningCodeStatement && (statement as RunningCodeStatement).AsyncInfo != null)
        {
            if (FunctionSoundsDirective.IsSoundEnabled(Options.EntryPoint))
            {
                Game.Current.Publish("AsyncStatementStart");
            }
            var asyncInfo = (statement as RunningCodeStatement).AsyncInfo;
            using (var asyncLifetime = Game.Current.CreateChildLifetime())
            {
                var matchingItem = CodeControl.CompiledCodeElements
                    .OrderBy(c => c.Token.Line == statement.Tokens.First().Line ? 0 : 1).First();

                var runningCodeStatement = statement as RunningCodeStatement;
                var lastTokenElement = CodeControl.CodeElements
                    .Where(c => runningCodeStatement.Tokens.Contains(c.Token))
                    .OrderByDescending(c => c.Left)
                    .First();

                lineElement.LineOfCode = lineElement.LineOfCode.StringValue.ToConsoleString(ActiveLineElement.AwaitForegroundColor, ActiveLineElement.AwaitBackgroundColor);

               

                ConsoleString outputData = null, returnData = null;
                SetResolutionContext(() =>
                {
                    outputData = asyncInfo.OutgoingData.ConsoleStringValue;
                    returnData = asyncInfo.ReturnData == null ? outputData : asyncInfo.ReturnData.ConsoleStringValue;
                });

                var outputString = outputData is ICanBeAConsoleString ? (outputData as ICanBeAConsoleString).ToConsoleString() : outputData.ToString().ToCyan();
                var returnString = returnData is ICanBeAConsoleString ? (returnData as ICanBeAConsoleString).ToConsoleString() : returnData.ToString().ToCyan();

                await Game.Current.Delay(TimeSpan.FromMilliseconds(asyncInfo.AsyncDuration + WireLatency()));
                if (IsExpired) return;
                if (runningCodeStatement.AsyncInfo.Log)
                {
                    SetResolutionContext(() =>
                    {
                        //Game.Current.WriteLine(runningCodeStatement.AsyncInfo.OutgoingData.ConsoleStringValue, true, false);
                    });
                }
                await Game.Current.Delay(asyncInfo.Latency);
                if (IsExpired) return;
                if (FunctionSoundsDirective.IsSoundEnabled(Options.EntryPoint))
                {
                    Game.Current.Publish("AsyncStatementReturn");
                }
                await Game.Current.Delay(TimeSpan.FromMilliseconds(asyncInfo.AsyncDuration + WireLatency()));
                if (IsExpired) return;

                if (runningCodeStatement.AsyncInfo.Log)
                {
                    SetResolutionContext(() =>
                    {
                        //Game.Current.WriteLine(runningCodeStatement.AsyncInfo.ReturnData.ConsoleStringValue, true, false);
                    });
                }

                lineElement.LineOfCode = lineElement.LineOfCode.StringValue.ToConsoleString(ActiveLineElement.ActiveForegroundColor, ActiveLineElement.ActiveBackgroundColor);
                await ShortCPUDelay();
                if (IsExpired) return;
            }
        }
        else if (Thread.AsyncTask != null)
        {
            await Thread.AsyncTask;
            Thread.AsyncTask = null;
        }
        if (IsExpired) return;
        lineElement.Dispose();
    }

    private async Task ActivateThenDeactivateAsync(List<CodeToken> tokens)
    {
        var lineElement = ActivateStatement(tokens);
        await CPUDelay();
        if (IsExpired) return;
        lineElement.TryDispose();
    }

    private ActiveLineElement ActivateStatement(List<CodeToken> tokens, string throwMessage = null)
    {
        if (FunctionSoundsDirective.IsSoundEnabled(Options.EntryPoint))
        {
            Game.Current.Publish("StatementActivated");
        }
        var anchor = CodeControl.CodeElements
            .Where(c => tokens.Contains(c.Token))
            .OrderBy(c => c.Token.Column).FirstOrDefault();

        var lineElement = throwMessage == null ? Game.Current.GamePanel.Add(new ActiveLineElement(tokens, this)) :
            Game.Current.GamePanel.Add(new ActiveLineElement(tokens, throwMessage, this));
        OnDisposed(lineElement.Dispose);

        if (anchor != null)
        {
            lineElement.MoveTo(anchor.Left, anchor.Top);
        }
        else
        {
            lineElement.ResizeTo(0, 0);
        }
        Game.Current.Invoke(async () =>
        {
            while (lineElement.IsExpired == false)
            {
                CodeControl.CodeElements
                    .Where(c => tokens.Contains(c.Token))
                    .OrderBy(c => c.Token.Column).FirstOrDefault();

                if (anchor != null && anchor.Top != lineElement.Top)
                {
                    lineElement.MoveTo(lineElement.Left, anchor.Top);
                }
                await Task.Yield();
            }
        });

        return lineElement;
    }

    private async Task CPUDelay()
    {
        if (this.Thread.TryResolve<float>("CPUDelay", out float delay) == false)
        {
            delay = 250;
        }
        await Game.Current.Delay(delay);
    }

    private async Task ShortCPUDelay()
    {
        if (this.Thread.TryResolve<float>("ShortCPUDelay", out float delay) == false)
        {
            delay = 150;
        }
        await Game.Current.Delay(delay);
    }

    private float WireLatency()
    {
        if (this.Thread.TryResolve<float>("WireLatency", out float delay) == false)
        {
            delay = 400;
        }
        return delay;
    }

    public void SetResolutionContext(Action resolveAction)
    {
        var oldCurrent = TimeThread.Current;
        try
        {
            TimeThread.Current = Thread;
            resolveAction();
        }
        finally
        {
            TimeThread.Current = oldCurrent;
        }
    }
}