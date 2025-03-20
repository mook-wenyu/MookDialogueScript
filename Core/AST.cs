using System.Collections.Generic;

namespace MookDialogueScript
{
    /// <summary>
    /// 抽象语法树节点
    /// </summary>
    public abstract class ASTNode
    {
        /// <summary>
        /// 行号
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// 列号
        /// </summary>
        public int Column { get; }

        protected ASTNode(int line, int column)
        {
            Line = line;
            Column = column;
        }
    }

    /// <summary>
    /// 脚本节点
    /// </summary>
    public class ScriptNode : ASTNode
    {
        /// <summary>
        /// 节点定义列表
        /// </summary>
        public List<NodeDefinitionNode> Nodes { get; }

        public ScriptNode(List<NodeDefinitionNode> nodes, int line, int column)
            : base(line, column)
        {
            Nodes = nodes;
        }
    }

    /// <summary>
    /// 节点定义节点
    /// </summary>
    public class NodeDefinitionNode : ASTNode
    {
        /// <summary>
        /// 节点名称
        /// </summary>
        public string NodeName { get; }

        /// <summary>
        /// 内容列表
        /// </summary>
        public List<ContentNode> Content { get; }

        public NodeDefinitionNode(string nodeName, List<ContentNode> content, int line, int column)
            : base(line, column)
        {
            NodeName = nodeName;
            Content = content;
        }
    }

    /// <summary>
    /// 内容节点
    /// </summary>
    public abstract class ContentNode : ASTNode
    {
        protected ContentNode(int line, int column) : base(line, column) { }
    }

    /// <summary>
    /// 对话节点
    /// </summary>
    public class DialogueNode : ContentNode
    {
        /// <summary>
        /// 说话者
        /// </summary>
        public string Speaker { get; }

        /// <summary>
        /// 表情
        /// </summary>
        public string? Emotion { get; }

        /// <summary>
        /// 文本列表
        /// </summary>
        public List<TextSegmentNode> Text { get; }

        /// <summary>
        /// 标签列表
        /// </summary>
        public List<string>? Labels { get; }

        public DialogueNode(string speaker, string? emotion, List<TextSegmentNode> text, List<string>? labels, int line, int column)
            : base(line, column)
        {
            Speaker = speaker;
            Emotion = emotion;
            Text = text;
            Labels = labels;
        }
    }

    /// <summary>
    /// 旁白节点
    /// </summary>
    public class NarrationNode : ContentNode
    {
        /// <summary>
        /// 文本列表
        /// </summary>
        public List<TextSegmentNode> Text { get; }

        /// <summary>
        /// 标签列表
        /// </summary>
        public List<string>? Labels { get; }

        public NarrationNode(List<TextSegmentNode> text, List<string>? labels, int line, int column)
            : base(line, column)
        {
            Text = text;
            Labels = labels;
        }
    }

    /// <summary>
    /// 文本段节点
    /// </summary>
    public abstract class TextSegmentNode : ASTNode
    {
        protected TextSegmentNode(int line, int column) : base(line, column) { }
    }

    /// <summary>
    /// 文本节点
    /// </summary>
    public class TextNode : TextSegmentNode
    {
        /// <summary>
        /// 文本
        /// </summary>
        public string Text { get; }

        public TextNode(string text, int line, int column)
            : base(line, column)
        {
            Text = text;
        }
    }

    /// <summary>
    /// 插值节点
    /// </summary>
    public class InterpolationNode : TextSegmentNode
    {
        /// <summary>
        /// 表达式
        /// </summary>
        public ExpressionNode Expression { get; }

        public InterpolationNode(ExpressionNode expression, int line, int column)
            : base(line, column)
        {
            Expression = expression;
        }
    }

    /// <summary>
    /// 选项节点
    /// </summary>
    public class ChoiceNode : ContentNode
    {
        /// <summary>
        /// 文本列表
        /// </summary>
        public List<TextSegmentNode> Text { get; }

        /// <summary>
        /// 条件
        /// </summary>
        public ExpressionNode? Condition { get; }

        /// <summary>
        /// 内容列表
        /// </summary>
        public List<ContentNode> Content { get; }

        public ChoiceNode(List<TextSegmentNode> text, ExpressionNode? condition, List<ContentNode> content, int line, int column)
            : base(line, column)
        {
            Text = text;
            Condition = condition;
            Content = content;
        }
    }

    /// <summary>
    /// 条件节点
    /// </summary>
    public class ConditionNode : ContentNode
    {
        /// <summary>
        /// 条件
        /// </summary>
        public ExpressionNode Condition { get; }

        /// <summary>
        /// 然后分支
        /// </summary>
        public List<ContentNode> ThenBranch { get; }

        /// <summary>
        /// 否则如果分支
        /// </summary>
        public List<(ExpressionNode Condition, List<ContentNode> Content)> ElifBranches { get; }

        /// <summary>
        /// 否则分支
        /// </summary>
        public List<ContentNode>? ElseBranch { get; }

        public ConditionNode(
            ExpressionNode condition,
            List<ContentNode> thenBranch,
            List<(ExpressionNode Condition, List<ContentNode> Content)> elifBranches,
            List<ContentNode>? elseBranch,
            int line, int column)
            : base(line, column)
        {
            Condition = condition;
            ThenBranch = thenBranch;
            ElifBranches = elifBranches;
            ElseBranch = elseBranch;
        }
    }

    /// <summary>
    /// 命令节点
    /// </summary>
    public abstract class CommandNode : ContentNode
    {
        protected CommandNode(int line, int column) : base(line, column) { }
    }

    /// <summary>
    /// 变量命令节点
    /// </summary>
    public class VarCommandNode : CommandNode
    {
        /// <summary>
        /// 变量
        /// </summary>
        public string Variable { get; }

        /// <summary>
        /// 值
        /// </summary>
        public ExpressionNode Value { get; }

        /// <summary>
        /// 操作
        /// </summary>
        public string Operation { get; } // set, add, sub, mul, div, mod
        public VarCommandNode(string variable, ExpressionNode value, string operation, int line, int column)
            : base(line, column)
        {
            Variable = variable;
            Value = value;
            Operation = operation;
        }
    }

    /// <summary>
    /// 调用命令节点
    /// </summary>
    public class CallCommandNode : CommandNode
    {
        /// <summary>
        /// 函数名称
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// 参数列表
        /// </summary>
        public List<ExpressionNode> Parameters { get; }

        public CallCommandNode(string functionName, List<ExpressionNode> parameters, int line, int column)
            : base(line, column)
        {
            FunctionName = functionName;
            Parameters = parameters;
        }
    }

    /// <summary>
    /// 等待命令节点
    /// </summary>
    public class WaitCommandNode : CommandNode
    {
        /// <summary>
        /// 持续时间
        /// </summary>
        public ExpressionNode Duration { get; }

        public WaitCommandNode(ExpressionNode duration, int line, int column)
            : base(line, column)
        {
            Duration = duration;
        }
    }

    /// <summary>
    /// 跳转命令节点
    /// </summary>
    public class JumpCommandNode : CommandNode
    {
        /// <summary>
        /// 目标节点
        /// </summary>
        public string TargetNode { get; }

        public JumpCommandNode(string targetNode, int line, int column)
            : base(line, column)
        {
            TargetNode = targetNode;
        }
    }

    /// <summary>
    /// 表达式节点
    /// </summary>
    public abstract class ExpressionNode : ASTNode
    {
        protected ExpressionNode(int line, int column) : base(line, column) { }
    }

    /// <summary>
    /// 二元运算符节点
    /// </summary>
    public class BinaryOpNode : ExpressionNode
    {
        /// <summary>
        /// 左操作数
        /// </summary>
        public ExpressionNode Left { get; }

        /// <summary>
        /// 运算符
        /// </summary>
        public string Operator { get; }

        /// <summary>
        /// 右操作数
        /// </summary>
        public ExpressionNode Right { get; }

        public BinaryOpNode(ExpressionNode left, string op, ExpressionNode right, int line, int column)
            : base(line, column)
        {
            Left = left;
            Operator = op;
            Right = right;
        }
    }

    /// <summary>
    /// 一元运算符节点
    /// </summary>
    public class UnaryOpNode : ExpressionNode
    {
        /// <summary>
        /// 运算符
        /// </summary>
        public string Operator { get; }

        /// <summary>
        /// 操作数
        /// </summary>
        public ExpressionNode Operand { get; }

        public UnaryOpNode(string op, ExpressionNode operand, int line, int column)
            : base(line, column)
        {
            Operator = op;
            Operand = operand;
        }
    }

    /// <summary>
    /// 数字节点
    /// </summary>
    public class NumberNode : ExpressionNode
    {
        /// <summary>
        /// 值
        /// </summary>
        public double Value { get; }

        public NumberNode(double value, int line, int column)
            : base(line, column)
        {
            Value = value;
        }
    }

    /// <summary>
    /// 字符串节点
    /// </summary>
    public class StringNode : ExpressionNode
    {
        /// <summary>
        /// 值
        /// </summary>
        public string Value { get; }

        public StringNode(string value, int line, int column)
            : base(line, column)
        {
            Value = value;
        }
    }

    /// <summary>
    /// 布尔节点
    /// </summary>
    public class BooleanNode : ExpressionNode
    {
        /// <summary>
        /// 值
        /// </summary>
        public bool Value { get; }

        public BooleanNode(bool value, int line, int column)
            : base(line, column)
        {
            Value = value;
        }
    }

    /// <summary>
    /// 变量节点
    /// </summary>
    public class VariableNode : ExpressionNode
    {
        /// <summary>
        /// 变量名（不包含$符号，$符号在词法分析阶段已被处理）
        /// </summary>
        public string Name { get; }

        public VariableNode(string name, int line, int column)
            : base(line, column)
        {
            Name = name;
        }
    }

    /// <summary>
    /// 标识符节点
    /// </summary>
    public class IdentifierNode : ExpressionNode
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; }

        public IdentifierNode(string name, int line, int column)
            : base(line, column)
        {
            Name = name;
        }
    }

    /// <summary>
    /// 函数调用节点
    /// </summary>
    public class FunctionCallNode : ExpressionNode
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 参数列表
        /// </summary>
        public List<ExpressionNode> Arguments { get; }

        public FunctionCallNode(string name, List<ExpressionNode> arguments, int line, int column)
            : base(line, column)
        {
            Name = name;
            Arguments = arguments;
        }

        public override string ToString()
        {
            return $"{Name}({string.Join(", ", Arguments)})";
        }
    }

    /// <summary>
    /// 字符串插值表达式节点
    /// </summary>
    public class InterpolationExpressionNode : ExpressionNode
    {
        /// <summary>
        /// 文本段列表
        /// </summary>
        public List<TextSegmentNode> Segments { get; }

        public InterpolationExpressionNode(List<TextSegmentNode> segments, int line, int column)
            : base(line, column)
        {
            Segments = segments;
        }
    }
}