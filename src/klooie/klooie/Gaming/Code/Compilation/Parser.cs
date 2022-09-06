using PowerArgs;

namespace klooie.Gaming.Code;
public enum TokenType
{
    PlainText,
    Comment,
    DoubleQuotedStringLiteral,
    SingleQuotedStringLiteral,
    Keyword,
    SpecialCharacter,
    NonTrailingWhitespace,
    TrailingWhitespace,
    Newline,
}

public static class Parser
{
    public static void Parse(List<CodeToken> tokens)
    {
        var reader = new TokenReader<CodeToken>(tokens);

        while (reader.CanAdvance())
        {
            if (reader.Peek().Value == "/*")
            {
                ParseBlockComment(reader);
            }
            else if (reader.Peek().Value == "//")
            {
                ParseLineComment(reader);
            }
            else if (reader.Peek().Value == "\"")
            {
                ParseDoubleQuotedStringLiteral(reader);
            }
            else if (LanguageConstants.Instance.IsSpecial(reader.Peek().Value))
            {
                reader.Advance().Type = TokenType.SpecialCharacter;
            }
            else if (LanguageConstants.Instance.IsKeyword(reader.Peek().Value))
            {
                reader.Advance().Type = TokenType.Keyword;
            }
            else if (reader.Peek().Value == "\n")
            {
                reader.Advance().Type = TokenType.Newline;
            }
            else if (string.IsNullOrWhiteSpace(reader.Peek().Value))
            {
                ParseWhitespace(reader);
            }
            else
            {
                reader.Advance().Type = TokenType.PlainText;
            }
        }
    }

    private static void ParseWhitespace(TokenReader<CodeToken> reader)
    {
        var tokens = new List<CodeToken>();
        tokens.Add(reader.Advance());

        while (reader.CanAdvance() && string.IsNullOrWhiteSpace(reader.Peek().Value) && reader.Peek().Value != "\n")
        {
            tokens.Add(reader.Advance());
        }

        var type = IsNextAdvanceOnANewLine(reader) ? TokenType.TrailingWhitespace : TokenType.NonTrailingWhitespace;
        foreach (var token in tokens)
        {
            token.Type = type;
        }
    }

    private static void ParseDoubleQuotedStringLiteral(TokenReader<CodeToken> reader)
    {
        reader.Expect("\"").Type = TokenType.DoubleQuotedStringLiteral;
        while (reader.CanAdvance() && reader.Peek().Value != "\"")
        {
            reader.Advance().Type = TokenType.DoubleQuotedStringLiteral;

            if (reader.Current.Value == "\\" && reader.CanAdvance() && reader.Peek().Value == "\"")
            {
                reader.Advance().Type = TokenType.DoubleQuotedStringLiteral;
            }
        }

        if (reader.CanAdvance())
        {
            reader.Expect("\"").Type = TokenType.DoubleQuotedStringLiteral;
        }
    }

    private static void ParseBlockComment(TokenReader<CodeToken> reader)
    {
        reader.Expect("/*").Type = TokenType.Comment;
        while (reader.CanAdvance() && reader.Peek().Value != "*/")
        {
            reader.Advance().Type = TokenType.Comment;
        }

        if (reader.CanAdvance())
        {
            reader.Expect("*/").Type = TokenType.Comment;
        }
    }

    private static void ParseLineComment(TokenReader<CodeToken> reader)
    {
        reader.Expect("//").Type = TokenType.Comment;
        while (reader.CanAdvance() && IsNextAdvanceOnANewLine(reader) == false)
        {
            reader.Advance().Type = TokenType.Comment;
        }
    }

    private static bool IsNextAdvanceOnANewLine(TokenReader<CodeToken> reader)
    {
        return reader.Position == 0 || reader.CanAdvance() == false ? true : reader.Peek().Value == "\n";
    }
}