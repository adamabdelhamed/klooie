using PowerArgs;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace klooie.Gaming.Code;
public class TimeThreadOptions
{
    public Event<ConsoleString> Log { get; set; } = new Event<ConsoleString>();
    public AST AST { get; set; }
    public IStatement EntryPoint { get; set; }
}

public class ThreadFinishedResult : StatementExecutionResult
{

}

public class TimeThread : Lifetime
{
    public Task AsyncTask { get; set; }

    public static Event<TimeThread> ThreadStarted { get; private set; } = new Event<TimeThread>();
    private static int NextId = 1;
    [ThreadStatic]
    private static TimeThread _current;
    public static TimeThread Current { get => _current; set => _current = value; }

    private TimeThreadOptions options;

    public int Id { get; private set; }

    public Stack<CallStackFrame> CallStack { get; private set; } = new Stack<CallStackFrame>();


    public TimeThreadOptions Options => options;
    public CallStackFrame CurrentFrame => CallStack.Peek();
    public IStatement CurrentStatement => CurrentFrame.Statement;
    private Dictionary<string, object> locals => CurrentFrame.Locals;
    public Block CurrentBlock => CurrentStatement.Parent;

    public string Log { get; private set; } = string.Empty;

    private bool entered;
    public TimeThread(TimeThreadOptions options)
    {
        this.options = options;
        Id = NextId++;
        CallStack.Push(new CallStackFrame() { Statement = options.EntryPoint });
        ThreadStarted.Fire(this);
    }

    public StatementExecutionResult Execute()
    {
        try
        {
            _current = this;
            if (entered == false)
            {
                entered = true;
                options.Log.Fire(("ENTRY: " + options.EntryPoint.ToString()).ToConsoleString());
                return options.EntryPoint.Execute(this);
            }
            else if (CallStack.Count == 1)
            {
                return new ThreadFinishedResult();
            }
            else
            {
                return CurrentStatement.Execute(this);
            }
        }
        finally
        {
            _current = null;
        }
    }

    public bool IsInScope(string key) => TryResolve(key, out object ignored);

    public void Set(string key, object val)
    {
        foreach (var frame in CallStack)
        {
            if (frame.Locals.ContainsKey(key))
            {
                frame.Locals[key] = val;
                return;
            }
        }

        if (Heap.Current.ContainsKey(key))
        {
            Heap.Current.Set(val, key);
        }
        else
        {
            locals.Add(key, val);
        }
    }

    public static void StaticSet(string key, object value)
    {
        if (Current != null)
        {
            Current.Set(key, value);
        }
        else
        {
            Heap.Current.Set(value, key);
        }
    }

    public T Resolve<T>(string key)
    {
        if (TryResolve(key, out T ret) == false)
        {
            throw new Exception($"Unable to resolve variable '{key}'");
        }
        else
        {
            return ret;
        }
    }


    public static ConsoleString ResolveDynamicStringTemplate(string value)
    {
        var replacements = Regex.Matches(value, "\\{\\{(?<variable>[^}]+)\\}\\}");
        foreach (Match replacement in replacements)
        {
            var variableName = replacement.Groups["variable"].Value;

            object resolved = value;
            if (Current == null || Current.TryResolve(variableName, out resolved) == false)
            {
                resolved = Heap.Current.Get<object>(variableName);
            }

            if (string.IsNullOrWhiteSpace(resolved?.ToString()))
            {
                resolved = resolved is ConsoleString ? (object)"false".ToConsoleString() : (object)"false";
            }

            if (resolved is ConsoleString && replacement.Length == value.Length)
            {
                return resolved as ConsoleString;
            }

            value = value.Replace("{{" + variableName + "}}", resolved + "");
        }
        return ConsoleString.Parse(value);
    }

    public float ResolveNumber(string value) => ResolveNumberStatic(value, this);
    public static float ResolveNumberStatic(string value, TimeThread thread = null) => float.Parse(ResolveStatic(value, thread).ToString());
    public object Resolve(string value) => ResolveStatic(value, this);
    public static object ResolveStatic(string value, TimeThread thread = null)
    {
        thread = thread ?? TimeThread.Current;
        object val = value;

        var resolved = TimeThread.ResolveDynamicStringTemplate(val.ToString());
        if (resolved.StartsWith("{") && resolved.EndsWith("}"))
        {
            val = NestedDirectedResolver.Resolve(resolved.ToString());
        }
        else
        {
            val = resolved;
        }

        if ((val is string || val is ConsoleString) && val.ToString().StartsWith("{") && val.ToString().EndsWith("}"))
        {
            val = NestedDirectedResolver.Resolve(val.ToString());
        }

        return val;
    }

    public bool TryResolve<T>(string key, out T ret, bool localsOnly = false)
    {
        foreach (var frame in CallStack)
        {
            if (frame.Locals.TryGetValue(key, out object localRet))
            {
                ret = (T)localRet;
                return true;
            }
        }

        if (localsOnly == false)
        {
            if (Heap.Current.TryGetValue<T>(key, out T globalRet))
            {
                ret = globalRet;
                return true;
            }
        }

        ret = default(T);
        return false;
    }
}

public class CallStackFrame
{
    public IStatement Statement { get; set; }
    public Dictionary<string, object> Locals { get; private set; } = new Dictionary<string, object>();
}

