using System.Reflection;

namespace klooie.Gaming.Code;

/// <summary>
/// A utility that can hydrate directives at runtime
/// </summary>
public static class DirectiveHydrator
{
    /// <summary>
    /// The set of assemblies to search for directives. You must add to this before
    /// any hydration is performed. By default it will include the klooie assembly
    /// and the program's entry assembly
    /// </summary>
    public static readonly HashSet<Assembly> DirectiveSources = new HashSet<Assembly>()
    {
        Assembly.GetExecutingAssembly(),
        Assembly.GetEntryAssembly(),
    };

    private static Dictionary<string, Type>? _directiveTypeCache;
    private static Dictionary<string, Type> DirectiveTypeCache
    {
        get
        {
            if (_directiveTypeCache == null)
            {
                _directiveTypeCache = new Dictionary<string, Type>();
                foreach (var assembly in DirectiveSources)
                {
                    foreach (var t in assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Directive))))
                    {
                        _directiveTypeCache.Add(t.Name.ToLower(), t);
                    }
                }
            }
            return _directiveTypeCache;
        }
    }

    /*
     * The Hydrate method is perf critical. 
     * 
     * Creating PowerArg definitions can be expensive, but
     * the definitions never change for a given type so we cache and reuse those.
     * 
     */
    private static Dictionary<Type, CommandLineArgumentsDefinition> cachedDefs = new Dictionary<Type, CommandLineArgumentsDefinition>();


    /// <summary>
    /// Hydrates a command that originated in a code document
    /// </summary>
    /// <param name="directiveTokens">the tokens to hydrate</param>
    /// <returns>a hydrated directive</returns>
    public static Directive Hydrate(List<CodeToken> directiveTokens)
    {
        var cmd = Commandify(directiveTokens);
        var ret = Hydrate(cmd, directiveTokens.First().Line, directiveTokens.First().Column);
        ret.Tokens.AddRange(directiveTokens);
        return ret;
    }

    /// <summary>
    /// Hydrates a directive given a command string
    /// </summary>
    /// <param name="cmd">a command string</param>
    /// <param name="line">the line in the original document</param>
    /// <param name="col">the column in the original document</param>
    /// <returns>a hydrated directive</returns>
    public static Directive Hydrate(string cmd, int line = -1, int col = -1) => Hydrate(Commandify(cmd), line, col);

    /// <summary>
    /// Converts the given tokens into a command string
    /// </summary>
    /// <param name="directiveTokens">the tokens</param>
    /// <returns>a command string</returns>
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

    /// <summary>
    /// Converts the directive text into a command string
    /// </summary>
    /// <param name="directiveText">the directive text</param>
    /// <returns>a command string</returns>
    public static string[] Commandify(string directiveText)
    {
        var start = directiveText.IndexOf("#") + 1;
        var ret = Args.Convert(directiveText.Substring(start));
        return ret;
    }

    private static Directive Hydrate(string[] cmd, int line = -1, int col = -1)
    {
        var directiveName = cmd[0];
        var directiveTypeName = (directiveName + "Directive").ToLower();
        if (DirectiveTypeCache.TryGetValue(directiveTypeName, out Type directiveType))
        {
            if (cachedDefs.TryGetValue(directiveType, out CommandLineArgumentsDefinition def) == false)
            {
                def = new CommandLineArgumentsDefinition(directiveType);
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
}
