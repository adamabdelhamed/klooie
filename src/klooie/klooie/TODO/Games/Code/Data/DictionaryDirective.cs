namespace klooie.Gaming.Code;
public class DictionaryDirective : Directive
{
    [ArgRequired]
    [ArgPosition(0)]
    public string VariableName { get; set; }

    [ArgRequired]
    [ArgPosition(1)]
    public string Key { get; set; }

    [ArgRequired]
    [ArgPosition(2)]
    public ConsoleString Value { get; set; }


    public override Task ExecuteAsync()
    {
        if (Game.Current.RuleVariables.TryGetValue(VariableName, out Dictionary<string, ConsoleString> targetStore) == false)
        {
            targetStore = new Dictionary<string, ConsoleString>();
            Game.Current.RuleVariables.Set(targetStore, VariableName);
        }

        if (targetStore.ContainsKey(Key))
        {
            targetStore[Key] = Value;
        }
        else
        {
            targetStore.Add(Key, Value);
        }

        return Task.CompletedTask;
    }
}
