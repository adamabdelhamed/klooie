using klooie.Gaming;
using PowerArgs;
using System;
using System.Collections.Generic;

namespace klooie.Gaming.Code;
public class AST : ICanBeAConsoleString, IRuleProvider
{
    public Block Root { get; set; }
    public List<CodeToken> Tokens { get; set; } = new List<CodeToken>();
    public override string ToString() => ToConsoleString().StringValue;

    public IEnumerable<Function> Functions
    {
        get
        {
            var functions = new List<Function>();
            Root.Visit((s) =>
           {
               if (s.GetType() == typeof(Function))
               {
                   functions.Add(s as Function);
               }
               return false;
           });
            return functions;
        }
    }

    public IRule[] Rules
    {
        get
        {
            var ret = new List<Directive>();
            Root.Visit((s) =>
            {
                if (s is Directive)
                {
                    ret.Add(s as Directive);
                }
                return false;
            });
            return ret.ToArray();

        }
    }


    public bool TryGetFunction(CodeToken token, out Function ret)
    {
        return TryGetFunction(token.Statement, out ret);
    }

    public bool TryGetFunction(IStatement s, out Function ret)
    {
        var current = s;

        while (current != null)
        {
            if (current.GetType() == typeof(Function))
            {
                ret = current as Function;
                return true;
            }
            else
            {
                current = current.Parent;
            }
        }

        ret = null;
        return false;
    }

    public ConsoleString ToConsoleString()
    {
        var buffer = new List<ConsoleCharacter>();
        foreach (var token in Tokens)
        {
            if (token.Type == TokenType.Comment)
            {
                buffer.AddRange(token.Value.ToGreen());
            }
            else if (token.Type == TokenType.DoubleQuotedStringLiteral)
            {
                buffer.AddRange(token.Value.ToYellow());
            }
            else if (token.Type == TokenType.Keyword)
            {
                buffer.AddRange(token.Value.ToCyan());
            }
            else if (token.Type == TokenType.SpecialCharacter)
            {
                buffer.AddRange(token.Value.ToDarkGray());
            }
            else if (token.Type == TokenType.TrailingWhitespace)
            {
                buffer.AddRange(token.Value.ToConsoleString(bg: ConsoleColor.White));
            }
            else
            {
                buffer.AddRange(token.Value.ToWhite());
            }
        }

        return new ConsoleString(buffer);
    }
}
