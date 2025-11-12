using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MIDI.Voice.EMEL.Execution;
using MIDI.Voice.EMEL.Errors;
using MIDI.Voice.EMEL.Parsing;

namespace MIDI.Voice.EMEL.Generation
{
    public class EmelGenerator : IAstVisitor<object?>
    {
        public EmelContext Context { get; private set; }
        private readonly Dictionary<string, FunctionDefinition> _functions;
        private readonly Dictionary<string, FunctionDefinitionNode> _userFunctions;
        public StringBuilder Output { get; }

        private readonly Stack<AstNode> _nodeStack = new Stack<AstNode>();
        public AstNode CurrentNode => _nodeStack.Peek();

        public EmelGenerator(EmelContext context, Dictionary<string, FunctionDefinition> functions)
        {
            Context = context;
            _functions = functions;
            _userFunctions = new Dictionary<string, FunctionDefinitionNode>();
            Output = new StringBuilder();
        }

        public object? VisitProgramNode(ProgramNode node)
        {
            _nodeStack.Push(node);
            foreach (var funcDef in node.Statements.OfType<FunctionDefinitionNode>())
            {
                _userFunctions[funcDef.Name] = funcDef;
            }

            var global = node.Statements.OfType<GlobalBlockNode>().FirstOrDefault();
            if (global != null)
            {
                if (global.Block is BlockNode globalBlock)
                {
                    foreach (var funcDef in globalBlock.Statements.OfType<FunctionDefinitionNode>())
                    {
                        _userFunctions[funcDef.Name] = funcDef;
                    }
                }
                global.Accept(this);
            }

            foreach (var statement in node.Statements.OfType<TrackNode>())
            {
                statement.Accept(this);
            }
            _nodeStack.Pop();
            return null;
        }

        public object? VisitBlockNode(BlockNode node)
        {
            _nodeStack.Push(node);
            Context = new EmelContext(Context);
            foreach (var statement in node.Statements)
            {
                statement.Accept(this);
            }
            Context = Context.Parent ?? Context;
            _nodeStack.Pop();
            return null;
        }

        public object? VisitLetNode(LetNode node)
        {
            _nodeStack.Push(node);
            var value = node.Initializer.Accept(this);
            Context.Define(node.VariableName, value);
            _nodeStack.Pop();
            return null;
        }

        public object? VisitAssignNode(AssignNode node)
        {
            _nodeStack.Push(node);
            var value = node.Value.Accept(this);
            if (!Context.TryAssign(node.VariableName, value))
            {
                throw new EmelException(EmelErrorCode.Runtime_UndefinedVariable, node.StartToken.Line, node.StartToken.Column, node.VariableName);
            }
            _nodeStack.Pop();
            return null;
        }

        public object? VisitRepeatNode(RepeatNode node)
        {
            _nodeStack.Push(node);
            var countObj = node.CountExpression.Accept(this);
            if (!TryConvertToDouble(countObj, out double count))
            {
                throw new EmelException(EmelErrorCode.Runtime_InvalidTypeForRepeat, node.CountExpression.StartToken.Line, node.CountExpression.StartToken.Column);
            }

            int repeatCount = (int)count;
            if (repeatCount < 0)
            {
                throw new EmelException(EmelErrorCode.Runtime_ValueOutOfRange, node.CountExpression.StartToken.Line, node.CountExpression.StartToken.Column, "repeat回数", repeatCount, 0, "無制限");
            }

            Context = new EmelContext(Context);
            for (int i = 0; i < repeatCount; i++)
            {
                Context.Define("index", (double)i);
                node.Block.Accept(this);
            }
            Context = Context.Parent ?? Context;
            _nodeStack.Pop();
            return null;
        }

        public object? VisitIfNode(IfNode node)
        {
            _nodeStack.Push(node);
            var condition = node.Condition.Accept(this);
            if (IsTruthy(condition))
            {
                node.ThenBranch.Accept(this);
            }
            else if (node.ElseBranch != null)
            {
                node.ElseBranch.Accept(this);
            }
            _nodeStack.Pop();
            return null;
        }

        public object? VisitFunctionDefinitionNode(FunctionDefinitionNode node)
        {
            _nodeStack.Push(node);
            _nodeStack.Pop();
            return null;
        }

        public object? VisitFunctionCallNode(FunctionCallNode node)
        {
            _nodeStack.Push(node);
            var args = node.Arguments.Select(arg => arg.Accept(this)).ToList();

            if (_functions.TryGetValue(node.FunctionName, out var function))
            {
                if (args.Count != function.Arity)
                {
                    throw new EmelException(EmelErrorCode.Runtime_WrongArgumentCount, node.StartToken.Line, node.StartToken.Column, node.FunctionName, function.Arity, args.Count);
                }
                var result = function.Implementation.Invoke(this, node, args);
                _nodeStack.Pop();
                return result;
            }

            if (_userFunctions.TryGetValue(node.FunctionName, out var userFunc))
            {
                if (args.Count != userFunc.Parameters.Count)
                {
                    throw new EmelException(EmelErrorCode.Runtime_WrongArgumentCount, node.StartToken.Line, node.StartToken.Column, node.FunctionName, userFunc.Parameters.Count, args.Count);
                }

                var originalContext = Context;
                Context = new EmelContext(Context);
                for (int i = 0; i < userFunc.Parameters.Count; i++)
                {
                    Context.Define(userFunc.Parameters[i], args[i]);
                }
                userFunc.Body.Accept(this);
                Context = originalContext;
                _nodeStack.Pop();
                return null;
            }

            throw new EmelException(EmelErrorCode.Runtime_UndefinedFunction, node.StartToken.Line, node.StartToken.Column, node.FunctionName);
        }

        public object? VisitBinaryOpNode(BinaryOpNode node)
        {
            _nodeStack.Push(node);
            var left = node.Left.Accept(this);
            var right = node.Right.Accept(this);

            if (TryConvertToDouble(left, out double l) && TryConvertToDouble(right, out double r))
            {
                object? result = node.Operator switch
                {
                    BinaryOperator.Add => l + r,
                    BinaryOperator.Subtract => l - r,
                    BinaryOperator.Multiply => l * r,
                    BinaryOperator.Divide => r == 0 ? throw new EmelException(EmelErrorCode.Runtime_DivideByZero, node.StartToken.Line, node.StartToken.Column) : (object)(l / r),
                    BinaryOperator.Modulo => r == 0 ? throw new EmelException(EmelErrorCode.Runtime_DivideByZero, node.StartToken.Line, node.StartToken.Column) : (object)(l % r),
                    BinaryOperator.EqualEqual => l == r,
                    BinaryOperator.NotEqual => l != r,
                    BinaryOperator.Greater => l > r,
                    BinaryOperator.GreaterEqual => l >= r,
                    BinaryOperator.Less => l < r,
                    BinaryOperator.LessEqual => l <= r,
                    _ => throw new EmelException(EmelErrorCode.Runtime_InvalidTypeForOperation, node.StartToken.Line, node.StartToken.Column, node.Operator, left?.GetType()?.Name ?? "null", right?.GetType()?.Name ?? "null")
                };
                _nodeStack.Pop();
                return result;
            }

            if (node.Operator == BinaryOperator.LogicalAnd)
            {
                _nodeStack.Pop();
                return IsTruthy(left) && IsTruthy(right);
            }
            if (node.Operator == BinaryOperator.LogicalOr)
            {
                _nodeStack.Pop();
                return IsTruthy(left) || IsTruthy(right);
            }

            if (node.Operator == BinaryOperator.EqualEqual)
            {
                _nodeStack.Pop();
                return Equals(left, right);
            }
            if (node.Operator == BinaryOperator.NotEqual)
            {
                _nodeStack.Pop();
                return !Equals(left, right);
            }

            if (node.Operator == BinaryOperator.Add)
            {
                if (left is string pitch && TryConvertToDouble(right, out double semitones))
                {
                    _nodeStack.Pop();
                    return PitchMath.Transpose(pitch, semitones);
                }
                if (TryConvertToDouble(left, out double semitonesLeft) && right is string pitchRight)
                {
                    _nodeStack.Pop();
                    return PitchMath.Transpose(pitchRight, semitonesLeft);
                }
                if (left is string || right is string)
                {
                    _nodeStack.Pop();
                    return (left?.ToString() ?? "") + (right?.ToString() ?? "");
                }
            }

            if (node.Operator == BinaryOperator.Subtract)
            {
                if (left is string pitch && TryConvertToDouble(right, out double semitones))
                {
                    _nodeStack.Pop();
                    return PitchMath.Transpose(pitch, -semitones);
                }
            }

            throw new EmelException(EmelErrorCode.Runtime_InvalidTypeForOperation, node.StartToken.Line, node.StartToken.Column, node.Operator, left?.GetType()?.Name ?? "null", right?.GetType()?.Name ?? "null");
        }

        public object? VisitUnaryOpNode(UnaryOpNode node)
        {
            _nodeStack.Push(node);
            var operand = node.Operand.Accept(this);
            if (node.Operator == UnaryOperator.Negate)
            {
                if (operand is double d)
                {
                    _nodeStack.Pop();
                    return -d;
                }
                throw new EmelException(EmelErrorCode.Runtime_InvalidType, node.StartToken.Line, node.StartToken.Column, operand?.GetType()?.Name ?? "null");
            }
            _nodeStack.Pop();
            return null;
        }

        public object? VisitVariableNode(VariableNode node)
        {
            _nodeStack.Push(node);
            if (Context.TryGet(node.Name, out var value))
            {
                _nodeStack.Pop();
                return value;
            }
            throw new EmelException(EmelErrorCode.Runtime_UndefinedVariable, node.StartToken.Line, node.StartToken.Column, node.Name);
        }

        public object? VisitLiteralNode(LiteralNode node)
        {
            _nodeStack.Push(node);
            _nodeStack.Pop();
            return node.Value;
        }

        public object? VisitArrayNode(ArrayNode node)
        {
            _nodeStack.Push(node);
            var elements = node.Elements.Select(e => e.Accept(this)).ToList();
            _nodeStack.Pop();
            return elements;
        }

        public object? VisitTrackNode(TrackNode node)
        {
            _nodeStack.Push(node);
            Context = new EmelContext(Context);

            var trackNumObj = node.TrackNumber.Accept(this);
            if (!TryConvertToDouble(trackNumObj, out double trackNum))
            {
                throw new EmelException(EmelErrorCode.Runtime_InvalidType, node.TrackNumber.StartToken.Line, node.TrackNumber.StartToken.Column, trackNumObj?.GetType()?.Name ?? "null");
            }

            Output.AppendLine($"#!Track={(int)trackNum}");
            node.Block.Accept(this);

            Context = Context.Parent ?? Context;
            _nodeStack.Pop();
            return null;
        }

        public object? VisitGlobalBlockNode(GlobalBlockNode node)
        {
            _nodeStack.Push(node);
            if (node.Block is BlockNode block)
            {
                foreach (var statement in block.Statements)
                {
                    statement.Accept(this);
                }
            }
            else
            {
                node.Block.Accept(this);
            }
            _nodeStack.Pop();
            return null;
        }

        private bool IsTruthy(object? obj)
        {
            if (obj == null) return false;
            if (obj is bool b) return b;
            if (obj is double d) return d != 0.0;
            return true;
        }

        private bool TryConvertToDouble(object? obj, out double result)
        {
            if (obj is double d)
            {
                result = d;
                return true;
            }
            if (obj is int i)
            {
                result = i;
                return true;
            }
            result = 0;
            return false;
        }
    }
}