namespace klooie.Gaming.Code;
public class LocalDirective : Directive
{
    [ArgRequired]
    public DynamicArg Group { get; set; }
    [ArgRequired]
    public DynamicArg Key { get; set; }
    [ArgRequired]
    public DynamicArg Value { get; set; }

    public static void InitializeGroup(string group, TimeThread thread = null)
    {
        thread = thread ?? TimeThread.Current;
        foreach (var local in Game.Current.Rules.WhereAs<LocalDirective>().Where(l => l.Group.StringValue == group))
        {
            thread.Set(local.Key.StringValue, local.Value.IsNumber ? local.Value.FloatValue : local.Value.ObjectValue);
        }
    }

    public static Dictionary<string, object> Load(string group)
    {
        var ret = new Dictionary<string, object>();
        foreach (var local in Game.Current.Rules.WhereAs<LocalDirective>().Where(l => l.Group.StringValue == group))
        {
            ret.Add(local.Key.StringValue, local.Value.IsNumber ? local.Value.FloatValue : local.Value.ObjectValue);
        }
        return ret;
    }
}
