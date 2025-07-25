using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using klooie;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public sealed class SynthTweaker : Recyclable, IDisposable
{
    private static class SyntaxHighlightingPalette
    {
        public static readonly RGB Keyword = new RGB(80, 160, 255);
        public static readonly RGB Identifier = RGB.White;
        public static readonly RGB StringLiteral = RGB.Yellow;
        public static readonly RGB NumberLiteral = RGB.Magenta;
        public static readonly RGB Comment = RGB.Green;
        public static readonly RGB Punctuation = RGB.White;
        public static readonly RGB TypeName = RGB.Cyan;
        public static readonly RGB Effect = RGB.Orange;
        public static RGB MethodDeclaration = RGB.White; // Example color
        public static RGB MethodCall = RGB.White;         // Example color
    }

    public static readonly HashSet<string> EffectMethodNames = typeof(SynthPatchExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m => m.ReturnType == typeof(ISynthPatch))
        .Select(m => m.Name)
        .ToHashSet();

    public Event<ConsoleString> CodeChanged { get; private set; } = Event<ConsoleString>.Create();
    public Event<List<PatchFactoryInfo>> PatchesCompiled { get; private set; } = Event<List<PatchFactoryInfo>>.Create();

    private static readonly LazyPool<SynthTweaker> _pool =
        new(() => new SynthTweaker());

    public static SynthTweaker Create()
    {
        var t = _pool.Value.Rent();
        return t;
    }

    // ────────── State ──────────
    private string _path = null!;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private CancellationTokenSource? _playCts;
 

    // ────────── Setup ──────────
    public void Initialize(string path)
    {
        _path = path;

        TryCompileAndPlay();
        var app = ConsoleApp.Current;
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(_path)!, Path.GetFileName(_path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        void OnFileEvent(object? sender, FileSystemEventArgs e)
        {
            // Only react if it's really our file (editors sometimes touch temp files)
            if (!string.Equals(e.FullPath, Path.GetFullPath(_path), StringComparison.OrdinalIgnoreCase))
                return;

            // Debounce (reset timer each event)
            _debounce?.Dispose();
            _debounce = new Timer(_ =>
            {
                // Try to ensure file is really written
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        using (File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { }
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(50);
                    }
                }
                app.Invoke(() =>
                {
                    TryCompileAndPlay();
                });
                
                _debounce!.Dispose();
                _debounce = null;
            }, null, 200, Timeout.Infinite);
        }

        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += (s, e) => OnFileEvent(s, new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(e.FullPath)!, Path.GetFileName(e.FullPath)));
    }

    // ─────── Compilation ───────
    private void TryCompileAndPlay()
    {
        try
        {
            var srcText = File.ReadAllText(_path);

            var syntax = CSharpSyntaxTree.ParseText(srcText,
                new CSharpParseOptions(LanguageVersion.Latest));

            // Reference BCL and klooie
            var refs = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ISynthPatch).Assembly.Location),
            };
            // Add critical BCL assemblies explicitly (no ambiguous Select)
            var bclDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

            var bclDlls = new[]
               {
                "System.Private.CoreLib.dll",
                "System.Runtime.dll",
                "System.Console.dll",
                "System.Linq.dll",
                "System.Linq.Expressions.dll",
                "System.Collections.dll",
                "netstandard.dll"
            };
            refs.AddRange(bclDlls
                .Select(f => Path.Combine(bclDir, f))
                .Where(File.Exists)
                .Select(f => MetadataReference.CreateFromFile(f)));

            var compilation = CSharpCompilation.Create(
                assemblyName: $"Tweaker_{Guid.NewGuid():N}",
                syntaxTrees: new[] { syntax },
                references: refs,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release));

            using var peStream = new MemoryStream();
            using var pdbStream = new MemoryStream();
            var result = compilation.Emit(peStream, pdbStream);

            if (!result.Success)
            {
                var errorString = "";
                foreach (var d in result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning))
                {
                    errorString+= $"{d.Severity}: {d.GetMessage()}";
                }
                CodeChanged.Fire($"Compilation failed:\n{errorString}\n\n{srcText}".ToRed());
                return;
            }

            peStream.Position = 0;
            pdbStream.Position = 0;

            var alc = new AssemblyLoadContext($"TweakerCtx_{Guid.NewGuid():N}", isCollectible: true);
            var asm = alc.LoadFromStream(peStream, pdbStream);

            var factories = FindPatchFactories(asm).ToList();
            if (!factories.Any())
            {
                CodeChanged.Fire($"No static ISynthPatch methods found\n\n{srcText}".ToRed());
                alc.Unload();
                return;
            }

            CodeChanged.Fire(RenderHighlightedSource(srcText));
            PatchesCompiled.Fire(factories);
        }
        catch (Exception ex)
        {
            CodeChanged.Fire(ex.ToString().ToRed());
        }
    }

 
    public class PatchFactoryInfo
    {
        public string Name { get; }
        public Func<ISynthPatch> Factory { get; }
        public PatchFactoryInfo(string Name, Func<ISynthPatch> Factory)
        {
            this.Name = Name ?? throw new ArgumentNullException(nameof(Name));
            this.Factory = Factory ?? throw new ArgumentNullException(nameof(Factory));
        }
        public override string ToString()
        {
            var isCreateFactory = Name.EndsWith(".Create");
            if (isCreateFactory) return Name;

            var lastDot = Name.LastIndexOf('.');
            if(lastDot < 0) return Name;

            // Return just the method name after the last dot
            return Name.Substring(lastDot + 1);
        }
    }

    // Find all public static ISynthPatch methods with zero params
    private static IEnumerable<PatchFactoryInfo> FindPatchFactories(Assembly asm)
    {
        foreach (var t in asm.GetTypes())
        {
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (typeof(ISynthPatch).IsAssignableFrom(m.ReturnType)
                    && m.GetParameters().Length == 0)
                {
                    string name = $"{t.FullName}.{m.Name}";
                    yield return new PatchFactoryInfo(
                        name,
                        () => (ISynthPatch)m.Invoke(null, null)!);
                }
            }
        }
    }

    private ConsoleString RenderHighlightedSource(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        var cs = new ConsoleString();
        foreach (var token in root.DescendantTokens())
        {
            var color = SyntaxHighlightingPalette.Identifier;

            if (token.IsKeyword())
                color = SyntaxHighlightingPalette.Keyword;
            else if (token.IsKind(SyntaxKind.StringLiteralToken) ||
         token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken) ||
         token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)) // just in case
                color = SyntaxHighlightingPalette.StringLiteral;
            else if (token.IsKind(SyntaxKind.NumericLiteralToken))
                color = SyntaxHighlightingPalette.NumberLiteral;
            else if (token.IsKind(SyntaxKind.IdentifierToken))
            {
                // Default: check if it starts with uppercase for type names
                if (char.IsUpper(token.Text.FirstOrDefault()))
                    color = SyntaxHighlightingPalette.TypeName;

                // Check for method declaration
                if (token.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax methodDecl &&
                    methodDecl.Identifier == token)
                {
                    color = SyntaxHighlightingPalette.MethodDeclaration;
                }
                // Check for method call (robust)
                else if (IsMethodCallIdentifier(token))
                {
                    color = EffectMethodNames.Contains(token.ValueText) ? SyntaxHighlightingPalette.Effect : SyntaxHighlightingPalette.MethodCall;
                }
            }
            else if (token.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                     token.IsKind(SyntaxKind.MultiLineCommentTrivia))
                color = SyntaxHighlightingPalette.Comment;
            else if (IsPunctuation(token))
                color = SyntaxHighlightingPalette.Punctuation;

            // Add leading trivia (e.g., comments/whitespace)
            foreach (var trivia in token.LeadingTrivia)
            {
                var triviaColor = SyntaxHighlightingPalette.Comment;
                if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                    triviaColor = color;
                cs += trivia.ToFullString().ToConsoleString(triviaColor);
            }

            // Add token
            cs += token.Text.ToConsoleString(color);

            // Add trailing trivia (e.g., newlines)
            foreach (var trivia in token.TrailingTrivia)
            {
                var triviaColor = SyntaxHighlightingPalette.Comment;
                if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    triviaColor = color;
                cs += trivia.ToFullString().ToConsoleString(triviaColor);
            }
        }

        return cs;
    }


    private static bool IsMethodCallIdentifier(SyntaxToken token)
    {
        var parent = token.Parent;
        if (parent is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax idName)
        {
            var grandparent = idName.Parent;

            // Simple call: DoSomething()
            if (grandparent is Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation &&
                invocation.Expression == idName)
                return true;

            // Member access call: obj.DoSomething()
            if (grandparent is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name == idName &&
                memberAccess.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax memberInvocation &&
                memberInvocation.Expression == memberAccess)
                return true;
        }
        return false;
    }

    private static bool IsPunctuation(SyntaxToken token)
    {
        return token.Kind() switch
        {
            SyntaxKind.OpenBraceToken or
            SyntaxKind.CloseBraceToken or
            SyntaxKind.OpenParenToken or
            SyntaxKind.CloseParenToken or
            SyntaxKind.OpenBracketToken or
            SyntaxKind.CloseBracketToken or
            SyntaxKind.SemicolonToken or
            SyntaxKind.CommaToken or
            SyntaxKind.DotToken => true,
            _ => false
        };
    }


    // ──────── Disposal ────────
    protected override void OnReturn() => Dispose();

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
        _playCts?.Cancel();
        _playCts?.Dispose();
        _path = null!;
    }
}
