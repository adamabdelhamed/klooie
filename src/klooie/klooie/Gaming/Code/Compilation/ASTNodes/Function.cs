namespace klooie.Gaming.Code;
public class Function : CodeBlock
{
    public bool CanExecute { get; set; } = true;
    public List<Parameter> Parameters { get; private set; } = new List<Parameter>();

    public string Name
    {
        get
        {
            for(var i = 0; i < Tokens.Count-1; i++)
            {
                if(Tokens[i+1].Value == "(")
                {
                    return Tokens[i].Value;
                }
            }
            throw new Exception("Could not parse function name");
        }
    }

    public ILifetimeManager Execute(string localGroupId = null)
    {
        if (CanExecute == false) throw new Exception("Can't run this function");

        var rt = new ThreadRuntime(new ThreadFunctionOptions() { EntryPoint = this, LocalGroupId = localGroupId });
        Process.Current.Threads.Add(rt);
        return rt;
    }
}

public class Parameter
{
    public string Name { get; set; }
}
