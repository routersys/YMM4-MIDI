using System.Collections.Generic;
using System.Linq;
using ICSharpCode.AvalonEdit.CodeCompletion;
using MIDI.Voice.EMEL.Execution;
using MIDI.Voice.EMEL.Parsing;
using MIDI.Tool.EMEL.ViewModel;

namespace MIDI.Tool.EMEL.Core
{
    public class EmelCompletionProvider
    {
        private readonly EmelEditorViewModel _viewModel;
        private readonly List<string> _keywords;
        private readonly List<string> _functions;

        public EmelCompletionProvider(EmelEditorViewModel viewModel)
        {
            _viewModel = viewModel;
            _keywords = new List<string> { "let", "repeat", "if", "else", "Track", "Global", "func" };

            var builtInFuncs = BuiltinFunctions.LoadDefinitions();
            _functions = builtInFuncs.Keys.ToList();
        }

        public IEnumerable<ICompletionData> GetCompletions(string code, int offset)
        {
            var completions = new List<ICompletionData>();

            completions.AddRange(_keywords.Select(k =>
                new EmelCompletionData(k, k switch
                {
                    "let" => "変数宣言キーワード",
                    "repeat" => "繰り返し処理キーワード",
                    "if" => "条件分岐キーワード",
                    "else" => "条件分岐の代替処理",
                    "Track" => "トラックブロックの開始",
                    "Global" => "グローバルブロックの開始",
                    "func" => "関数定義キーワード",
                    _ => "キーワード"
                })));

            completions.AddRange(_functions.Select(f => new EmelCompletionData(f, "組み込み関数")));

            try
            {
                var tokens = EmelParser.Lex(code);
                var parser = new EmelParser(tokens);
                var ast = parser.ParseProgram();

                var variables = CollectVariables(ast);
                completions.AddRange(variables.Select(v => new EmelCompletionData(v, "変数")));
            }
            catch
            {
            }

            return completions.OrderBy(c => c.Text);
        }

        private HashSet<string> CollectVariables(AstNode node)
        {
            var variables = new HashSet<string>();
            Collect(node, variables);
            return variables;
        }

        private void Collect(AstNode node, HashSet<string> variables)
        {
            if (node is LetNode letNode)
            {
                variables.Add(letNode.VariableName);
                Collect(letNode.Initializer, variables);
            }
            else if (node is AssignNode assignNode)
            {
                variables.Add(assignNode.VariableName);
                Collect(assignNode.Value, variables);
            }
            else if (node is FunctionDefinitionNode funcDefNode)
            {
                foreach (var param in funcDefNode.Parameters)
                {
                    variables.Add(param);
                }
                Collect(funcDefNode.Body, variables);
            }
            else if (node is ProgramNode progNode)
            {
                foreach (var stmt in progNode.Statements)
                {
                    Collect(stmt, variables);
                }
            }
            else if (node is BlockNode blockNode)
            {
                foreach (var stmt in blockNode.Statements)
                {
                    Collect(stmt, variables);
                }
            }
            else if (node is IfNode ifNode)
            {
                Collect(ifNode.Condition, variables);
                Collect(ifNode.ThenBranch, variables);
                if (ifNode.ElseBranch != null)
                {
                    Collect(ifNode.ElseBranch, variables);
                }
            }
            else if (node is RepeatNode repeatNode)
            {
                Collect(repeatNode.CountExpression, variables);
                Collect(repeatNode.Block, variables);
            }
            else if (node is TrackNode trackNode)
            {
                Collect(trackNode.TrackNumber, variables);
                Collect(trackNode.Block, variables);
            }
            else if (node is GlobalBlockNode globalNode)
            {
                Collect(globalNode.Block, variables);
            }
            else if (node is BinaryOpNode binOpNode)
            {
                Collect(binOpNode.Left, variables);
                Collect(binOpNode.Right, variables);
            }
            else if (node is UnaryOpNode unOpNode)
            {
                Collect(unOpNode.Operand, variables);
            }
            else if (node is FunctionCallNode callNode)
            {
                foreach (var arg in callNode.Arguments)
                {
                    Collect(arg, variables);
                }
            }
            else if (node is ArrayNode arrNode)
            {
                foreach (var el in arrNode.Elements)
                {
                    Collect(el, variables);
                }
            }
        }
    }
}