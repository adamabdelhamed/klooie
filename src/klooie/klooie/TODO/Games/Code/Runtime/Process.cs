namespace klooie.Gaming.Code;

public class Process
{
    public const int LineNumberWidth = 3;
    public static Process Current { get; private set; }
    public Heap Heap { get; private set; }

    public AST AST { get; private set; }

    public List<ThreadRuntime> Threads { get; private set; } = new List<ThreadRuntime>();

    public Process(ILifetimeManager lt, AST ast)
    {
        Heap = new Heap(lt);
        if (Current != null) throw new NotSupportedException("There can only be one Process at a time");
        Current = this;
        lt.OnDisposed(() => Current = null);

        this.AST = ast;
        InitializeCodeFunctions(lt);
    }

    private void InitializeCodeFunctions(ILifetimeManager lt)
    {
        RegisterFunction("Sleep", async (statement, parameters) =>
        {
            var duration = int.Parse("" + parameters[0]);
            await Game.Current.Delay(duration);
        }, lt);
    }

    public void RegisterFunction(string name, FunctionImplementation impl, ILifetimeManager lt = null)
    {
        lt = lt ?? Game.Current;
        var key = name + "()";
        Heap.Current.Set(impl, key);
        lt.OnDisposed(() => Heap.Current?.Set<object>(null, key));
    }

    public void RegisterStatementHandler(string statementText, StatementImplementation impl, ILifetimeManager lt = null)
    {
        lt = lt ?? Game.Current;
        var key = RunningCodeStatement.FormatStatementImplementationKey(statementText);
        Heap.Current.Set(impl, key);
        lt.OnDisposed(() => Heap.Current?.Set<object>(null, key));
    }

    public List<CodeControl> RenderCode(Block root, bool lineNumbers, float leftOfDocument, float topOfDocument)
    {
        var elements = CreateCodeElements(root, leftOfDocument, topOfDocument);

        foreach (var element in elements)
        {
            Game.Current.GamePanel.Controls.Add(element);
        }

        if (lineNumbers)
        {
            var topElement = elements.Select(e => e.Top).Min();
            var linesNeeded = (elements.Select(e => e.Top).Max()+1) - elements.Select(e => e.Top).Min();
            for (var i = 0; i < linesNeeded; i++)
            {
                var lineElement = Game.Current.GamePanel.Add(new LineNumberControl(i + 1));
                lineElement.MoveTo(leftOfDocument, topElement + i);
            }
        }
        return elements;
    }

    private List<CodeControl> CreateCodeElements(Block root, float leftOfDocument, float topOfDocument)
    {
        var directives = new List<Directive>();
        var codeControls = new List<CodeControl>();
        var topLine = int.MaxValue;

        root.Visit((s) =>
        {
            var topInStatement = s.Tokens.None() ? int.MaxValue : s.Tokens.Select(t => t.Line).Min();
            topLine = Math.Min(topLine, topInStatement);
            return false;
        });

        root.Visit((s) =>
        {
            var firstOnLine = true;
            CodeControl lastOnLine = null;
            foreach (var token in s.Tokens.Where(token => string.IsNullOrWhiteSpace(token.Value) == false))
            {
                var placement = GetPlacement(token, leftOfDocument, topOfDocument, token.Line-topLine);
                var element = new CodeControl(token);

                if (firstOnLine)
                {
                    firstOnLine = false;
                }
                else
                {
                    lastOnLine = element;
                }

                element.Bounds = placement;
                codeControls.Add(element);
            }

            if (s is Directive)
            {
                directives.Add(s as Directive);
            }

            return false;
        });

        for (var y = Game.Current.GameBounds.Top; y < Game.Current.GameBounds.Bottom; y++)
        {
            var nonDirectiveCodeOnLine = codeControls.Where(c => c.Token.Statement is Directive == false && ConsoleMath.Round(c.Top) == y).ToArray();
            var directiveCodeOnLine = codeControls.Where(c => c.Token.Statement is Directive == true && ConsoleMath.Round(c.Top) == y).ToArray();
            if (directiveCodeOnLine.Length > 0 && nonDirectiveCodeOnLine.Length == 0)
            {
                foreach (var element in codeControls.Where(c => ConsoleMath.Round(c.Top) > y).OrderBy(c => c.Top))
                {
                    element.MoveBy(0, -1);
                }
                y--;
            }

            foreach (var directiveCode in directiveCodeOnLine)
            {
                codeControls.Remove(directiveCode);
                directiveCode.Dispose();
            }
        }
        
        return codeControls;
    }

    private RectF GetPlacement(CodeToken token, float leftOfDocument, float topOfDocument, int y)
    {
        var h = 1f;
        var w = token.Value.Length;
        var placement = new RectF(leftOfDocument + LineNumberWidth + (token.Column - 1), topOfDocument + (y - 1), w, h);
        placement = placement.ToSameWithWiggleRoom();
        return placement;
    }
}

