using MIDI.Voice.EMEL.Errors;
using System.Collections.Generic;
using static MIDI.Voice.EMEL.Parsing.EmelParser;

namespace MIDI.Voice.EMEL.Parsing
{
    public abstract class AstNode
    {
        public Token StartToken { get; }

        public AstNode(Token startToken)
        {
            StartToken = startToken;
        }

        public abstract T Accept<T>(IAstVisitor<T> visitor);
    }

    public interface IAstVisitor<T>
    {
        T VisitProgramNode(ProgramNode node);
        T VisitBlockNode(BlockNode node);
        T VisitLetNode(LetNode node);
        T VisitAssignNode(AssignNode node);
        T VisitRepeatNode(RepeatNode node);
        T VisitIfNode(IfNode node);
        T VisitFunctionDefinitionNode(FunctionDefinitionNode node);
        T VisitFunctionCallNode(FunctionCallNode node);
        T VisitBinaryOpNode(BinaryOpNode node);
        T VisitUnaryOpNode(UnaryOpNode node);
        T VisitVariableNode(VariableNode node);
        T VisitLiteralNode(LiteralNode node);
        T VisitArrayNode(ArrayNode node);
        T VisitTrackNode(TrackNode node);
        T VisitGlobalBlockNode(GlobalBlockNode node);
    }

    public class ProgramNode : AstNode
    {
        public List<AstNode> Statements { get; } = new List<AstNode>();
        public ProgramNode(Token startToken) : base(startToken) { }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitProgramNode(this);
    }

    public class BlockNode : AstNode
    {
        public List<AstNode> Statements { get; } = new List<AstNode>();
        public BlockNode(Token startToken) : base(startToken) { }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBlockNode(this);
    }

    public class LetNode : AstNode
    {
        public string VariableName { get; }
        public AstNode Initializer { get; }
        public LetNode(Token startToken, string variableName, AstNode initializer) : base(startToken)
        {
            VariableName = variableName;
            Initializer = initializer;
        }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitLetNode(this);
    }

    public class AssignNode : AstNode
    {
        public string VariableName { get; }
        public AstNode Value { get; }
        public AssignNode(Token startToken, string variableName, AstNode value) : base(startToken)
        {
            VariableName = variableName;
            Value = value;
        }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitAssignNode(this);
    }

    public class RepeatNode : AstNode
    {
        public AstNode CountExpression { get; }
        public AstNode Block { get; }
        public RepeatNode(Token startToken, AstNode countExpression, AstNode block) : base(startToken)
        {
            CountExpression = countExpression;
            Block = block;
        }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitRepeatNode(this);
    }

    public class IfNode : AstNode
    {
        public AstNode Condition { get; }
        public AstNode ThenBranch { get; }
        public AstNode? ElseBranch { get; }
        public IfNode(Token startToken, AstNode condition, AstNode thenBranch, AstNode? elseBranch) : base(startToken)
        {
            Condition = condition;
            ThenBranch = thenBranch;
            ElseBranch = elseBranch;
        }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitIfNode(this);
    }

    public class FunctionDefinitionNode : AstNode
    {
        public string Name { get; }
        public List<string> Parameters { get; }
        public AstNode Body { get; }
        public FunctionDefinitionNode(Token startToken, string name, List<string> parameters, AstNode body) : base(startToken)
        {
            Name = name;
            Parameters = parameters;
            Body = body;
        }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFunctionDefinitionNode(this);
    }

    public class FunctionCallNode : AstNode
    {
        public string FunctionName { get; }
        public List<AstNode> Arguments { get; }
        public FunctionCallNode(Token startToken, string functionName, List<AstNode> arguments) : base(startToken)
        {
            FunctionName = functionName;
            Arguments = arguments;
        }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFunctionCallNode(this);
    }

    public enum BinaryOperator
    {
        Add, Subtract, Multiply, Divide, Modulo,
        LogicalOr, LogicalAnd,
        EqualEqual, NotEqual,
        Greater, GreaterEqual,
        Less, LessEqual
    }

    public class BinaryOpNode : AstNode
    {
        public AstNode Left { get; }
        public BinaryOperator Operator { get; }
        public AstNode Right { get; }
        public BinaryOpNode(Token startToken, AstNode left, BinaryOperator op, AstNode right) : base(startToken)
        {
            Left = left;
            Operator = op;
            Right = right;
        }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBinaryOpNode(this);
    }

    public enum UnaryOperator { Negate }

    public class UnaryOpNode : AstNode
    {
        public UnaryOperator Operator { get; }
        public AstNode Operand { get; }
        public UnaryOpNode(Token startToken, UnaryOperator op, AstNode operand) : base(startToken)
        {
            Operator = op;
            Operand = operand;
        }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitUnaryOpNode(this);
    }

    public class VariableNode : AstNode
    {
        public string Name { get; }
        public VariableNode(Token startToken, string name) : base(startToken) { Name = name; }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitVariableNode(this);
    }

    public class LiteralNode : AstNode
    {
        public object Value { get; }
        public LiteralNode(Token startToken, object value) : base(startToken) { Value = value; }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitLiteralNode(this);
    }

    public class ArrayNode : AstNode
    {
        public List<AstNode> Elements { get; }
        public ArrayNode(Token startToken, List<AstNode> elements) : base(startToken)
        {
            Elements = elements;
        }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitArrayNode(this);
    }

    public class TrackNode : AstNode
    {
        public AstNode TrackNumber { get; }
        public AstNode Block { get; }
        public TrackNode(Token startToken, AstNode trackNumber, AstNode block) : base(startToken)
        {
            TrackNumber = trackNumber;
            Block = block;
        }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitTrackNode(this);
    }

    public class GlobalBlockNode : AstNode
    {
        public AstNode Block { get; }
        public GlobalBlockNode(Token startToken, AstNode block) : base(startToken)
        {
            Block = block;
        }
        public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitGlobalBlockNode(this);
    }
}