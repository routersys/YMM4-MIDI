using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using MIDI.Voice.EMEL.Errors;

namespace MIDI.Voice.EMEL.Parsing
{
    public class EmelParser
    {
        public enum TokenType
        {
            Let, Repeat, If, Else, Track, Global, Func,
            Identifier, Number, String,
            Plus, Minus, Multiply, Divide, Percent,
            Assign,
            LogicalOr, LogicalAnd,
            EqualEqual, NotEqual,
            Greater, GreaterEqual,
            Less, LessEqual,
            LParen, RParen, LBrace, RBrace,
            LBracket, RBracket,
            Comma, Semicolon,
            Error,
            EOF
        }

        public class Token
        {
            public TokenType Type { get; }
            public string? Value { get; }
            public int Line { get; }
            public int Column { get; }
            public Token(TokenType type, string? value, int line, int column)
            {
                Type = type;
                Value = value;
                Line = line;
                Column = column;
            }
        }

        private static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
        {
            {"let", TokenType.Let},
            {"repeat", TokenType.Repeat},
            {"if", TokenType.If},
            {"else", TokenType.Else},
            {"Track", TokenType.Track},
            {"Global", TokenType.Global},
            {"func", TokenType.Func}
        };

        public static List<Token> Lex(string code)
        {
            var tokens = new List<Token>();
            var tokenRegex = new Regex(
                @"(?<String>""(?:\\.|[^""])*"")|" +
                @"(?<Number>\-?\d+(?:\.\d+)?)|" +
                @"(?<Identifier>[a-zA-Z_][a-zA-Z0-9_]*)|" +
                @"(?<Operator>==|!=|>=|<=|&&|\|\||>|<|[\+\-\*/%=])|" +
                @"(?<Delimiter>[\(\)\{\}\,;\[\]])|" +
                @"(?<Whitespace>[\s\u00A0]+)|" +
                @"(?<Comment>(//.*))|" +
                @"(?<Error>.)",
                RegexOptions.Compiled
            );

            var matches = tokenRegex.Matches(code);
            int line = 1;
            int lineStartOffset = 0;
            int lastProcessedIndex = 0;

            foreach (Match match in matches)
            {
                for (int i = lastProcessedIndex; i < match.Index; i++)
                {
                    if (code[i] == '\n')
                    {
                        line++;
                        lineStartOffset = i + 1;
                    }
                }

                int col = match.Index - lineStartOffset + 1;

                if (match.Groups["Whitespace"].Success || match.Groups["Comment"].Success)
                {
                    for (int i = 0; i < match.Length; i++)
                    {
                        if (match.Value[i] == '\n')
                        {
                            line++;
                            lineStartOffset = match.Index + i + 1;
                        }
                    }
                    lastProcessedIndex = match.Index + match.Length;
                    continue;
                }

                if (match.Groups["String"].Success) tokens.Add(new Token(TokenType.String, match.Value.Substring(1, match.Value.Length - 2), line, col));
                else if (match.Groups["Number"].Success) tokens.Add(new Token(TokenType.Number, match.Value, line, col));
                else if (match.Groups["Identifier"].Success)
                {
                    if (Keywords.TryGetValue(match.Value, out var type)) tokens.Add(new Token(type, null, line, col));
                    else tokens.Add(new Token(TokenType.Identifier, match.Value, line, col));
                }
                else if (match.Groups["Operator"].Success)
                {
                    switch (match.Value)
                    {
                        case "+": tokens.Add(new Token(TokenType.Plus, null, line, col)); break;
                        case "-": tokens.Add(new Token(TokenType.Minus, null, line, col)); break;
                        case "*": tokens.Add(new Token(TokenType.Multiply, null, line, col)); break;
                        case "/": tokens.Add(new Token(TokenType.Divide, null, line, col)); break;
                        case "%": tokens.Add(new Token(TokenType.Percent, null, line, col)); break;
                        case "=": tokens.Add(new Token(TokenType.Assign, null, line, col)); break;
                        case "&&": tokens.Add(new Token(TokenType.LogicalAnd, null, line, col)); break;
                        case "||": tokens.Add(new Token(TokenType.LogicalOr, null, line, col)); break;
                        case "==": tokens.Add(new Token(TokenType.EqualEqual, null, line, col)); break;
                        case "!=": tokens.Add(new Token(TokenType.NotEqual, null, line, col)); break;
                        case ">": tokens.Add(new Token(TokenType.Greater, null, line, col)); break;
                        case ">=": tokens.Add(new Token(TokenType.GreaterEqual, null, line, col)); break;
                        case "<": tokens.Add(new Token(TokenType.Less, null, line, col)); break;
                        case "<=": tokens.Add(new Token(TokenType.LessEqual, null, line, col)); break;
                    }
                }
                else if (match.Groups["Delimiter"].Success)
                {
                    switch (match.Value)
                    {
                        case "(": tokens.Add(new Token(TokenType.LParen, null, line, col)); break;
                        case ")": tokens.Add(new Token(TokenType.RParen, null, line, col)); break;
                        case "{": tokens.Add(new Token(TokenType.LBrace, null, line, col)); break;
                        case "}": tokens.Add(new Token(TokenType.RBrace, null, line, col)); break;
                        case "[": tokens.Add(new Token(TokenType.LBracket, null, line, col)); break;
                        case "]": tokens.Add(new Token(TokenType.RBracket, null, line, col)); break;
                        case ",": tokens.Add(new Token(TokenType.Comma, null, line, col)); break;
                        case ";": tokens.Add(new Token(TokenType.Semicolon, null, line, col)); break;
                    }
                }
                else if (match.Groups["Error"].Success)
                {
                    throw new EmelException(EmelErrorCode.Lexer_InvalidCharacter, line, col, match.Value);
                }

                lastProcessedIndex = match.Index + match.Length;
            }

            for (int i = lastProcessedIndex; i < code.Length; i++)
            {
                if (code[i] == '\n')
                {
                    line++;
                    lineStartOffset = i + 1;
                }
            }
            int eofCol = code.Length - lineStartOffset + 1;
            tokens.Add(new Token(TokenType.EOF, null, line, eofCol));

            return tokens;
        }

        private readonly List<Token> tokens;
        private int position = 0;

        public EmelParser(List<Token> tokens)
        {
            this.tokens = tokens;
        }

        private Token Peek() => tokens[position];
        private Token Previous() => tokens[position - 1];
        private Token Advance()
        {
            if (!IsAtEnd()) position++;
            return Previous();
        }
        private bool IsAtEnd() => Peek().Type == TokenType.EOF;
        private bool Check(TokenType type) => Peek().Type == type;
        private bool Match(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }
        private Token Consume(TokenType type, string message)
        {
            if (Check(type)) return Advance();
            var token = Peek();
            throw new EmelException(EmelErrorCode.Parser_ExpectedToken, token.Line, token.Column, type, token.Type);
        }

        public AstNode ParseProgram()
        {
            var startToken = Peek();
            var program = new ProgramNode(startToken);
            while (!IsAtEnd())
            {
                program.Statements.Add(ParseStatement());
            }
            return program;
        }

        private AstNode ParseStatement()
        {
            if (Match(TokenType.Let)) return ParseLetStatement();
            if (Match(TokenType.Repeat)) return ParseRepeatStatement();
            if (Match(TokenType.If)) return ParseIfStatement();
            if (Match(TokenType.Func)) return ParseFunctionDeclaration();
            if (Match(TokenType.LBrace)) return ParseBlockStatement();
            if (Match(TokenType.Track)) return ParseTrackBlockStatement();
            if (Match(TokenType.Global)) return ParseGlobalBlockStatement();
            return ParseExpressionStatement();
        }

        private AstNode ParseLetStatement()
        {
            var letToken = Previous();
            var nameToken = Consume(TokenType.Identifier, "Expect variable name.");
            Consume(TokenType.Assign, "Expect '=' after variable name.");
            var initializer = ParseExpression();
            Match(TokenType.Semicolon);
            return new LetNode(letToken, nameToken.Value!, initializer);
        }

        private AstNode ParseRepeatStatement()
        {
            var repeatToken = Previous();
            Consume(TokenType.LParen, "Expect '(' after 'repeat'.");
            var count = ParseExpression();
            Consume(TokenType.RParen, "Expect ')' after repeat count.");
            var block = ParseStatement();
            return new RepeatNode(repeatToken, count, block);
        }

        private AstNode ParseIfStatement()
        {
            var ifToken = Previous();
            Consume(TokenType.LParen, "Expect '(' after 'if'.");
            var condition = ParseExpression();
            Consume(TokenType.RParen, "Expect ')' after if condition.");
            var thenBranch = ParseStatement();
            AstNode? elseBranch = null;
            if (Match(TokenType.Else))
            {
                elseBranch = ParseStatement();
            }
            return new IfNode(ifToken, condition, thenBranch, elseBranch);
        }

        private AstNode ParseFunctionDeclaration()
        {
            var funcToken = Previous();
            var name = Consume(TokenType.Identifier, "Expect function name.").Value!;
            Consume(TokenType.LParen, "Expect '(' after function name.");
            var parameters = new List<string>();
            if (!Check(TokenType.RParen))
            {
                do
                {
                    parameters.Add(Consume(TokenType.Identifier, "Expect parameter name.").Value!);
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RParen, "Expect ')' after parameters.");
            Consume(TokenType.LBrace, "Expect '{' before function body.");
            var body = ParseBlockStatement();
            return new FunctionDefinitionNode(funcToken, name, parameters, body);
        }

        private AstNode ParseBlockStatement()
        {
            var lBraceToken = Previous();
            var block = new BlockNode(lBraceToken);
            while (!Check(TokenType.RBrace) && !IsAtEnd())
            {
                block.Statements.Add(ParseStatement());
            }
            Consume(TokenType.RBrace, "Expect '}' after block.");
            return block;
        }

        private AstNode ParseTrackBlockStatement()
        {
            var trackToken = Previous();
            Consume(TokenType.LBracket, "Expect '[' after Track.");
            var number = ParseExpression();
            Consume(TokenType.RBracket, "Expect ']' after Track number.");
            Consume(TokenType.LBrace, "Expect '{' before Track body.");
            var block = ParseBlockStatement();
            return new TrackNode(trackToken, number, block);
        }

        private AstNode ParseGlobalBlockStatement()
        {
            var globalToken = Previous();
            Consume(TokenType.LBrace, "Expect '{' after Global.");
            var block = ParseBlockStatement();
            return new GlobalBlockNode(globalToken, block);
        }

        private AstNode ParseExpressionStatement()
        {
            var expr = ParseExpression();
            Match(TokenType.Semicolon);
            return expr;
        }

        private AstNode ParseExpression() => ParseAssignment();

        private AstNode ParseAssignment()
        {
            var expr = ParseLogicalOr();
            if (Match(TokenType.Assign))
            {
                var equalsToken = Previous();
                var value = ParseAssignment();
                if (expr is VariableNode varNode)
                {
                    return new AssignNode(equalsToken, varNode.Name, value);
                }
                throw new EmelException(EmelErrorCode.Parser_InvalidAssignmentTarget, expr.StartToken.Line, expr.StartToken.Column);
            }
            return expr;
        }

        private AstNode ParseLogicalOr()
        {
            var expr = ParseLogicalAnd();
            while (Match(TokenType.LogicalOr))
            {
                var opToken = Previous();
                var op = BinaryOperator.LogicalOr;
                var right = ParseLogicalAnd();
                expr = new BinaryOpNode(opToken, expr, op, right);
            }
            return expr;
        }

        private AstNode ParseLogicalAnd()
        {
            var expr = ParseEquality();
            while (Match(TokenType.LogicalAnd))
            {
                var opToken = Previous();
                var op = BinaryOperator.LogicalAnd;
                var right = ParseEquality();
                expr = new BinaryOpNode(opToken, expr, op, right);
            }
            return expr;
        }

        private AstNode ParseEquality()
        {
            var expr = ParseComparison();
            while (Match(TokenType.EqualEqual, TokenType.NotEqual))
            {
                var opToken = Previous();
                var op = opToken.Type == TokenType.EqualEqual ? BinaryOperator.EqualEqual : BinaryOperator.NotEqual;
                var right = ParseComparison();
                expr = new BinaryOpNode(opToken, expr, op, right);
            }
            return expr;
        }

        private AstNode ParseComparison()
        {
            var expr = ParseAddition();
            while (Match(TokenType.Greater, TokenType.GreaterEqual, TokenType.Less, TokenType.LessEqual))
            {
                var opToken = Previous();
                BinaryOperator op;
                switch (opToken.Type)
                {
                    case TokenType.Greater: op = BinaryOperator.Greater; break;
                    case TokenType.GreaterEqual: op = BinaryOperator.GreaterEqual; break;
                    case TokenType.Less: op = BinaryOperator.Less; break;
                    case TokenType.LessEqual: op = BinaryOperator.LessEqual; break;
                    default: throw new Exception("Unreachable.");
                }
                var right = ParseAddition();
                expr = new BinaryOpNode(opToken, expr, op, right);
            }
            return expr;
        }

        private AstNode ParseAddition()
        {
            var expr = ParseMultiplication();
            while (Match(TokenType.Plus, TokenType.Minus))
            {
                var opToken = Previous();
                var op = opToken.Type == TokenType.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
                var right = ParseMultiplication();
                expr = new BinaryOpNode(opToken, expr, op, right);
            }
            return expr;
        }

        private AstNode ParseMultiplication()
        {
            var expr = ParseUnary();
            while (Match(TokenType.Multiply, TokenType.Divide, TokenType.Percent))
            {
                var opToken = Previous();
                BinaryOperator op;
                switch (opToken.Type)
                {
                    case TokenType.Multiply: op = BinaryOperator.Multiply; break;
                    case TokenType.Divide: op = BinaryOperator.Divide; break;
                    case TokenType.Percent: op = BinaryOperator.Modulo; break;
                    default: throw new Exception("Unreachable.");
                }
                var right = ParseUnary();
                expr = new BinaryOpNode(opToken, expr, op, right);
            }
            return expr;
        }

        private AstNode ParseUnary()
        {
            if (Match(TokenType.Minus))
            {
                var opToken = Previous();
                var right = ParseUnary();
                return new UnaryOpNode(opToken, UnaryOperator.Negate, right);
            }
            return ParsePrimary();
        }

        private AstNode ParsePrimary()
        {
            if (Match(TokenType.Number)) return new LiteralNode(Previous(), double.Parse(Previous().Value!, CultureInfo.InvariantCulture));
            if (Match(TokenType.String)) return new LiteralNode(Previous(), Previous().Value!);
            if (Match(TokenType.Identifier))
            {
                var nameToken = Previous();
                var name = nameToken.Value!;
                if (Match(TokenType.LParen))
                {
                    return ParseFunctionCall(nameToken);
                }
                return new VariableNode(nameToken, name);
            }
            if (Match(TokenType.LParen))
            {
                var expr = ParseExpression();
                Consume(TokenType.RParen, "Expect ')' after expression.");
                return expr;
            }
            if (Match(TokenType.LBracket))
            {
                return ParseArrayLiteral();
            }

            var token = Peek();
            throw new EmelException(EmelErrorCode.Parser_ExpectedExpression, token.Line, token.Column);
        }

        private AstNode ParseArrayLiteral()
        {
            var lBracketToken = Previous();
            var elements = new List<AstNode>();
            if (!Check(TokenType.RBracket))
            {
                do
                {
                    elements.Add(ParseExpression());
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RBracket, "Expect ']' after array elements.");
            return new ArrayNode(lBracketToken, elements);
        }

        private AstNode ParseFunctionCall(Token nameToken)
        {
            var args = new List<AstNode>();
            if (!Check(TokenType.RParen))
            {
                do
                {
                    args.Add(ParseExpression());
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RParen, "Expect ')' after arguments.");
            return new FunctionCallNode(nameToken, nameToken.Value!, args);
        }
    }
}