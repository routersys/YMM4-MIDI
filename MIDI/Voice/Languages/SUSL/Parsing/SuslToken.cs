namespace MIDI.Voice.SUSL.Parsing
{
    public enum TokenType
    {
        Header,
        Section,
        End,
        Keyword,
        Identifier,
        Integer,
        Float,
        String,

        Period,
        LBrace,
        RBrace,
        Semicolon,
        Slash,
        Equals,
        Plus,
        Comma,

        EOF
    }

    public struct Token
    {
        public TokenType Type { get; }
        public string Value { get; }
        public int Line { get; }
        public int Column { get; }

        public Token(TokenType type, string value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }
    }
}