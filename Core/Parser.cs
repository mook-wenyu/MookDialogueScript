using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace MookDialogueScript
{
    /// <summary>
    /// 语法错误异常
    /// </summary>
    public class SyntaxErrorException : Exception
    {
        public int Line { get; }
        public int Column { get; }

        public SyntaxErrorException(string message) : base(message)
        {
        }

        public SyntaxErrorException(string message, int line, int column) : base(message)
        {
            Line = line;
            Column = column;
        }

        public SyntaxErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class Parser
    {
        private readonly Lexer _lexer;
        private Token _currentToken;
        private readonly List<Token> _tokens;
        private int _tokenIndex;
        private readonly bool _useTokenList;

        public Parser(Lexer lexer)
        {
            _lexer = lexer;
            _tokens = null;
            _tokenIndex = 0;
            _useTokenList = false;
            _currentToken = _lexer.GetNextToken();
        }

        public Parser(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                Logger.LogError("Token list cannot be null or empty");
                throw new ArgumentException("Token list cannot be null or empty");
            }

            _lexer = null;
            _tokens = tokens;
            _tokenIndex = 0;
            _useTokenList = true;
            _currentToken = _tokens[0];
        }

        /// <summary>
        /// 消耗一个Token
        /// </summary>
        private void Consume(TokenType type)
        {
            if (_currentToken.Type == type)
            {
                GetNextToken();
            }
            else if (_currentToken.Type == TokenType.EOF && type == TokenType.NEWLINE)
            {
                // 如果期望换行符但遇到了EOF，视为合法情况
                // 不需要获取下一个Token，因为已经到达文件末尾
            }
            else
            {
                Logger.LogError($"Syntax error: Expected {type}, got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                throw new SyntaxErrorException($"Syntax error: Expected {type}, got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
            }
        }

        /// <summary>
        /// 获取下一个Token
        /// </summary>
        private void GetNextToken()
        {
            if (_useTokenList)
            {
                _tokenIndex++;
                if (_tokenIndex < _tokens.Count)
                {
                    _currentToken = _tokens[_tokenIndex];
                }
                else
                {
                    _currentToken = new Token(TokenType.EOF, "", _tokens[_tokens.Count - 1].Line, _tokens[_tokens.Count - 1].Column);
                }
            }
            else
            {
                _currentToken = _lexer.GetNextToken();
            }
        }

        /// <summary>
        /// 查看当前Token类型
        /// </summary>
        private bool Check(TokenType type)
        {
            return _currentToken.Type == type;
        }

        /// <summary>
        /// 查看下一个Token类型
        /// </summary>
        private bool CheckNext(TokenType type)
        {
            if (_useTokenList)
            {
                if (_tokenIndex + 1 < _tokens.Count)
                {
                    return _tokens[_tokenIndex + 1].Type == type;
                }
                return false;
            }
            else
            {
                // 使用Lexer的PeekNextToken方法，但不缓存结果
                Token nextToken = _lexer.PeekNextToken();
                return nextToken.Type == type;
            }
        }

        /// <summary>
        /// 匹配并消耗一个Token
        /// </summary>
        private bool Match(TokenType type)
        {
            if (Check(type))
            {
                GetNextToken();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 解析脚本
        /// </summary>
        public ScriptNode Parse()
        {
            var nodes = new List<NodeDefinitionNode>();

            while (_currentToken.Type != TokenType.EOF)
            {
                if (_currentToken.Type == TokenType.DOUBLE_COLON)
                {
                    var node = ParseNodeDefinition();
                    
                    // 如果节点内容为空，则跳过该节点
                    if (node.Content.Count > 0)
                    {
                        nodes.Add(node);
                    }
                }
                else if (_currentToken.Type == TokenType.NEWLINE)
                {
                    Consume(TokenType.NEWLINE);
                }
                else
                {
                    Logger.LogError($"Unexpected token {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                    throw new SyntaxErrorException($"Unexpected token {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                }
            }

            return new ScriptNode(nodes, 1, 1);
        }

        /// <summary>
        /// 解析节点定义
        /// </summary>
        private NodeDefinitionNode ParseNodeDefinition()
        {
            Consume(TokenType.DOUBLE_COLON);
            if (_currentToken.Type != TokenType.IDENTIFIER)
            {
                Logger.LogError($"Expected node name but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                throw new SyntaxErrorException($"Expected node name but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
            }

            string nodeName = _currentToken.Value;
            int line = _currentToken.Line;
            int column = _currentToken.Column;

            Consume(TokenType.IDENTIFIER);
            Consume(TokenType.NEWLINE);

            var content = new List<ContentNode>();
            while (_currentToken.Type != TokenType.EOF && _currentToken.Type != TokenType.DOUBLE_COLON)
            {
                if (_currentToken.Type == TokenType.NEWLINE)
                {
                    Consume(TokenType.NEWLINE);
                    continue;
                }
                
                ContentNode node = ParseContent();
                // 跳过空内容节点，例如只包含空文本的旁白
                if (IsEmptyContentNode(node))
                {
                    continue;
                }
                
                content.Add(node);
            }

            return new NodeDefinitionNode(nodeName, content, line, column);
        }
        
        /// <summary>
        /// 检查内容节点是否为空
        /// </summary>
        private bool IsEmptyContentNode(ContentNode node)
        {
            // 对于旁白节点，检查是否只包含空文本
            if (node is NarrationNode narration)
            {
                // 如果没有文本段落，或者所有文本段落都是空的
                return narration.Text.Count == 0 || 
                       narration.Text.All(segment => segment is TextNode textNode && string.IsNullOrWhiteSpace(textNode.Text));
            }
            
            // 对于对话节点，检查是否只包含空文本
            if (node is DialogueNode dialogue)
            {
                // 如果没有文本段落，或者所有文本段落都是空的
                return dialogue.Text.Count == 0 || 
                       dialogue.Text.All(segment => segment is TextNode textNode && string.IsNullOrWhiteSpace(textNode.Text));
            }
            
            // 对于选择节点，检查是否没有内容
            if (node is ChoiceNode choice)
            {
                // 首先检查选择文本是否为空
                bool emptyText = choice.Text.Count == 0 || 
                                choice.Text.All(segment => segment is TextNode textNode && string.IsNullOrWhiteSpace(textNode.Text));
                
                // 如果选择文本和内容都是空的，则认为此节点为空
                return emptyText && (choice.Content == null || choice.Content.Count == 0);
            }
            
            // 对于条件节点，检查是否没有内容
            if (node is ConditionNode condition)
            {
                return (condition.ThenBranch == null || condition.ThenBranch.Count == 0) &&
                       (condition.ElifBranches == null || condition.ElifBranches.Count == 0) &&
                       (condition.ElseBranch == null || condition.ElseBranch.Count == 0);
            }
            
            // 其他类型的节点通常不会是空的
            return false;
        }

        private ContentNode ParseContent()
        {
            switch (_currentToken.Type)
            {
                case TokenType.TEXT:
                case TokenType.IDENTIFIER:  // 增加对IDENTIFIER类型的支持，使其也被识别为对话的开始
                    // 如果后面有冒号，则认为是对话
                    if (CheckNext(TokenType.LEFT_BRACKET) || CheckNext(TokenType.COLON))
                    {
                        return ParseDialogue();
                    }
                    return ParseNarration();
                case TokenType.ARROW:
                    return ParseChoice();
                case TokenType.IF:
                    return ParseCondition();
                case TokenType.ENDIF:
                    Logger.LogError($"Unexpected ENDIF at line {_currentToken.Line}, column {_currentToken.Column}");
                    throw new SyntaxErrorException($"Unexpected ENDIF at line {_currentToken.Line}, column {_currentToken.Column}");
                case TokenType.COMMAND:
                case TokenType.SET:
                case TokenType.ADD:
                case TokenType.SUB:
                case TokenType.MUL:
                case TokenType.DIV:
                case TokenType.MOD:
                case TokenType.CALL:
                case TokenType.WAIT:
                case TokenType.VAR:
                case TokenType.JUMP:
                    return ParseCommand();
                default:
                    return ParseNarration();
            }
        }

        /// <summary>
        /// 解析对话
        /// </summary>
        private DialogueNode ParseDialogue()
        {
            string speaker = _currentToken.Value;
            int line = _currentToken.Line;
            int column = _currentToken.Column;
            
            // 消耗当前token，可能是TEXT或IDENTIFIER
            Consume(_currentToken.Type);

            string? emotion = null;
            // 记录扫描到的位置，用于防止重复处理同一个LEFT_BRACKET
            int lastBracketLine = -1;
            int lastBracketColumn = -1;
            
            if (_currentToken.Type == TokenType.LEFT_BRACKET)
            {
                // 记录当前LEFT_BRACKET的位置
                lastBracketLine = _currentToken.Line;
                lastBracketColumn = _currentToken.Column;
                
                // 直接消耗左括号
                GetNextToken();
                
                // 防止重复处理同一个LEFT_BRACKET
                if (_currentToken.Type == TokenType.LEFT_BRACKET && 
                    _currentToken.Line == lastBracketLine && 
                    _currentToken.Column == lastBracketColumn)
                {
                    // 这是重复的LEFT_BRACKET，跳过它
                    Logger.LogError($"Skipping duplicate LEFT_BRACKET at line {_currentToken.Line}, column {_currentToken.Column}");
                    GetNextToken();
                }
                
                if (_currentToken.Type == TokenType.IDENTIFIER || _currentToken.Type == TokenType.TEXT)
                {
                    emotion = _currentToken.Value;
                    GetNextToken();
                }
                else
                {
                    Logger.LogError($"Expected emotion but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                    throw new SyntaxErrorException($"Expected emotion but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                }
                
                if (_currentToken.Type == TokenType.RIGHT_BRACKET)
                {
                    GetNextToken();
                }
                else
                {
                    Logger.LogError($"Expected right bracket but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                    throw new SyntaxErrorException($"Expected right bracket but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                }
            }

            if (_currentToken.Type == TokenType.COLON)
            {
                GetNextToken();
            }
            else
            {
                Logger.LogError($"Expected colon but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                throw new SyntaxErrorException($"Expected colon but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
            }

            var text = ParseText();

            List<string>? labels = null;
            while (_currentToken.Type == TokenType.HASH)
            {
                if (labels == null)
                {
                    labels = new List<string>();
                }
                
                // 消耗HASH标记
                Consume(TokenType.HASH);
                
                // 获取标签名称（应该是IDENTIFIER）
                if (_currentToken.Type != TokenType.IDENTIFIER)
                {
                    Logger.LogError($"Expected label name but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                    throw new SyntaxErrorException($"Expected label name but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                }
                
                labels.Add(_currentToken.Value);
                Consume(TokenType.IDENTIFIER);
            }

            // 处理文件末尾情况
            if (_currentToken.Type != TokenType.EOF)
            {
                Consume(TokenType.NEWLINE);
            }

            return new DialogueNode(speaker, emotion, text, labels, line, column);
        }

        private List<TextSegmentNode> ParseText()
        {
            var segments = new List<TextSegmentNode>();
            StringBuilder textBuilder = new StringBuilder();

            while (_currentToken.Type != TokenType.NEWLINE && _currentToken.Type != TokenType.HASH)
            {
                if (_currentToken.Type == TokenType.LEFT_BRACE)
                {
                    if (textBuilder.Length > 0)
                    {
                        segments.Add(new TextNode(textBuilder.ToString(), _currentToken.Line, _currentToken.Column));
                        textBuilder.Clear();
                    }
                    segments.Add(ParseInterpolation());
                }
                else if (_currentToken.Type == TokenType.TEXT)
                {
                    if (textBuilder.Length > 0)
                    {
                        segments.Add(new TextNode(textBuilder.ToString().TrimEnd(), _currentToken.Line, _currentToken.Column));
                        textBuilder.Clear();
                    }
                    segments.Add(new TextNode(_currentToken.Value, _currentToken.Line, _currentToken.Column));
                    Consume(TokenType.TEXT);
                }
                else if (_currentToken.Type == TokenType.STRING ||
                         _currentToken.Type == TokenType.IDENTIFIER ||
                         _currentToken.Type == TokenType.NUMBER)
                {
                    if (textBuilder.Length > 0)
                    {
                        textBuilder.Append(" ");
                    }
                    textBuilder.Append(_currentToken.Value);
                    Consume(_currentToken.Type);
                }
                else
                {
                    if (textBuilder.Length > 0)
                    {
                        textBuilder.Append(" ");
                    }
                    textBuilder.Append(_currentToken.Value);
                    Consume(_currentToken.Type);
                }
            }

            if (textBuilder.Length > 0)
            {
                segments.Add(new TextNode(textBuilder.ToString().TrimEnd(), _currentToken.Line, _currentToken.Column));
            }

            return segments;
        }

        private InterpolationNode ParseInterpolation()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;
            Consume(TokenType.LEFT_BRACE);
            var expr = ParseExpression();
            Consume(TokenType.RIGHT_BRACE);
            return new InterpolationNode(expr, line, column);
        }

        private ChoiceNode ParseChoice()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;
            Consume(TokenType.ARROW);

            var text = ParseText();

            ExpressionNode? condition = null;
            if (Match(TokenType.LEFT_BRACKET))
            {
                Consume(TokenType.IF);
                condition = ParseExpression();
                Consume(TokenType.RIGHT_BRACKET);
            }

            Consume(TokenType.NEWLINE);
            Consume(TokenType.INDENT);

            var content = new List<ContentNode>();
            while (_currentToken.Type != TokenType.DEDENT && _currentToken.Type != TokenType.EOF && _currentToken.Type != TokenType.DOUBLE_COLON)
            {
                ContentNode node;
                if (_currentToken.Type == TokenType.IF)
                {
                    node = ParseCondition();
                }
                else
                {
                    node = ParseContent();
                }
                
                // 跳过空的内容节点
                if (!IsEmptyContentNode(node))
                {
                    content.Add(node);
                }
            }

            if (_currentToken.Type == TokenType.DEDENT)
            {
                Consume(TokenType.DEDENT);
            }

            return new ChoiceNode(text, condition, content, line, column);
        }

        private ConditionNode ParseCondition()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;
            Consume(TokenType.IF);

            var condition = ParseExpression();
            Consume(TokenType.NEWLINE);
            Consume(TokenType.INDENT);

            var thenBranch = new List<ContentNode>();
            while (_currentToken.Type != TokenType.DEDENT && _currentToken.Type != TokenType.ELIF && _currentToken.Type != TokenType.ELSE && _currentToken.Type != TokenType.ENDIF)
            {
                ContentNode node = ParseContent();
                // 跳过空的内容节点
                if (!IsEmptyContentNode(node))
                {
                    thenBranch.Add(node);
                }
            }

            if (_currentToken.Type == TokenType.DEDENT)
            {
                Consume(TokenType.DEDENT);
            }

            var elifBranches = new List<(ExpressionNode Condition, List<ContentNode> Content)>();
            while (_currentToken.Type == TokenType.ELIF)
            {
                Consume(TokenType.ELIF);
                var elifCondition = ParseExpression();
                Consume(TokenType.NEWLINE);
                Consume(TokenType.INDENT);

                var elifContent = new List<ContentNode>();
                while (_currentToken.Type != TokenType.DEDENT && _currentToken.Type != TokenType.ELIF && _currentToken.Type != TokenType.ELSE && _currentToken.Type != TokenType.ENDIF)
                {
                    ContentNode node = ParseContent();
                    // 跳过空的内容节点
                    if (!IsEmptyContentNode(node))
                    {
                        elifContent.Add(node);
                    }
                }

                elifBranches.Add((elifCondition, elifContent));

                if (_currentToken.Type == TokenType.DEDENT)
                {
                    Consume(TokenType.DEDENT);
                }
            }

            List<ContentNode>? elseBranch = null;
            if (_currentToken.Type == TokenType.ELSE)
            {
                Consume(TokenType.ELSE);
                Consume(TokenType.NEWLINE);
                Consume(TokenType.INDENT);

                elseBranch = new List<ContentNode>();
                while (_currentToken.Type != TokenType.DEDENT && _currentToken.Type != TokenType.ENDIF)
                {
                    ContentNode node = ParseContent();
                    // 跳过空的内容节点
                    if (!IsEmptyContentNode(node))
                    {
                        elseBranch.Add(node);
                    }
                }

                if (_currentToken.Type == TokenType.DEDENT)
                {
                    Consume(TokenType.DEDENT);
                }
            }

            if (_currentToken.Type != TokenType.ENDIF)
            {
                Logger.LogError($"Expected ENDIF but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                throw new SyntaxErrorException($"Expected ENDIF but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
            }

            Consume(TokenType.ENDIF);
            Consume(TokenType.NEWLINE);

            return new ConditionNode(condition, thenBranch, elifBranches, elseBranch, line, column);
        }

        /// <summary>
        /// 解析命令
        /// </summary>
        private CommandNode ParseCommand()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;

            string commandValue = _currentToken.Value.ToLower();
            TokenType commandType = _currentToken.Type;
            Consume(commandType);

            switch (commandType)
            {
                case TokenType.SET:
                case TokenType.ADD:
                case TokenType.SUB:
                case TokenType.MUL:
                case TokenType.DIV:
                case TokenType.MOD:
                    string variable = _currentToken.Value;
                    Consume(TokenType.VARIABLE);

                    // 赋值符号是可选的
                    if (_currentToken.Type == TokenType.ASSIGN)
                    {
                        Consume(TokenType.ASSIGN);
                    }

                    var value = ParseExpression();

                    // 处理文件末尾情况
                    if (_currentToken.Type != TokenType.EOF)
                    {
                        Consume(TokenType.NEWLINE);
                    }

                    return new VarCommandNode(variable, value, commandValue, line, column);

                case TokenType.CALL:
                    string functionName = _currentToken.Value;
                    Consume(TokenType.IDENTIFIER);
                    Consume(TokenType.LEFT_PAREN);
                    var parameters = new List<ExpressionNode>();
                    if (_currentToken.Type != TokenType.RIGHT_PAREN)
                    {
                        parameters.Add(ParseExpression());
                        while (Match(TokenType.COMMA))
                        {
                            parameters.Add(ParseExpression());
                        }
                    }
                    Consume(TokenType.RIGHT_PAREN);

                    // 处理文件末尾情况
                    if (_currentToken.Type != TokenType.EOF)
                    {
                        Consume(TokenType.NEWLINE);
                    }

                    return new CallCommandNode(functionName, parameters, line, column);

                case TokenType.WAIT:
                    var duration = ParseExpression();

                    // 处理文件末尾情况
                    if (_currentToken.Type != TokenType.EOF)
                    {
                        Consume(TokenType.NEWLINE);
                    }

                    return new WaitCommandNode(duration, line, column);

                case TokenType.VAR:
                    if (_currentToken.Type != TokenType.VARIABLE)
                    {
                        Logger.LogError($"Expected variable name but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                        throw new SyntaxErrorException($"Expected variable name but got {_currentToken.Type} at line {_currentToken.Line}, column {_currentToken.Column}");
                    }
                    var varName = _currentToken.Value;
                    Consume(TokenType.VARIABLE);

                    // 赋值符号是可选的
                    if (_currentToken.Type == TokenType.ASSIGN)
                    {
                        Consume(TokenType.ASSIGN);
                    }

                    var initialValue = ParseExpression();

                    // 处理文件末尾情况
                    if (_currentToken.Type != TokenType.EOF)
                    {
                        Consume(TokenType.NEWLINE);
                    }

                    return new VarCommandNode(varName, initialValue, "set", line, column);

                case TokenType.JUMP:
                    string targetNode = _currentToken.Value;
                    Consume(TokenType.IDENTIFIER);

                    // 处理文件末尾情况
                    if (_currentToken.Type != TokenType.EOF)
                    {
                        Consume(TokenType.NEWLINE);
                    }

                    return new JumpCommandNode(targetNode, line, column);

                case TokenType.COMMAND:
                    Logger.LogError($"Unknown command {commandValue} at line {line}, column {column}");
                    throw new SyntaxErrorException($"Unknown command {commandValue} at line {line}, column {column}");

                default:
                    Logger.LogError($"Unknown command type {commandType} at line {line}, column {column}");
                    throw new SyntaxErrorException($"Unknown command type {commandType} at line {line}, column {column}");
            }
        }

        private ExpressionNode ParseExpression()
        {
            return ParseOr();
        }

        private ExpressionNode ParseOr()
        {
            var left = ParseAnd();

            while (_currentToken.Type == TokenType.OR)
            {
                var op = _currentToken.Value;
                int line = _currentToken.Line;
                int column = _currentToken.Column;
                Consume(TokenType.OR);
                var right = ParseAnd();
                left = new BinaryOpNode(left, op, right, line, column);
            }

            return left;
        }

        private ExpressionNode ParseAnd()
        {
            var left = ParseComparison();

            while (_currentToken.Type == TokenType.AND)
            {
                var op = _currentToken.Value;
                int line = _currentToken.Line;
                int column = _currentToken.Column;
                Consume(TokenType.AND);
                var right = ParseComparison();
                left = new BinaryOpNode(left, op, right, line, column);
            }

            return left;
        }

        private ExpressionNode ParseComparison()
        {
            var left = ParseAdditive();

            while (_currentToken.Type == TokenType.EQUALS || _currentToken.Type == TokenType.NOT_EQUALS ||
                   _currentToken.Type == TokenType.GREATER || _currentToken.Type == TokenType.LESS ||
                   _currentToken.Type == TokenType.GREATER_EQUALS || _currentToken.Type == TokenType.LESS_EQUALS)
            {
                var op = _currentToken.Value;
                int line = _currentToken.Line;
                int column = _currentToken.Column;
                Consume(_currentToken.Type);
                var right = ParseAdditive();
                left = new BinaryOpNode(left, op, right, line, column);
            }

            return left;
        }

        private ExpressionNode ParseAdditive()
        {
            var left = ParseMultiplicative();

            while (_currentToken.Type == TokenType.PLUS || _currentToken.Type == TokenType.MINUS)
            {
                var op = _currentToken.Value;
                int line = _currentToken.Line;
                int column = _currentToken.Column;
                Consume(_currentToken.Type);
                var right = ParseMultiplicative();
                left = new BinaryOpNode(left, op, right, line, column);
            }

            return left;
        }

        private ExpressionNode ParseMultiplicative()
        {
            var left = ParseUnary();

            while (_currentToken.Type == TokenType.MULTIPLY || _currentToken.Type == TokenType.DIVIDE || _currentToken.Type == TokenType.MODULO)
            {
                var op = _currentToken.Value;
                int line = _currentToken.Line;
                int column = _currentToken.Column;
                Consume(_currentToken.Type);
                var right = ParseUnary();
                left = new BinaryOpNode(left, op, right, line, column);
            }

            return left;
        }

        private ExpressionNode ParseUnary()
        {
            if (_currentToken.Type == TokenType.NOT || _currentToken.Type == TokenType.MINUS)
            {
                var op = _currentToken.Value;
                int line = _currentToken.Line;
                int column = _currentToken.Column;
                Consume(_currentToken.Type);
                var operand = ParseUnary();
                return new UnaryOpNode(op, operand, line, column);
            }

            return ParsePrimary();
        }

        private ExpressionNode ParsePrimary()
        {
            Token token = _currentToken;
            switch (token.Type)
            {
                case TokenType.NUMBER:
                    Consume(TokenType.NUMBER);
                    return new NumberNode(double.Parse(token.Value), token.Line, token.Column);

                case TokenType.STRING:
                    Consume(TokenType.STRING);
                    // 检查字符串中是否包含插值表达式
                    if (token.Value.Contains("{") && token.Value.Contains("}"))
                    {
                        var segments = new List<TextSegmentNode>();
                        var text = token.Value;
                        int startIndex = 0;
                        int exprLine = token.Line;
                        int exprColumn = token.Column;

                        while (true)
                        {
                            int leftBrace = text.IndexOf('{', startIndex);
                            if (leftBrace == -1)
                            {
                                if (startIndex < text.Length)
                                {
                                    segments.Add(new TextNode(text.Substring(startIndex), exprLine, exprColumn + startIndex));
                                }
                                break;
                            }

                            if (leftBrace > startIndex)
                            {
                                segments.Add(new TextNode(text.Substring(startIndex, leftBrace - startIndex), exprLine, exprColumn + startIndex));
                            }

                            int rightBrace = text.IndexOf('}', leftBrace);
                            if (rightBrace == -1)
                            {
                                throw new SyntaxErrorException($"未闭合的插值表达式 at line {exprLine}, column {exprColumn + leftBrace}");
                            }

                            string varName = text.Substring(leftBrace + 1, rightBrace - leftBrace - 1).Trim();
                            if (varName.StartsWith("$"))
                            {
                                segments.Add(new InterpolationNode(new VariableNode(varName.Substring(1), exprLine, exprColumn + leftBrace), exprLine, exprColumn + leftBrace));
                            }
                            else
                            {
                                throw new SyntaxErrorException($"插值表达式中的变量名必须以$开头 at line {exprLine}, column {exprColumn + leftBrace}");
                            }

                            startIndex = rightBrace + 1;
                        }

                        // 如果只有一个文本段，直接返回StringNode
                        if (segments.Count == 1 && segments[0] is TextNode textNode)
                        {
                            return new StringNode(textNode.Text, exprLine, exprColumn);
                        }

                        // 否则创建一个插值表达式
                        return new InterpolationExpressionNode(segments, exprLine, exprColumn);
                    }
                    return new StringNode(token.Value, token.Line, token.Column);

                case TokenType.TRUE:
                    Consume(TokenType.TRUE);
                    return new BooleanNode(true, token.Line, token.Column);

                case TokenType.FALSE:
                    Consume(TokenType.FALSE);
                    return new BooleanNode(false, token.Line, token.Column);

                case TokenType.VARIABLE:
                    var variableValue = _currentToken.Value;
                    int line = _currentToken.Line;
                    int column = _currentToken.Column;
                    Consume(TokenType.VARIABLE);
                    return new VariableNode(variableValue, line, column);

                case TokenType.IDENTIFIER:
                    var identifierValue = _currentToken.Value;
                    line = _currentToken.Line;
                    column = _currentToken.Column;
                    Consume(TokenType.IDENTIFIER);

                    // 检查是否是函数调用
                    if (Check(TokenType.LEFT_PAREN))
                    {
                        Consume(TokenType.LEFT_PAREN);
                        var parameters = new List<ExpressionNode>();
                        if (!Check(TokenType.RIGHT_PAREN))
                        {
                            parameters.Add(ParseExpression());
                            while (Match(TokenType.COMMA))
                            {
                                parameters.Add(ParseExpression());
                            }
                        }
                        Consume(TokenType.RIGHT_PAREN);
                        return new FunctionCallNode(identifierValue, parameters, line, column);
                    }

                    return new IdentifierNode(identifierValue, line, column);

                case TokenType.LEFT_PAREN:
                    Consume(TokenType.LEFT_PAREN);
                    var expr = ParseExpression();
                    Consume(TokenType.RIGHT_PAREN);
                    return expr;

                default:
                    Logger.LogError($"Unexpected token {token.Type} at line {token.Line}, column {token.Column}");
                    throw new SyntaxErrorException($"Unexpected token {token.Type} at line {token.Line}, column {token.Column}");
            }
        }

        /// <summary>
        /// 解析旁白
        /// </summary>
        private NarrationNode ParseNarration()
        {
            int line = _currentToken.Line;
            int column = _currentToken.Column;
            var text = ParseText();

            List<string>? labels = null;
            while (Match(TokenType.HASH))
            {
                if (labels == null)
                {
                    labels = new List<string>();
                }
                labels.Add(_currentToken.Value);
                Consume(TokenType.IDENTIFIER);
            }

            // 处理文件末尾情况
            if (_currentToken.Type != TokenType.EOF)
            {
                Consume(TokenType.NEWLINE);
            }

            return new NarrationNode(text, labels, line, column);
        }
    }
}