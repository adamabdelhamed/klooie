using PowerArgs;

namespace klooie.Gaming.Code;
internal static class SemanticAnalyzer
{
    private class Context
    {
        public AST AST { get; set; }
        public TokenReader<CodeToken> Reader { get; set; }
        public int NextBlockId { get; set; }
        public Stack<Block> BlockStack { get; private set; } = new Stack<Block>();
        public Block CurrentBlock => BlockStack.Count == 0 ? null : BlockStack.Peek();
    }

    private static readonly string[] controlFlowStatements = new string[] { "return", "break", "continue", };
    private static readonly string[] loopIndicators = new string[] { "for", "while", };


    public static void BuildTree(AST ast)
    {
        var context = new Context()
        {
            AST = ast,
            Reader = new TokenReader<CodeToken>(ast.Tokens),
        };

        ast.Root = new NoOpBlock() { AST = ast };
        context.BlockStack.Push(ast.Root);
        OnOpen(context);

        // HACK - because I don't want to walk backwards since
        // my stupid analyzer doesn't detect functions until it
        // sees the open curly brace of the body. By then its
        // too late to capture the signature that preceeded the body.
        // So this hack looks at the statement above the curly brace,
        // removes it, and adds its tokens to the function
        foreach (var function in ast.Functions.Where(f => f.GetType() == typeof(Function)))
        {
            var i = function.Parent.Statements.IndexOf(function);
            var preceedingStatement = function.Parent.Statements[i - 1];
            function.Parent.Statements.RemoveAt(i - 1);
            function.Tokens.AddRange(preceedingStatement.Tokens);
            function.Tokens = function.Tokens.OrderBy(t => t.Line).ThenBy(t => t.Column).ToList();
            foreach (var token in function.Tokens)
            {
                token.Function = function;
            }
        }

        ast.Root.Visit((s) =>
        {

            if (s is NoOpStatement && s.Tokens.Where(t => string.IsNullOrWhiteSpace(t.Value) == false).None())
            {
                var i = s.Parent.Statements.IndexOf(s);
                s.Parent.Statements.RemoveAt(i);
                return false;
            }

            foreach (var token in s.Tokens)
            {
                token.Statement = s;
            }
            s.AST = ast;
            return false;
        });
    }

    private static void OnOpen(Context context)
    {
        while (context.Reader.CanAdvance())
        {
            var isSpecial = context.Reader.Peek().Type == TokenType.SpecialCharacter;
            var isKeyword = context.Reader.Peek().Type == TokenType.Keyword;
            var isPlainText = context.Reader.Peek().Type == TokenType.PlainText;
            var isFunctionBlock = IsWithinFunction(context);
            var isControlFlowStatement = context.Reader.Peek().Type == TokenType.Keyword &&
                controlFlowStatements.Contains(context.Reader.Peek().Value);

            if (context.Reader.Peek().Value == "{" && isSpecial)
            {
                OnBlockOpen(context);
            }
            else if (context.Reader.Peek().Value == "}" && isSpecial)
            {
                OnBlockClose(context);
            }
            else if (context.Reader.Peek().Type == TokenType.Comment && context.Reader.Peek().Value == "//")
            {
                OnSingleLineComment(context);
            }
            else if (isFunctionBlock && DoesCurrentLineEndWithSemicolon(context))
            {
                OnSingleLineStatement(context);
            }
            else
            {
                OnUnknownCode(context);
            }
        }
    }

    private static void OnBlockOpen(Context context)
    {
        var tokens = new List<CodeToken>();
        tokens.Add(context.Reader.Expect("{"));

        if (IsCurrentOpenCurlyFromLoop(context))
        {
            var toUpgrade = context.CurrentBlock.Statements.Last();
            toUpgrade.Parent.Statements.RemoveAt(toUpgrade.Parent.Statements.Count - 1);
            var upgraded = new RunningCodeStatement() { Tokens = toUpgrade.Tokens, AST = toUpgrade.AST, Parent = toUpgrade.Parent };
            toUpgrade.Parent.Statements.Add(upgraded);

            context.BlockStack.Push(new Loop() { Parent = context.CurrentBlock, Iterations = 3 });
            context.CurrentBlock.Tokens.AddRange(tokens);
            context.CurrentBlock.Parent.Statements.Add(context.CurrentBlock);
        }
        else if (IsCurrentOpenCurlyFromCatchBlock(context))
        {
            context.BlockStack.Push(new CatchBlock() { Parent = context.CurrentBlock, });
            context.CurrentBlock.Tokens.AddRange(tokens);
            context.CurrentBlock.Parent.Statements.Add(context.CurrentBlock);
        }
        else if (IsCurrentOpenCurlyFromTryBlock(context))
        {
            context.BlockStack.Push(new TryBlock() { Parent = context.CurrentBlock, });
            context.CurrentBlock.Tokens.AddRange(tokens);
            context.CurrentBlock.Parent.Statements.Add(context.CurrentBlock);
        }
        else if (IsCurrentOpenCurlyFromIfBlock(context))
        {
            var previous = context.CurrentBlock.Statements.Last() as NoOpStatement;
            context.CurrentBlock.Statements.RemoveAt(context.CurrentBlock.Statements.Count - 1);
            context.BlockStack.Push(new If() { Parent = context.CurrentBlock, });
            context.CurrentBlock.Tokens.AddRange(previous.Tokens);
            context.CurrentBlock.Tokens.AddRange(tokens);
            context.CurrentBlock.Parent.Statements.Add(context.CurrentBlock);
        }
        else if (IsCurrentOpenCurlyFromElseBlock(context))
        {
            var previous = context.CurrentBlock.Statements.Last() as NoOpStatement;
            context.CurrentBlock.Statements.RemoveAt(context.CurrentBlock.Statements.Count - 1);
            context.BlockStack.Push(new Else() { Parent = context.CurrentBlock, });
            context.CurrentBlock.Tokens.AddRange(previous.Tokens);
            context.CurrentBlock.Tokens.AddRange(tokens);
            context.CurrentBlock.Parent.Statements.Add(context.CurrentBlock);
        }
        else if (context.Reader.Position > 0)
        {
            bool done = false;
            var initialPosition = context.Reader.Position;
            while (context.Reader.Position > 0)
            {
                context.Reader.RewindOne();
                if (context.Reader.Current.Type == TokenType.NonTrailingWhitespace ||
                    context.Reader.Current.Type == TokenType.TrailingWhitespace ||
                    context.Reader.Current.Type == TokenType.Comment ||
                    context.Reader.Current.Type == TokenType.Newline)
                {
                    continue;
                }
                else if (context.Reader.Current.Type == TokenType.SpecialCharacter &&
                    context.Reader.Current.Value == ")")
                {
                    context.BlockStack.Push(new Function() { Parent = context.CurrentBlock });
                    context.CurrentBlock.Tokens.AddRange(tokens);
                    context.CurrentBlock.Parent.Statements.Add(context.CurrentBlock);
                    done = true;
                    break;
                }
                else
                {
                    if (IsWithinFunction(context))
                    {
                        context.BlockStack.Push(new CodeBlock() { Parent = context.CurrentBlock });
                        context.CurrentBlock.Tokens.AddRange(tokens);
                        context.CurrentBlock.Parent.Statements.Add(context.CurrentBlock);
                        done = true;
                        break;
                    }
                    else
                    {
                        context.BlockStack.Push(new NoOpBlock() { Parent = context.CurrentBlock });
                        context.CurrentBlock.Tokens.AddRange(tokens);
                        context.CurrentBlock.Parent.Statements.Add(context.CurrentBlock);
                        done = true;
                        break;
                    }
                }
            }

            if (!done)
            {
                context.BlockStack.Push(new NoOpBlock() { Parent = context.CurrentBlock });
                context.CurrentBlock.Tokens.AddRange(tokens);
                context.CurrentBlock.Parent.Statements.Add(context.CurrentBlock);
            }

            FastForward(context, initialPosition);
        }
        else
        {
            context.BlockStack.Push(new NoOpBlock() { Parent = context.CurrentBlock });
            context.CurrentBlock.Tokens.AddRange(tokens);
            context.CurrentBlock.Parent.Statements.Add(context.CurrentBlock);
        }

        context.CurrentBlock.Id = ++(context.NextBlockId);
        context.CurrentBlock.Path = string.Join("/", context.BlockStack.Reverse().Select(b => b.Id.ToString()));
    }

    private static void OnBlockClose(Context context)
    {
        context.CurrentBlock.Tokens.Add(context.Reader.Expect("}"));
        if (context.BlockStack.Count > 0)
        {
            context.BlockStack.Pop();
        }
    }

    private static void OnSingleLineComment(Context context)
    {
        var position = context.Reader.Position;
        context.Reader.Expect("//");

        if (context.Reader.CanAdvance(true) == false || context.Reader.Peek().Value != "#")
        {
            Rewind(context, position);
            var comment = new Comment() { Parent = context.CurrentBlock };
            context.CurrentBlock.Statements.Add(comment);
            while (context.Reader.CanAdvance() && context.Reader.Peek().Value != "\n")
            {
                comment.Tokens.Add(context.Reader.Advance());
            }
        }
        else
        {
            Rewind(context, position);
            var directiveTokens = new List<CodeToken>();
            directiveTokens.Add(context.Reader.Expect("//"));
            directiveTokens.Add(context.Reader.Expect("#", true));
            while (context.Reader.CanAdvance() && context.Reader.Peek().Value != "\n")
            {
                directiveTokens.Add(context.Reader.Advance());
            }

            if (context.Reader.CanAdvance() && context.Reader.Peek().Value == "\n")
            {
                context.Reader.Advance();
            }

            var directive = DirectiveHydrator.Hydrate(directiveTokens);
            directive.Parent = context.CurrentBlock;
            context.CurrentBlock.Statements.Add(directive);
        }
    }

    private static void OnSingleLineStatement(Context context)
    {
        var statement = new RunningCodeStatement() { Parent = context.CurrentBlock };
        statement.Tokens.Add(context.Reader.Advance());
        while (context.Reader.CanAdvance() && IsEndOfStatement(context.Reader.Peek()) == false)
        {
            statement.Tokens.Add(context.Reader.Advance());
        }

        if (context.Reader.CanAdvance())
        {
            statement.Tokens.Add(context.Reader.Expect(";"));
        }

        if (context.Reader.CanAdvance() && context.Reader.Peek().Value == "\n")
        {
            context.Reader.Advance();
        }

        foreach (var token in statement.Tokens)
        {
            if (token.Value.Trim().Length == 0)
            {
                // keep going
            }
            else if (token.Value == "return" && token.Type == TokenType.Keyword)
            {
                var returnStatement = new Return() { Parent = context.CurrentBlock };
                returnStatement.Tokens.AddRange(statement.Tokens);
                statement = returnStatement;
                break;
            }
            else
            {
                break;
            }
        }

        context.CurrentBlock.Statements.Add(statement);

    }

    private static bool IsEndOfStatement(CodeToken t) => t.Value == ";" && t.Type == TokenType.SpecialCharacter;

    private static void OnUnknownCode(Context context)
    {
        var lastStatement = context.CurrentBlock.Statements.LastOrDefault() as NoOpStatement;
        if (lastStatement == null)
        {
            lastStatement = new NoOpStatement() { Parent = context.CurrentBlock };
            context.CurrentBlock.Statements.Add(lastStatement);
        }
        lastStatement.Tokens.Add(context.Reader.Advance());
    }

    private static bool IsWithinFunction(Context context)
    {
        var current = context.CurrentBlock;
        while (current != null)
        {
            if (current is Function)
            {
                return true;
            }
            else
            {
                current = current.Parent;
            }
        }
        return false;
    }

    private static bool DoesCurrentLineEndWithSemicolon(Context context)
    {
        var initialPosition = context.Reader.Position;
        var currentLine = context.Reader.Current.Line;

        CodeToken lastToken = null;
        var ignoreTypes = new TokenType[] { TokenType.Comment, TokenType.TrailingWhitespace };
        var ret = false;
        while (context.Reader.CanAdvance())
        {
            context.Reader.Advance();

            if (context.Reader.Current.Type == TokenType.Newline)
            {
                if (lastToken != null && lastToken.Value == ";" && lastToken.Type == TokenType.SpecialCharacter)
                {
                    ret = true;
                }
                else
                {
                    break;
                }
            }
            else if (ignoreTypes.Contains(context.Reader.Current.Type) == false)
            {
                lastToken = context.Reader.Current;
            }
        }

        Rewind(context, initialPosition);
        return ret;
    }


    private static bool IsCurrentOpenCurlyFromLoop(Context context)
    {
        var position = context.Reader.Position;
        int targetLine;
        if (context.Reader.Current.Line > 1 && IsCurrentOpenCurlyAloneOnLine(context))
        {
            targetLine = context.Reader.Current.Line - 1;
        }
        else
        {
            targetLine = context.Reader.Current.Line;
        }

        var targetToken = GetFirstNonWhitespaceNotCommentTokenOnLine(context, targetLine);
        FastForward(context, position);

        if (targetToken != null && loopIndicators.Contains(targetToken.Value))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static bool IsCurrentOpenCurlyFromTryBlock(Context context)
    {
        var position = context.Reader.Position;
        int targetLine;
        if (context.Reader.Current.Line > 1 && IsCurrentOpenCurlyAloneOnLine(context))
        {
            targetLine = context.Reader.Current.Line - 1;
        }
        else
        {
            targetLine = context.Reader.Current.Line;
        }

        var targetToken = GetFirstNonWhitespaceNotCommentTokenOnLine(context, targetLine);
        FastForward(context, position);

        if (targetToken != null && targetToken.Value == "try")
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static bool IsCurrentOpenCurlyFromIfBlock(Context context)
    {
        var position = context.Reader.Position;
        int targetLine;
        if (context.Reader.Current.Line > 1 && IsCurrentOpenCurlyAloneOnLine(context))
        {
            targetLine = context.Reader.Current.Line - 1;
        }
        else
        {
            targetLine = context.Reader.Current.Line;
        }

        var targetToken = GetFirstNonWhitespaceNotCommentTokenOnLine(context, targetLine);
        FastForward(context, position);

        if (targetToken != null && targetToken.Value == "if")
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static bool IsCurrentOpenCurlyFromElseBlock(Context context)
    {
        var position = context.Reader.Position;
        int targetLine;
        if (context.Reader.Current.Line > 1 && IsCurrentOpenCurlyAloneOnLine(context))
        {
            targetLine = context.Reader.Current.Line - 1;
        }
        else
        {
            targetLine = context.Reader.Current.Line;
        }

        var targetToken = GetFirstNonWhitespaceNotCommentTokenOnLine(context, targetLine);
        FastForward(context, position);

        if (targetToken != null && targetToken.Value == "else")
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static bool IsCurrentOpenCurlyFromCatchBlock(Context context)
    {
        var position = context.Reader.Position;
        int targetLine;
        if (context.Reader.Current.Line > 1 && IsCurrentOpenCurlyAloneOnLine(context))
        {
            targetLine = context.Reader.Current.Line - 1;
        }
        else
        {
            targetLine = context.Reader.Current.Line;
        }

        var targetToken = GetFirstNonWhitespaceNotCommentTokenOnLine(context, targetLine);
        FastForward(context, position);

        if (targetToken != null && targetToken.Value == "catch")
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static CodeToken GetFirstNonWhitespaceNotCommentTokenOnLine(Context context, int line)
    {
        var ret = context.Reader.Tokens
            .Where(t => t.Line == line)
            .Where(t => t.Type != TokenType.Comment)
            .Where(t => t.Type != TokenType.Newline)
            .Where(t => t.Type != TokenType.NonTrailingWhitespace)
            .Where(t => t.Type != TokenType.TrailingWhitespace)
            .OrderBy(t => t.Column)
            .FirstOrDefault();
        return ret;
    }

    private static bool IsCurrentOpenCurlyAloneOnLine(Context context)
    {
        var position = context.Reader.Position;
        var line = context.Reader.Current.Line;
        while (context.Reader.Position > 0)
        {
            context.Reader.RewindOne();
            if (context.Reader.Current.Value == "\n")
            {
                FastForward(context, position);
                return true;
            }
            else if (string.IsNullOrWhiteSpace(context.Reader.Current.Value) || context.Reader.Current.Type == TokenType.Comment)
            {
                // nothing
            }
            else
            {
                FastForward(context, position);
                return false;
            }
        }

        FastForward(context, position);
        return false;
    }

    private static void FastForward(Context context, int initialPosition)
    {
        while (context.Reader.Position != initialPosition)
        {
            context.Reader.Advance();
        }
    }

    private static void Rewind(Context context, int initialPosition)
    {
        while (context.Reader.Position != initialPosition)
        {
            context.Reader.RewindOne();
        }
    }
}
