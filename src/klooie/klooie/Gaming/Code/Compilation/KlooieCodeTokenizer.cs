namespace klooie.Gaming.Code;
internal static class KlooieCodeTokenizer
{
    public static List<CodeToken> Tokenize(string code, string sourceLocation)
    {
        code = code.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\t", "    ");
        var tokenizer = new KlooieCodeTokenizerImpl();
        tokenizer.EscapeSequenceIndicator = (char)0;
        tokenizer.DoubleQuoteBehavior = DoubleQuoteBehavior.NoSpecialHandling;
        tokenizer.WhitespaceBehavior = WhitespaceBehavior.DelimitAndInclude;
        tokenizer.Delimiters.AddRange(new string[]
        {
            "/*", "*/", "//",
            "'","\"",
        });
        tokenizer.Delimiters.AddRange(LanguageConstants.Instance.SpecialCharacters);
        tokenizer.SourceFileLocation = sourceLocation ?? "no source location specified";
        var ret = tokenizer.Tokenize(code);
        return ret;
    }

    private class KlooieCodeTokenizerImpl : Tokenizer<CodeToken>
    {
        protected override CodeToken TokenFactory(int currentIndex, int line, int col)
        {
            var ret = new CodeToken(currentIndex, line, col);
            ret.SourceFileLocation = this.SourceFileLocation;
            return ret;
        }
    }
}
