using System.Text;
namespace klooie.Gaming.Code;

/// <summary>
/// Options for compiling klooie code
/// </summary>
public class CompilerOptions
{
    /// <summary>
    /// The code to compile
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// The name of the source file
    /// </summary>
    public string CodeLocation { get; set; }

    /// <summary>
    /// The script provider to use to load scripts, if your program supports that
    /// </summary>
    public IScriptProvider? ScriptProvider { get; set; }
}

/// <summary>
/// An extensibility point that lets you dynamically load scripts
/// </summary>
public interface IScriptProvider
{
    /// <summary>
    /// Loads the script by id
    /// </summary>
    /// <param name="id">the id of the script to load</param>
    /// <returns>the code for the script</returns>
    public string LoadScriptById(string id);
}

/// <summary>
/// A utility for compiling klooie code
/// </summary>
public static class Compiler
{
    private static Dictionary<string, string> ScriptMemo = new Dictionary<string, string>();

    /// <summary>
    /// runs the compiler
    /// </summary>
    /// <param name="options">the compiler options</param>
    /// <returns>a compiled AST</returns>
    public static AST Compile(CompilerOptions options)
    {
        var ast = new AST();
        var builder = new StringBuilder();
        RenderLines(GetCleanLines(options.Code), options, builder);
        ast.Tokens = KlooieCodeTokenizer.Tokenize(builder.ToString(), options.CodeLocation);
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

    private static IEnumerable<string> GetCleanLines(string s)
    {
        List<string> lines = new List<string>();
        var currentLine = string.Empty;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\n')
            {
                lines.Add(currentLine);
                currentLine = string.Empty;
            }
            else if (c == '\r')
            {
                lines.Add(currentLine);
                currentLine = string.Empty;
                if (i < s.Length - 1 && s[i + 1] == '\n')
                {
                    i++;
                }
            }
            else
            {
                currentLine += c;
            }
        }

        if(string.IsNullOrEmpty(currentLine) == false)
        {
            lines.Add(currentLine);
        }

        return lines;
    }

    private static bool TryLoadScript(string line, CompilerOptions options, StringBuilder builder)
    {
        if (options.ScriptProvider == null) return false;

        var tokens = KlooieCodeTokenizer.Tokenize(line, options.CodeLocation);
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
            var codeToInsertLines = GetCleanLines(codeToInsert).ToArray();
            RenderLines(codeToInsertLines, options, tempBuilder);
            var finalScriptText = tempBuilder.ToString();
            builder.Append(finalScriptText);
            ScriptMemo.Add(id, finalScriptText);
            return true;
        }
        return false;
    }
}
