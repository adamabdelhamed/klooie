using PowerArgs;
using System.Reflection;

namespace klooie.Gaming.Code;
public class DirectiveExceptionData
{
    public int Line { get; set; }
    public int Column { get; set; }
    public Exception Exception { get; set; }
    public string[] CMD { get; set; }
}

public static class DirectiveHydrator
{
    private static Dictionary<string, Type> _directiveTypeCache;
    private static Dictionary<string, Type> DirectiveTypeCache
    {
        get
        {
            if (_directiveTypeCache == null)
            {
                _directiveTypeCache = new Dictionary<string, Type>();
                foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(Directive))))
                {
                    _directiveTypeCache.Add(t.Name.ToLower(), t);
                }

                foreach (var t in Assembly.GetEntryAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(Directive))))
                {
                    _directiveTypeCache.Add(t.Name.ToLower(), t);
                }
            }
            return _directiveTypeCache;
        }
    }

    public static Directive Hydrate(List<CodeToken> directiveTokens)
    {
        var cmd = Commandify(directiveTokens);
        var ret = Hydrate(cmd, directiveTokens.First().Line, directiveTokens.First().Column);
        ret.Tokens.AddRange(directiveTokens);
        return ret;
    }

    public static Directive Hydrate(string cmd, int line = -1, int col = -1) => Hydrate(Commandify(cmd), line, col);

    /*
     * The Hydrate method is perf critical. 
     * 
     * Creating PowerArg definitions can be expensive, but
     * the definitions never change for a given type so we cache and reuse those.
     * 
     * 
     * PowerArgs validation can be expensive. It is disabled in RELEASE mode / non-local. This is because I will have tested all the 
     * production directives prior to shipping. Any case where these validations would throw would be a bug anyway.
     * 
     */

    private static Dictionary<Type, CommandLineArgumentsDefinition> cachedDefs = new Dictionary<Type, CommandLineArgumentsDefinition>();
    private static Directive Hydrate(string[] cmd, int line = -1, int col = -1)
    {
        var directiveName = cmd[0];
        var directiveTypeName = (directiveName + "Directive").ToLower();
        if (DirectiveTypeCache.TryGetValue(directiveTypeName, out Type directiveType))
        {
            if (cachedDefs.TryGetValue(directiveType, out CommandLineArgumentsDefinition def) == false)
            {
                def = new CommandLineArgumentsDefinition(directiveType);
#if DEBUG
#else
                def.ValidationEnabled = false;
#endif
                cachedDefs.Add(directiveType, def);
            }

            var effectiveCmd = cmd.Skip(1).ToArray();
            var ret = (Directive)Args.Parse(def, effectiveCmd).Value;
            return ret;

        }
        else
        {
            var ret = new Directive();
            return ret;
        }
    }

    public static string[] Commandify(List<CodeToken> directiveTokens)
    {
        var reader = new TokenReader<CodeToken>(directiveTokens);

        while (reader.Advance().Value != "#") ;

        var commandLine = "";
        while (reader.CanAdvance())
        {
            commandLine += reader.Advance().Value;
        }

        var ret = Args.Convert(commandLine);
        return ret;
    }

    public static string[] Commandify(string directiveText)
    {
        var start = directiveText.IndexOf("#") + 1;
        var ret = Args.Convert(directiveText.Substring(start));
        return ret;
    }
}
