using System.Text;

namespace klooie.Gaming.Code;
public class CompilerOptions
{
    public string Code { get; set; }
    public string CodeLocation { get; set; }
    public ILifetimeManager Lifetime { get; set; }
    public IScriptProvider? ScriptProvider { get; set; }
}

public interface IScriptProvider
{
    public string LoadScriptById(string id);
}

public static class Compiler
{
    private static Dictionary<string, string> ScriptMemo = new Dictionary<string, string>();

    public static AST Compile(CompilerOptions options)
    {
        var ast = new AST();
        var builder = new StringBuilder();
        RenderLines(GetCleanLines(options.Code), options, builder);
        ast.Tokens = Tokenizer.Tokenize(builder.ToString(), options.CodeLocation);
        Parser.Parse(ast.Tokens);
        SemanticAnalyzer.BuildTree(ast);
        return ast;
    }

    private static void RenderLines(IEnumerable<string> lines, CompilerOptions options, StringBuilder builder)
    {
        foreach (var line in lines)
        {
            var wasScript = false;
            if (line.Trim().StartsWith("//#script", StringComparison.OrdinalIgnoreCase))
            {
                wasScript = TryLoadScript(line, options, builder);
            }

            if (wasScript == false)
            {
                builder.AppendLine(line);
            }
        }
    }

    public static IEnumerable<string> GetCleanLines(string s)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\n')
            {
                yield return builder.ToString();
                builder.Clear();
            }
            else if (c == '\r')
            {
                yield return builder.ToString();
                builder.Clear();
                if (i < s.Length - 1 && s[i + 1] == '\n')
                {
                    i++;
                }
            }
            else
            {
                builder.Append(c);
            }
        }
    }

    private static bool TryLoadScript(string line, CompilerOptions options, StringBuilder builder)
    {
        if (options.ScriptProvider == null) return false;

        var tokens = Tokenizer.Tokenize(line, options.CodeLocation);
        var d = DirectiveHydrator.Hydrate(tokens);
        if (d is ScriptDirective)
        {
            var id = (d as ScriptDirective).Id.StringValue;

            if (ScriptMemo.TryGetValue(id, out string cached))
            {
                builder.Append(cached);
                return true;
            }
            var tempBuilder = new StringBuilder();
            var codeToInsert = options.ScriptProvider.LoadScriptById(id);
            var codeToInsertLines = GetCleanLines(codeToInsert);
            RenderLines(codeToInsertLines, options, tempBuilder);
            var finalScriptText = tempBuilder.ToString();
            builder.Append(finalScriptText);
            ScriptMemo.Add(id, finalScriptText);
            return true;
        }
        return false;
    }
}
