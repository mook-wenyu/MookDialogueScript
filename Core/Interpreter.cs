using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MookDialogueScript
{
    /// <summary>
    /// 表达式解释器，专注于表达式求值
    /// </summary>
    public class Interpreter
    {
        private readonly DialogueContext _context;
        private readonly Dictionary<string, Func<ExpressionNode, Task<RuntimeValue>>> _operators;

        public Interpreter(DialogueContext context)
        {
            _context = context;
            _operators = new Dictionary<string, Func<ExpressionNode, Task<RuntimeValue>>>
            {
                ["+"] = async (right) => new RuntimeValue(await GetNumberValue(right)),
                ["-"] = async (right) => new RuntimeValue(-await GetNumberValue(right)),
                ["!"] = async (right) => new RuntimeValue(!await GetBooleanValue(right))
            };
        }

        /// <summary>
        /// 评估表达式并返回运行时值
        /// </summary>
        /// <param name="node">表达式节点</param>
        /// <returns>计算结果的运行时值</returns>
        public async Task<RuntimeValue> EvaluateExpression(ExpressionNode node)
        {
            switch (node)
            {
                case NumberNode n:
                    return new RuntimeValue(n.Value);

                case StringNode s:
                    return new RuntimeValue(s.Value);

                case BooleanNode b:
                    return new RuntimeValue(b.Value);

                case VariableNode v:
                    return _context.GetVariable(v.Name);

                case UnaryOpNode u:
                    if (!_operators.TryGetValue(u.Operator, out var @operator))
                    {
                        Console.WriteLine($"未知的一元运算符 '{u.Operator}'");
                        return null;
                    }
                    return await @operator(u.Operand);

                case BinaryOpNode b:
                    var left = await EvaluateExpression(b.Left);
                    var right = await EvaluateExpression(b.Right);

                    // 如果任一操作数是函数调用的结果，确保类型匹配
                    if (b.Operator is "-" or "*" or "/" or "%" or ">" or "<" or ">=" or "<=")
                    {
                        if (left.Type != RuntimeValue.ValueType.Number)
                        {
                            Console.WriteLine($"运算符 '{b.Operator}' 的左操作数必须是数值类型");
                            return null;
                        }
                        if (right.Type != RuntimeValue.ValueType.Number)
                        {
                            Console.WriteLine($"运算符 '{b.Operator}' 的右操作数必须是数值类型");
                            return null;
                        }
                    }
                    else if (b.Operator is "&&" or "||")
                    {
                        if (left.Type != RuntimeValue.ValueType.Boolean)
                        {
                            Console.WriteLine($"运算符 '{b.Operator}' 的左操作数必须是布尔类型");
                            return null;
                        }
                        if (right.Type != RuntimeValue.ValueType.Boolean)
                        {
                            Console.WriteLine($"运算符 '{b.Operator}' 的右操作数必须是布尔类型");
                            return null;
                        }
                    }

                    switch (b.Operator)
                    {
                        case "+":
                            if (left.Type == RuntimeValue.ValueType.String || right.Type == RuntimeValue.ValueType.String)
                                return new RuntimeValue(left.ToString() + right.ToString());
                            return new RuntimeValue((double)left.Value + (double)right.Value);

                        case "-":
                            return new RuntimeValue((double)left.Value - (double)right.Value);

                        case "*":
                            return new RuntimeValue((double)left.Value * (double)right.Value);

                        case "/":
                            if ((double)right.Value == 0)
                            {
                                Console.WriteLine("除数不能为零");
                                return null;
                            }
                            return new RuntimeValue((double)left.Value / (double)right.Value);

                        case "%":
                            if ((double)right.Value == 0)
                            {
                                Console.WriteLine("取模运算的除数不能为零");
                                return null;
                            }
                            return new RuntimeValue((double)left.Value % (double)right.Value);

                        case "==":
                            if (left.Type != right.Type)
                                return null;
                            return new RuntimeValue(left.Value.Equals(right.Value));

                        case "!=":
                            if (left.Type != right.Type)
                                return null;
                            return new RuntimeValue(!left.Value.Equals(right.Value));

                        case ">":
                            return new RuntimeValue((double)left.Value > (double)right.Value);

                        case "<":
                            return new RuntimeValue((double)left.Value < (double)right.Value);

                        case ">=":
                            return new RuntimeValue((double)left.Value >= (double)right.Value);

                        case "<=":
                            return new RuntimeValue((double)left.Value <= (double)right.Value);

                        case "&&":
                            return new RuntimeValue((bool)left.Value && (bool)right.Value);

                        case "||":
                            return new RuntimeValue((bool)left.Value || (bool)right.Value);

                        default:
                            Console.WriteLine($"未知的二元运算符 '{b.Operator}'");
                            return null;
                    }

                case FunctionCallNode f:
                    var args = new List<RuntimeValue>();
                    // 递归评估每个参数，支持嵌套函数调用
                    foreach (var arg in f.Arguments)
                    {
                        args.Add(await EvaluateExpression(arg));
                    }
                    return await _context.CallFunction(f.Name, args);

                case InterpolationExpressionNode i:
                    var result = new System.Text.StringBuilder();
                    foreach (var segment in i.Segments)
                    {
                        switch (segment)
                        {
                            case TextNode t:
                                result.Append(t.Text);
                                break;

                            case InterpolationNode interpolation:
                                try
                                {
                                    var value = await EvaluateExpression(interpolation.Expression);
                                    result.Append(value.ToString());
                                }
                                catch (Exception)
                                {
                                    // 如果变量或函数未找到，保留原始插值文本
                                    result.Append($"{{{interpolation.Expression}}}");
                                }
                                break;
                        }
                    }
                    return new RuntimeValue(result.ToString());

                default:
                    Console.WriteLine($"未知的表达式类型 {node.GetType().Name}");
                    return null;
            }
        }

        /// <summary>
        /// 获取表达式的数值结果
        /// </summary>
        /// <param name="expression">表达式节点</param>
        /// <returns>数值</returns>
        public async Task<double> GetNumberValue(ExpressionNode expression)
        {
            var value = await EvaluateExpression(expression);
            if (value.Type != RuntimeValue.ValueType.Number)
            {
                Console.WriteLine("表达式必须计算为数值类型");
                return 0;
            }
            return (double)value.Value;
        }

        /// <summary>
        /// 获取表达式的布尔结果
        /// </summary>
        /// <param name="node">表达式节点</param>
        /// <returns>布尔值</returns>
        public async Task<bool> GetBooleanValue(ExpressionNode node)
        {
            var value = await EvaluateExpression(node);
            if (value.Type != RuntimeValue.ValueType.Boolean)
            {
                Console.WriteLine("表达式必须计算为布尔类型");
                return false;
            }
            return (bool)value.Value;
        }

        /// <summary>
        /// 获取表达式的字符串结果
        /// </summary>
        /// <param name="node">表达式节点</param>
        /// <returns>字符串</returns>
        public async Task<string> GetStringValue(ExpressionNode node)
        {
            var value = await EvaluateExpression(node);
            if (value.Type != RuntimeValue.ValueType.String)
            {
                Console.WriteLine("表达式必须计算为字符串类型");
                return string.Empty;
            }
            return (string)value.Value;
        }

        /// <summary>
        /// 构建文本
        /// </summary>
        /// <param name="segments">文本段列表</param>
        /// <returns>构建后的文本</returns>
        public async Task<string> BuildText(List<TextSegmentNode> segments)
        {
            var result = new System.Text.StringBuilder();
            foreach (var segment in segments)
            {
                switch (segment)
                {
                    case TextNode t:
                        result.Append(t.Text);
                        break;

                    case InterpolationNode i:
                        try
                        {
                            var value = await EvaluateExpression(i.Expression);
                            result.Append(value.ToString());
                        }
                        catch (Exception)
                        {
                            // 如果变量或函数未找到，保留原始插值文本
                            result.Append($"{i.Expression}");
                        }
                        break;
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// 注册脚本中的所有节点
        /// </summary>
        /// <param name="script">脚本</param>
        public void RegisterNodes(ScriptNode script)
        {
            // 注册所有节点
            foreach (var node in script.Nodes)
            {
                _context.RegisterNode(node.NodeName, node);
            }
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="command">命令节点</param>
        /// <returns>如果是跳转命令则返回目标节点名称，否则返回空字符串</returns>
        public async Task<string> ExecuteCommand(CommandNode command)
        {
            switch (command)
            {
                case VarCommandNode v:
                    var value = await EvaluateExpression(v.Value);
                    switch (v.Operation.ToLower())
                    {
                        case "set":
                            _context.SetVariable(v.Variable, value);
                            break;

                        case "add":
                            var current = _context.GetVariable(v.Variable);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                Console.WriteLine("Add操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.Variable, new RuntimeValue((double)current.Value + (double)value.Value));
                            break;

                        case "sub":
                            current = _context.GetVariable(v.Variable);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                Console.WriteLine("Sub操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.Variable, new RuntimeValue((double)current.Value - (double)value.Value));
                            break;

                        case "mul":
                            current = _context.GetVariable(v.Variable);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                Console.WriteLine("Mul操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.Variable, new RuntimeValue((double)current.Value * (double)value.Value));
                            break;

                        case "div":
                            current = _context.GetVariable(v.Variable);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                Console.WriteLine("Div操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.Variable, new RuntimeValue((double)current.Value / (double)value.Value));
                            break;

                        case "mod":
                            current = _context.GetVariable(v.Variable);
                            if (current.Type != RuntimeValue.ValueType.Number || value.Type != RuntimeValue.ValueType.Number)
                            {
                                Console.WriteLine("Mod操作需要数值类型");
                                return string.Empty;
                            }
                            _context.SetVariable(v.Variable, new RuntimeValue((double)current.Value % (double)value.Value));
                            break;

                        default:
                            Console.WriteLine($"未知的变量操作 '{v.Operation}'");
                            return string.Empty;
                    }
                    return string.Empty;

                case CallCommandNode c:
                    var args = new List<RuntimeValue>();
                    foreach (var arg in c.Parameters)
                    {
                        args.Add(await EvaluateExpression(arg));
                    }
                    await _context.CallFunction(c.FunctionName, args);
                    return string.Empty;

                case WaitCommandNode w:
                    double duration = await GetNumberValue(w.Duration);
                    await Task.Delay(TimeSpan.FromSeconds(duration));
                    return string.Empty;

                case JumpCommandNode j:
                    return j.TargetNode;

                default:
                    Console.WriteLine($"未知的命令类型 {command.GetType().Name}");
                    return string.Empty;
            }
        }
    }
}