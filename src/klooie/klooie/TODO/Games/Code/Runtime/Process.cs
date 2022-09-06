using PowerArgs;

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

    public void RenderCode(Block root, bool lineNumbers, float leftOfDocument, float topOfDocument)
    {
        var elements = CreateCodeElements(root, leftOfDocument, topOfDocument);

        foreach (var element in elements)
        {
            Game.Current.GamePanel.Controls.Add(element);
        }

        if (lineNumbers)
        {
            var topElement = elements.Select(e => e.Top()).Min();
            var linesNeeded = (elements.Select(e => e.Top()).Max()+1) - elements.Select(e => e.Top()).Min();
            for (var i = 0; i < linesNeeded; i++)
            {
                var lineElement = Game.Current.GamePanel.Add(new LineNumberControl(i + 1));
                lineElement.MoveTo(leftOfDocument, topElement + i);
            }
        }
    }

    private List<CodeControl> CreateCodeElements(Block root, float leftOfDocument, float topOfDocument)
    {
        var directives = new List<Directive>();
        var codeControls = new List<CodeControl>();

        var manualPlacements = new Dictionary<Function, MoveFunctionDirective>();
        var manualTopLefts = new Dictionary<Function, RectF>();
        var manuallyPlaced = new HashSet<ConsoleControl>();
        foreach (var moveFunctionDirective in Game.Current.Rules.WhereAs<MoveFunctionDirective>())
        {
            if (moveFunctionDirective.If != null && moveFunctionDirective.If.BooleanValue == false)
            {
                continue;
            }

            manualPlacements.Add(moveFunctionDirective.Function, moveFunctionDirective);
            var topLeftToken = moveFunctionDirective.Function.Tokens.OrderBy(t => t.Line).ThenBy(t => t.Column).First();
            manualTopLefts.Add(moveFunctionDirective.Function, Place(topLeftToken, leftOfDocument, topOfDocument));
        }

        root.Visit((s) =>
        {
            var firstOnLine = true;
            CodeControl lastOnLine = null;
            foreach (var token in s.Tokens.Where(token => string.IsNullOrWhiteSpace(token.Value) == false))
            {
                var placement = Place(token, leftOfDocument, topOfDocument);

                var element = new CodeControl(token);
                if (AST.TryGetFunction(token, out Function function) && manualPlacements.TryGetValue(function, out MoveFunctionDirective moveDirective))
                {
                    var origin = manualTopLefts[function];
                    var dx = placement.Left - origin.Left;
                    var dy = placement.Top - origin.Top;
                    placement = new RectF(moveDirective.Left.FloatValue + dx, moveDirective.Top.FloatValue + dy, placement.Width, placement.Height);
                    manuallyPlaced.Add(element);
                }



                if (firstOnLine)
                {
                    firstOnLine = false;
                }
                else
                {
                    lastOnLine = element;
                }

                element.X = ConsoleMath.Round(placement.Left);
                element.Y = ConsoleMath.Round(placement.Top);
                codeControls.Add(element);
            }

            if (s is Directive)
            {
                directives.Add(s as Directive);
            }

            return false;
        });

        for (var i = 0; i < Game.Current.GamePanel.Height + 50; i++)
        {
            var nonDirectiveCodeOnLine = codeControls.Where(c => c.Token.Statement is Directive == false && (int)ConsoleMath.Round(c.Top) == i).ToArray();
            var directiveCodeOnLine = codeControls.Where(c => c.Token.Statement is Directive == true && (int)ConsoleMath.Round(c.Top) == i).ToArray();
            if (directiveCodeOnLine.Length > 0 && nonDirectiveCodeOnLine.Length == 0)
            {
                foreach (var element in codeControls.Where(c => ConsoleMath.Round(c.Top) > i).OrderBy(c => c.Top))
                {
                    if (manuallyPlaced.Contains(element) == false)
                    {
                        element.MoveBy(0, -1);
                    }
                }
                i--;
            }

            foreach (var directiveCode in directiveCodeOnLine)
            {
                codeControls.Remove(directiveCode);
                directiveCode.Dispose();
            }
        }
        var shifts = directives.WhereAs<ShiftCodeDirective>().ToList();
        if (shifts.Count > 0)
        {
            foreach (var codeControl in codeControls)
            {
                foreach (var shift in shifts)
                {
                    codeControl.MoveBy(0, shift.Amount);
                }
            }
        }

        return codeControls;
    }

    private RectF Place(CodeToken token, float leftOfDocument, float topOfDocument)
    {
        var h = .8f;
        var w = token.Value.Length - .2f;
        var hPad = .1f;
        var vPad = (1 - h) / 2;
        var placement = new RectF(leftOfDocument + LineNumberWidth + (token.Column - 1), topOfDocument + (token.Line - 1), w, h);
        return placement;
    }
}

