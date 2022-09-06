namespace klooie.Gaming.Code;
internal class LanguageConstants
{
    private static Lazy<LanguageConstants> _instance = new Lazy<LanguageConstants>(() => new LanguageConstants(), true);
    public static LanguageConstants Instance => _instance.Value;

    private List<string> specialCharacters;
    private List<string> keywords;

    public IEnumerable<string> SpecialCharacters => specialCharacters.AsReadOnly();

    public bool IsKeyword(string val) => keywords.BinarySearch(val) >= 0;
    public bool IsSpecial(string val) => specialCharacters.BinarySearch(val) >= 0;

    private LanguageConstants()
    {
        specialCharacters = new List<string>()
        {
            "{", "}", "[", "]", "(", ")", "<", ">", "\\",
            ";",":", ".", "?","~","&", "@",
            "=", "+", "%", "!", "$","#", "^",
        };
        specialCharacters.Sort();

        keywords = new List<string>()
        {
            "public", "private", "protected", "internal", "static",
            "try", "catch",
            "namespace", "using", "import",
            "class", "interface", "enum", "struct",
            "get", "set",
            "if", "for", "while", "foreach", "do", "continue", "break", "else",
            "await", "async", "return",
            "new",
            "void", "string", "var", "throw", "int","bool", "boolean", "number", "double", "long", "short", "byte", "typeof", "true", "false",
        };
        keywords.Sort();
    }
}