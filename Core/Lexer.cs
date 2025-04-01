using System;
using System.Collections.Generic;
using System.Text;

namespace MookDialogueScript
{
    public class Lexer
    {
        private readonly string _source;
        private int _position;
        private int _line;
        private int _column;
        private char _currentChar;
        private readonly List<int> _indentStack;
        private int _currentIndent;
        private Token? _nextToken = null;

        private static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
        {
            {"if", TokenType.IF},
            {"elif", TokenType.ELIF},
            {"else", TokenType.ELSE},
            {"endif", TokenType.ENDIF},
            {"true", TokenType.TRUE},
            {"false", TokenType.FALSE},
            {"var", TokenType.VAR},
            {"set", TokenType.SET},
            {"add", TokenType.ADD},
            {"sub", TokenType.SUB},
            {"mul", TokenType.MUL},
            {"div", TokenType.DIV},
            {"mod", TokenType.MOD},
            {"=>", TokenType.JUMP},
            {"jump", TokenType.JUMP},
            {"call", TokenType.CALL},
            {"wait", TokenType.WAIT},
        };

        private static readonly HashSet<TokenType> CommandKeywords = new HashSet<TokenType>
        {
            TokenType.VAR,
            TokenType.SET,
            TokenType.ADD,
            TokenType.SUB,
            TokenType.MUL,
            TokenType.DIV,
            TokenType.MOD,
            TokenType.JUMP,
            TokenType.CALL,
            TokenType.WAIT,
        };

        public Lexer(string source)
        {
            _source = source;
            _position = 0;
            _line = 1;
            _column = 1;
            _currentChar = _position < _source.Length ? _source[_position] : '\0';
            _indentStack = new List<int> { 0 };
            _currentIndent = 0;
        }

        /// <summary>
        /// 前进一个字符
        /// </summary>
        private void Advance()
        {
            _position++;
            _column++;
            _currentChar = _position < _source.Length ? _source[_position] : '\0';
        }

        /// <summary>
        /// 查看下一个字符
        /// </summary>
        private char Peek()
        {
            int peekPos = _position + 1;
            return peekPos < _source.Length ? _source[peekPos] : '\0';
        }

        /// <summary>
        /// 跳过空白字符
        /// </summary>
        private void SkipWhitespace()
        {
            while (_currentChar != '\0' && char.IsWhiteSpace(_currentChar) && _currentChar != '\n' && _currentChar != '\r')
            {
                Advance();
            }
        }

        /// <summary>
        /// 跳过注释
        /// </summary>
        private void SkipComment()
        {
            while (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r')
            {
                Advance();
            }
        }

        /// <summary>
        /// 处理缩进
        /// </summary>
        private Token HandleIndentation()
        {
            int indent = 0;
            while (_currentChar == ' ' || _currentChar == '\t')
            {
                indent++;
                Advance();
            }

            if (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r' && _currentChar != '/')
            {
                if (indent > _currentIndent)
                {
                    _indentStack.Add(indent);
                    _currentIndent = indent;
                    return new Token(TokenType.INDENT, "", _line, _column);
                }
                else if (indent < _currentIndent)
                {
                    // 找到正确的缩进级别
                    while (_indentStack.Count > 0 && _indentStack[_indentStack.Count - 1] > indent)
                    {
                        _indentStack.RemoveAt(_indentStack.Count - 1);
                    }

                    if (_indentStack.Count == 0 || _indentStack[_indentStack.Count - 1] != indent)
                    {
                        Logger.LogError($"Invalid indentation at line {_line}, column {_column}");
                        throw new InvalidOperationException($"Invalid indentation at line {_line}, column {_column}");
                    }

                    _currentIndent = indent;
                    return new Token(TokenType.DEDENT, "", _line, _column);
                }
                else if (indent != _currentIndent)
                {
                    Logger.LogError($"Inconsistent indentation at line {_line}, column {_column}");
                    throw new InvalidOperationException($"Inconsistent indentation at line {_line}, column {_column}");
                }
            }

            return null;
        }

        /// <summary>
        /// 处理数字
        /// </summary>
        private Token HandleNumber()
        {
            StringBuilder result = new StringBuilder();
            bool hasDecimalPoint = false;

            while (_currentChar != '\0' &&
                (char.IsDigit(_currentChar) || _currentChar == '.'))
            {
                if (_currentChar == '.')
                {
                    if (hasDecimalPoint)
                        break;
                    hasDecimalPoint = true;
                }
                result.Append(_currentChar);
                Advance();
            }

            return new Token(TokenType.NUMBER, result.ToString(), _line, _column);
        }

        /// <summary>
        /// 处理字符串
        /// </summary>
        private Token HandleString()
        {
            char quoteType = _currentChar;
            Advance(); // Skip opening quote
            StringBuilder result = new StringBuilder();

            while (_currentChar != '\0' && _currentChar != quoteType)
            {
                if (_currentChar == '\\' && Peek() == quoteType)
                {
                    Advance(); // Skip backslash
                    result.Append(_currentChar);
                }
                else
                {
                    result.Append(_currentChar);
                }
                Advance();
            }

            if (_currentChar == quoteType)
            {
                Advance(); // Skip closing quote
                return new Token(TokenType.STRING, result.ToString(), _line, _column);
            }
            else
            {
                Logger.LogError($"Unterminated string at line {_line}, column {_column}");
                throw new InvalidOperationException($"Unterminated string at line {_line}, column {_column}");
            }
        }

        /// <summary>
        /// 处理文本
        /// </summary>
        private Token HandleText()
        {
            StringBuilder result = new StringBuilder();

            while (_currentChar != '\0' && _currentChar != '\n' && _currentChar != '\r')
            {
                // 处理转义字符
                if (_currentChar == '\\')
                {
                    char nextChar = Peek();
                    if (nextChar == '#' || nextChar == ':' || nextChar == '：' ||
                        nextChar == '[' || nextChar == ']' || nextChar == '{' ||
                        nextChar == '}' || nextChar == '\\')
                    {
                        // 跳过反斜杠
                        Advance();
                        // 直接添加被转义的字符
                        result.Append(_currentChar);
                        Advance();
                        continue;
                    }
                    // 如果不是特殊字符，保留反斜杠
                    result.Append(_currentChar);
                    Advance();
                    continue;
                }

                // 遇到非转义的特殊字符时截断
                if (_currentChar == '#' || _currentChar == '{')
                {
                    break;
                }

                result.Append(_currentChar);
                Advance();
            }

            return new Token(TokenType.TEXT, result.ToString().Trim(), _line, _column);
        }

        /// <summary>
        /// 处理标识符或关键字
        /// </summary>
        private Token HandleIdentifierOrKeyword()
        {
            StringBuilder result = new StringBuilder();

            // 第一个字符必须是字母、下划线或中文
            if (_currentChar != '\0' &&
                (char.IsLetter(_currentChar) || _currentChar == '_' ||
                _currentChar is >= '\u4e00' and <= '\u9fa5'))
            {
                result.Append(_currentChar);
                Advance();

                // 后续字符可以包含数字
                while (_currentChar != '\0' &&
                    (char.IsLetterOrDigit(_currentChar) || _currentChar == '_' ||
                    _currentChar >= '\u4e00' && _currentChar <= '\u9fa5'))
                {
                    result.Append(_currentChar);
                    Advance();
                }
            }

            string text = result.ToString();
            string textLower = text.ToLower();

            // 检查是否是关键字
            if (Keywords.TryGetValue(textLower, out TokenType type))
            {
                // 检查是否是命令关键字
                if (CommandKeywords.Contains(type))
                {
                    return new Token(type, text, _line, _column);
                }

                return new Token(type, text, _line, _column);
            }

            // 所有非关键字的标识符都作为IDENTIFIER处理
            return new Token(TokenType.IDENTIFIER, text, _line, _column);
        }

        /// <summary>
        /// 预览下一个Token而不消耗它
        /// </summary>
        /// <returns>下一个Token</returns>
        public Token PeekNextToken()
        {
            // 保存当前状态
            int savedPosition = _position;
            int savedLine = _line;
            int savedColumn = _column;
            char savedCurrentChar = _currentChar;
            int savedCurrentIndent = _currentIndent;

            // 清除任何现有的缓存，强制执行新的token扫描
            _nextToken = null;

            // 获取下一个Token
            var nextToken = GetNextToken();

            // 恢复状态
            _position = savedPosition;
            _line = savedLine;
            _column = savedColumn;
            _currentChar = savedCurrentChar;
            _currentIndent = savedCurrentIndent;

            return nextToken;
        }

        /// <summary>
        /// 获取下一个Token并消耗它
        /// </summary>
        /// <returns>下一个Token</returns>
        public Token GetNextToken()
        {
            // 我们不再使用缓存的Token
            _nextToken = null;

            while (_currentChar != '\0')
            {
                // 处理行首缩进
                if (_column == 1)
                {
                    Token indentToken = HandleIndentation();
                    if (indentToken != null)
                        return indentToken;
                }

                // 跳过空白字符
                if (char.IsWhiteSpace(_currentChar) && _currentChar != '\n' && _currentChar != '\r')
                {
                    SkipWhitespace();
                    continue;
                }

                // 处理注释
                if (_currentChar == '/' && Peek() == '/')
                {
                    SkipComment();
                    continue;
                }

                // 处理换行
                if (_currentChar == '\n' || _currentChar == '\r')
                {
                    if (_currentChar == '\r' && Peek() == '\n')
                    {
                        Advance();
                    }
                    Advance();
                    _line++;
                    _column = 1;
                    return new Token(TokenType.NEWLINE, "\\n", _line - 1, _column);
                }

                // 处理转义字符
                if (_currentChar == '\\')
                {
                    return HandleText();
                }

                // 处理标识符、关键字和命令
                if (char.IsLetter(_currentChar) || _currentChar == '_' ||
                _currentChar is >= '\u4e00' and <= '\u9fa5')
                {
                    return HandleIdentifierOrKeyword();
                }

                // 处理数字
                if (char.IsDigit(_currentChar))
                {
                    return HandleNumber();
                }

                // 处理字符串
                if (_currentChar == '\'' || _currentChar == '"')
                {
                    return HandleString();
                }

                // 处理操作符和标点符号
                switch (_currentChar)
                {
                    case ':':
                    case '：': // 中文冒号
                        Advance();
                        if (_currentChar == ':' || _currentChar == '：')
                        {
                            Advance();
                            return new Token(TokenType.DOUBLE_COLON, "::", _line, _column - 2);
                        }
                        // 不直接处理后面的文本，只返回冒号标记
                        return new Token(TokenType.COLON, ":", _line, _column - 1);

                    case '-':
                        Advance();
                        if (_currentChar == '>' || _currentChar == '》') // 支持中文后书名号
                        {
                            Advance();
                            // 不直接处理后面的文本，只返回箭头标记
                            return new Token(TokenType.ARROW, "->", _line, _column - 2);
                        }
                        return new Token(TokenType.MINUS, "-", _line, _column - 1);

                    case '=':
                        Advance();
                        if (_currentChar == '>' || _currentChar == '》') // 支持中文后书名号
                        {
                            Advance();
                            return new Token(TokenType.JUMP, "=>", _line, _column - 2);
                        }
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.EQUALS, "==", _line, _column - 2);
                        }
                        return new Token(TokenType.ASSIGN, "=", _line, _column - 1);
                    case '$':
                        Advance(); // 跳过$符号
                        StringBuilder varResult = new StringBuilder();

                        // 变量名的第一个字符必须是字母、下划线或中文
                        if (_currentChar != '\0' &&
                            (char.IsLetter(_currentChar) || _currentChar == '_' ||
                            _currentChar is >= '\u4e00' and <= '\u9fa5'))
                        {
                            varResult.Append(_currentChar);
                            Advance();

                            // 后续字符可以包含数字
                            while (_currentChar != '\0' &&
                                (char.IsLetterOrDigit(_currentChar) || _currentChar == '_' ||
                                _currentChar >= '\u4e00' && _currentChar <= '\u9fa5'))
                            {
                                varResult.Append(_currentChar);
                                Advance();
                            }
                        }
                        return new Token(TokenType.VARIABLE, varResult.ToString(), _line, _column - 1);

                    case '+': Advance(); return new Token(TokenType.PLUS, "+", _line, _column - 1);
                    case '*': Advance(); return new Token(TokenType.MULTIPLY, "*", _line, _column - 1);
                    case '/': Advance(); return new Token(TokenType.DIVIDE, "/", _line, _column - 1);
                    case '%': Advance(); return new Token(TokenType.MODULO, "%", _line, _column - 1);
                    case '(': Advance(); return new Token(TokenType.LEFT_PAREN, "(", _line, _column - 1);
                    case ')': Advance(); return new Token(TokenType.RIGHT_PAREN, ")", _line, _column - 1);
                    case '[': Advance(); return new Token(TokenType.LEFT_BRACKET, "[", _line, _column - 1);
                    case ']': Advance(); return new Token(TokenType.RIGHT_BRACKET, "]", _line, _column - 1);
                    case '{': Advance(); return new Token(TokenType.LEFT_BRACE, "{", _line, _column - 1);
                    case '}': Advance(); return new Token(TokenType.RIGHT_BRACE, "}", _line, _column - 1);
                    case ',':
                        Advance();
                        return new Token(TokenType.COMMA, ",", _line, _column - 1);
                    case '#': Advance(); return new Token(TokenType.HASH, "#", _line, _column - 1);

                    case '!':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.NOT_EQUALS, "!=", _line, _column - 2);
                        }
                        return new Token(TokenType.NOT, "!", _line, _column - 1);

                    case '>':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.GREATER_EQUALS, ">=", _line, _column - 2);
                        }
                        return new Token(TokenType.GREATER, ">", _line, _column - 1);

                    case '<':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.LESS_EQUALS, "<=", _line, _column - 2);
                        }
                        return new Token(TokenType.LESS, "<", _line, _column - 1);

                    case '&':
                        Advance();
                        if (_currentChar == '&')
                        {
                            Advance();
                            return new Token(TokenType.AND, "&&", _line, _column - 2);
                        }
                        Logger.LogError($"Invalid character sequence at line {_line}, column {_column}");
                        throw new InvalidOperationException($"Invalid character sequence at line {_line}, column {_column}");

                    case '|':
                        Advance();
                        if (_currentChar == '|')
                        {
                            Advance();
                            return new Token(TokenType.OR, "||", _line, _column - 2);
                        }
                        Logger.LogError($"Invalid character sequence at line {_line}, column {_column}");
                        throw new InvalidOperationException($"Invalid character sequence at line {_line}, column {_column}");

                    default:
                        // 如果是其他字符，作为Text处理
                        return HandleText();
                }
            }

            // 处理文件结束
            return new Token(TokenType.EOF, "", _line, _column);
        }
    }
}