using System.Text.RegularExpressions;

namespace klooie.Gaming.Code;
[ArgReviverType]
public class DynamicArg
{

    public static string WeaponDisplayName<T>() where T : Weapon => WeaponDisplayName(typeof(T));

    public static string WeaponDisplayName(Type t) => WeaponDisplayName(t.Name);

    public static string WeaponDisplayName(string typeName) => GetDisplayName(typeName);


    public static string AbilityDisplayName<T>() where T : IAbility => AbilityDisplayName(typeof(T));

    public static string AbilityConsoleName(Type t) => AbilityDisplayName(t).Replace(" ", "");

    public static string AbilityDisplayName(Type t) => AbilityDisplayName(t.Name);

    public static string AbilityDisplayName(string typeName) => GetDisplayName(typeName);

    public static string UsableItemDisplayName<T>() where T : UsableItem => UsableItemDisplayName(typeof(T));

    public static string UsableItemDisplayName(Type t) => UsableItemDisplayName(t.Name);

    public static string UsableItemDisplayName(string typeName) => GetDisplayName(typeName);


    private static Dictionary<string, string> displayNameMemo = new Dictionary<string, string>();
    private static string GetDisplayName(string typeName)
    {
        if (displayNameMemo.TryGetValue(typeName, out string ret) == false)
        {
            ret = FromVariable(typeName + "DisplayName").StringValue;
            if (ret == "false")
            {
                ret = typeName;
            }
            displayNameMemo.Add(typeName, ret);
        }
        return ret;
    }

    public string Argument { get; set; }
    [ArgReviver]
    public static DynamicArg Revive(string key, string val) => FromObject(val);
    public ConsoleString ConsoleStringValue => (ConsoleString)Resolve();
    public object ObjectValue => Resolve();
    public string StringValue
    {
        get
        {
            var val = Resolve();

            if (val is bool)
            {
                return val.ToString().ToLower();
            }
            else
            {
                var ret = val.ToString();
                if (ret == "True" || ret == "False") ret = ret.ToLower();
                return ret;
            }

        }
    }

    public override string ToString() => Argument;

    public float FloatValue
    {
        get
        {
            var resolved = Resolve();

            if (resolved == null || resolved + "" == "false")
            {
                return 0;
            }
            else
            {
                return (float)ArgRevivers.Revive(typeof(float), "", "" + resolved);
            }
        }
    }
    public int IntValue => int.Parse("" + Resolve());
    public bool IsNumber => float.TryParse("" + Resolve(), out float ignored);

    public static DynamicArg FromObject(object o) => new DynamicArg() { Argument = "" + o };

    public static DynamicArg FromVariable(string variableName) => new DynamicArg() { Argument = "{{" + variableName + "}}" };


    public T EnumValue<T>()
    {
        return string.IsNullOrWhiteSpace(StringValue) ? default(T) : (T)Enum.Parse(typeof(T), StringValue);
    }

    public List<string> ListValue => StringValue.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

    public bool BooleanValue => StringValue == null ? false : Evaluator.EvaluateBooleanExpression(StringValue?.ToLower());

    public void Invalidate()
    {
        cached = null;
    }

    private object cached;
    private object Resolve()
    {
        if (TimeThread.Current != null)
        {
            Invalidate();
        }
        cached = cached ?? TimeThread.ResolveStatic(Argument);
        return cached;
    }

    private static Regex bigLevelRgex = new Regex(@"^.+-(?<X>\d+)-(?<Y>\d+)$");

}

public static class DynamicArgEx
{
    public static DynamicArg ToDynamicArg(this object o) => DynamicArg.FromObject(o);
}

[ArgReviverType]
public class DisabledUntilArgument
{
    private bool isEnabled;
    private bool isListenerRegistered;
    public string Argument { get; private set; }

    [ArgReviver]
    public static DisabledUntilArgument Revive(string key, string val) => new DisabledUntilArgument() { Argument = val };

    public bool AllowExecute()
    {
        if (Argument != null && isListenerRegistered == false)
        {
            isListenerRegistered = true;
            Game.Current.Subscribe(Argument, ev => isEnabled = true, Game.Current);
        }

        if (Argument == null)
        {
            isEnabled = true;
        }

        return isEnabled;
    }
}