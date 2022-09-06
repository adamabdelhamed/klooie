namespace klooie.Gaming.Code;
public class Function : CodeBlock
{
    public bool CanRun { get; set; } = true;
    public List<Parameter> Parameters { get; private set; } = new List<Parameter>();
    public ExternalEndpointElement Source { get; set; }

    public Lifetime StartThread(string localGroupId = null)
    {
        if (CanRun == false)
        {
            throw new System.Exception("Can't run this function");
        }

        var rt = new ThreadRuntime(new ThreadFunctionOptions()
        {
            EntryPoint = this,
            LocalGroupId = localGroupId,
            Source = Source,
            InitialDestination = CodeControl.CodeElements.Where(c => c.Token?.Statement == this).OrderByDescending(c => c.Left + c.Width).First()
        });
        Process.Current.Threads.Add(rt);
        return rt;
    }
}

public class Parameter
{
    public string Name { get; set; }
}
