﻿namespace klooie.Gaming.Code;

public enum CodeDisplayState
{
    Normal,
    Infected,
    InfectedWithHotfixReady,
    TrainingData,
    Ghost,
}
public interface IGhost
{
    bool IsGhost { get; set; }
}
public class CodeControl : GameCollider, IGhost
{
    public bool IsTargeted { get; set; }
    public bool IsDimmed { get; set; }
    public bool IsGhost
    {
        get
        {
            return State == CodeDisplayState.Ghost;
        }
        set
        {
            State = value ? CodeDisplayState.Ghost : CodeDisplayState.Normal;
            if (IsGhost)
            {
                this.MoveTo(this.Left, this.Top);
            }
            else
            {
                this.MoveTo(this.Left, this.Top);
            }
        }
    }
    public LineNumberControl LineNumberElement => Game.Current.GamePanel.Controls.WhereAs<LineNumberControl>()
        .Where(l => Math.Floor(l.CenterY()) == Math.Floor(this.CenterY()))
        .SingleOrDefault();

    private CodeDisplayState _state = CodeDisplayState.Normal;
    public CodeDisplayState State
    {
        get => _state;
        set
        {
            _state = value;
            var lineNumber = LineNumberElement;
            if (lineNumber != null)
            {
                lineNumber.State = value;
            }
            FirePropertyChanged(nameof(Bounds));
        }
    }

    public CodeToken Token;

    public CodeControl(CodeToken token)
    {
        CompositionMode = CompositionMode.BlendBackground;
        this.MoveTo(this.Left, this.Top);
        this.ResizeTo(token.Value.Length, 1);
        this.AddTag(DamageDirective.DamageableTag);
        this.Token = token;
        Subscribe(nameof(Bounds), () =>
        {
            IsTargeted = MainCharacter.Current?.Target == this;

            if (this.State == CodeDisplayState.Infected || this.State == CodeDisplayState.InfectedWithHotfixReady)
            {
                foreach (var line in Game.Current.GamePanel.Controls.WhereAs<LineNumberControl>().ToArray())
                {
                    line.State = CodeDisplayState.Normal;
                }

                foreach (var el in Game.Current.GamePanel.Controls.WhereAs<CodeControl>().ToArray())
                {
                    if (el.State == CodeDisplayState.Infected || el.State == CodeDisplayState.InfectedWithHotfixReady)
                    {
                        var line = el.LineNumberElement;
                        if (line != null)
                        {
                            line.State = el.State;
                        }
                    }
                }
            }
        }, this);
    }

    public virtual ConsoleString LineOfCode
    {
        get
        {
            return Token.Value.ToConsoleString();
        }
    }

    public override string ToString() => $"{Token.ToString()}";

    public ConsoleString FormatToken(CodeDisplayState? state = null)
    {
        state = state.HasValue ? state.Value : State;
        var token = Token;

        ConsoleColor fg = RGB.Gray, bg = RGB.Black;

        if (state == CodeDisplayState.Normal)
        {
            if (token.Statement is Directive)
            {
                bg = ConsoleColor.DarkGreen;
            }

            if (token.Type == TokenType.Comment)
            {
                fg = ConsoleColor.Green;
            }
            else if (token.Type == TokenType.Keyword)
            {
                fg = ConsoleColor.Cyan;
            }
            else if (token.Type == TokenType.DoubleQuotedStringLiteral)
            {
                return DoubleQuotedStringFormat();
                fg = ConsoleColor.DarkYellow;
            }
            else if (token.Type == TokenType.TrailingWhitespace)
            {
                bg = ConsoleColor.White;
            }
            else if (token.Type == TokenType.NonTrailingWhitespace)
            {
                bg = ConsoleColor.White;
            }
        }
        else if (state == CodeDisplayState.Infected)
        {
            fg = ConsoleColor.Red;
        }
        else if (state == CodeDisplayState.InfectedWithHotfixReady)
        {
            fg = ConsoleColor.Black;
            bg = ConsoleColor.White;
        }
        else if (state == CodeDisplayState.TrainingData)
        {
            fg = ConsoleColor.Yellow;
        }

        if (IsDimmed)
        {
            fg = ConsoleColor.DarkGray;
            bg = ConsoleColor.Black;
        }

        return token.Value.ToConsoleString(fg, bg);
    }

    private ConsoleString DoubleQuotedStringFormat()
    {
        var game = ConsoleApp.Current as Game;
        if (Token.Value == "[")
        {
            return new ConsoleString("[", RGB.DarkGray);
        }
        else if (Token.Value == "]")
        {
            return new ConsoleString("]", RGB.DarkGray);
        }
        if (Token.Value == "{")
        {
            return new ConsoleString("{", RGB.DarkGray);
        }
        else if (Token.Value == "}")
        {
            return new ConsoleString("}", RGB.DarkGray);
        }
        else if (Enum.TryParse(Token.Value, out ConsoleColor color))
        {
            return new ConsoleString(Token.Value, color);
        }
        else if (Heap.Current.TryGetValue(Token.Value, out object val))
        {
            return new ConsoleString(Token.Value, RGB.Gray);
        }
        else
        {
            return new ConsoleString(Token.Value, RGB.DarkYellow);
        }
    }

    public static IEnumerable<CodeControl> CodeElements => Game.Current.GamePanel.Controls.WhereAs<CodeControl>();

    public static IEnumerable<CodeControl> CompiledCodeElements => CodeElements
        .Where(c => c is MaliciousCodeElement == false)
        .Where(c => c is OptimizationCodeElement == false)
        .ToArray();

    protected override void OnPaint(ConsoleBitmap context)
    {
        var str = FormatToken();
        if (IsTargeted) str = str.StringValue.ToConsoleString(ConsoleColor.Black, ConsoleColor.Cyan);
        context.DrawString(str, 0, 0);
    }
}
