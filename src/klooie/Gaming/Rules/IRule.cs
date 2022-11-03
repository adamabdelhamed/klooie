namespace klooie.Gaming;

/// <summary>
/// A Rule that can be added to a rule based, event driven game
/// </summary>
public interface IRule
{
    /// <summary>
    /// Executes the rule
    /// </summary>
    /// <returns>a task</returns>
    Task ExecuteAsync();
}

/// <summary>
/// A rule that can be created by a Func<Task>
/// </summary>
public class FuncRule : IRule
{
    private Func<Task> func;

    /// <summary>
    /// Creates a rule
    /// </summary>
    /// <param name="func">the implementation</param>
    /// <returns>a rule</returns>
    public static IRule Create(Func<Task> func) => new FuncRule(func);
    private FuncRule(Func<Task> func) => this.func = func;
    
    /// <summary>
    /// Executes the Func<Task> that was provided
    /// </summary>
    /// <returns></returns>
    public Task ExecuteAsync() => func();
}