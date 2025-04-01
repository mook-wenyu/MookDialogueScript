namespace MookDialogueScript
{
    public enum TokenType
    {
        // 特殊标记
        /// <summary>
        /// 文件结束
        /// </summary>
        EOF,

        // 标点符号
        /// <summary>
        /// 冒号 :
        /// </summary>
        COLON,          // :
        /// <summary>
        /// 箭头 ->
        /// </summary>
        ARROW,          // ->
        /// <summary>
        /// 双冒号 ::
        /// </summary>
        DOUBLE_COLON,   // ::
        /// <summary>
        /// 左中括号 [
        /// </summary>
        LEFT_BRACKET,   // [
        /// <summary>
        /// 右中括号 ]
        /// </summary>
        RIGHT_BRACKET,  // ]
        /// <summary>
        /// 左大括号 {
        /// </summary>
        LEFT_BRACE,     // {
        /// <summary>
        /// 右大括号 }
        /// </summary>
        RIGHT_BRACE,    // }
        /// <summary>
        /// 左括号 (
        /// </summary>
        LEFT_PAREN,     // (
        /// <summary>
        /// 右括号 )
        /// </summary>
        RIGHT_PAREN,    // )
        /// <summary>
        /// 逗号 ,
        /// </summary>
        COMMA,          // ,
        /// <summary>
        /// 井号 #
        /// </summary>
        HASH,           // #

        // 运算符
        /// <summary>
        /// 加号 +
        /// </summary>
        PLUS,           // +
        /// <summary>
        /// 减号 -
        /// </summary>
        MINUS,          // -
        /// <summary>
        /// 乘号 *
        /// </summary>
        MULTIPLY,       // *
        /// <summary>
        /// 除号 /
        /// </summary>
        DIVIDE,         // /
        /// <summary>
        /// 取模 %
        /// </summary>
        MODULO,         // %
        /// <summary>
        /// 等于 ==
        /// </summary>
        EQUALS,         // ==
        /// <summary>
        /// 不等于 !=
        /// </summary>
        NOT_EQUALS,     // !=
        /// <summary>
        /// 大于 >
        /// </summary>
        GREATER,        // >
        /// <summary>
        /// 小于 <
        /// </summary>
        LESS,           // <
        /// <summary>
        /// 大于等于 >=
        /// </summary>
        GREATER_EQUALS, // >=
        /// <summary>
        /// 小于等于 <=
        /// </summary>
        LESS_EQUALS,    // <=
        /// <summary>
        /// 与 &&
        /// </summary>
        AND,            // &&
        /// <summary>
        /// 或 ||
        /// </summary>
        OR,            // ||
        /// <summary>
        /// 非 !
        /// </summary>
        NOT,           // !

        // 关键字
        /// <summary>
        /// 如果 if
        /// </summary>
        IF,
        /// <summary>
        /// 否则如果 elif
        /// </summary>
        ELIF,
        /// <summary>
        /// 否则 else
        /// </summary>
        ELSE,
        /// <summary>
        /// 结束if
        /// </summary>
        ENDIF,
        /// <summary>
        /// 真 true
        /// </summary>
        TRUE,
        /// <summary>
        /// 假 false
        /// </summary>
        FALSE,


        // 命令
        /// <summary>
        /// 命令关键字
        /// </summary>
        COMMAND,        // 命令关键字
        /// <summary>
        /// 声明变量
        /// </summary>
        VAR,
        /// <summary>
        /// 设置变量
        /// </summary>
        SET,
        /// <summary>
        /// 加 +
        /// </summary>
        ADD,
        /// <summary>
        /// 减 -
        /// </summary>
        SUB,
        /// <summary>
        /// 乘 *
        /// </summary>
        MUL,
        /// <summary>
        /// 除 /
        /// </summary>
        DIV,
        /// <summary>
        /// 取模
        /// </summary>
        MOD,
        /// <summary>
        /// 跳转 => or jump
        /// </summary>
        JUMP,           // => or jump
        /// <summary>
        /// 调用 call
        /// </summary>
        CALL,
        /// <summary>
        /// 等待 wait
        /// </summary>
        WAIT,

        // 标识符和字面量
        /// <summary>
        /// 标识符
        /// </summary>
        IDENTIFIER,     // 标识符
        /// <summary>
        /// 变量，以$开头 如 $name
        /// </summary>
        VARIABLE,       // 变量，如 $name
        /// <summary>
        /// 数字
        /// </summary>
        NUMBER,         // 数字
        /// <summary>
        /// 字符串
        /// </summary>
        STRING,         // 字符串
        /// <summary>
        /// 文本 对话内容和选项内容，不需要引号的字符串
        /// </summary>
        TEXT,           // 对话内容和选项内容，不需要引号的字符串
        /// <summary>
        /// 赋值 =
        /// </summary>
        ASSIGN,         // =

        // 其他
        /// <summary>
        /// 换行
        /// </summary>
        NEWLINE,        // 换行
        /// <summary>
        /// 缩进
        /// </summary>
        INDENT,         // 缩进
        /// <summary>
        /// 减少缩进
        /// </summary>
        DEDENT,         // 减少缩进
    }

    public class Token
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

        public override string ToString()
        {
            return $"Token({Type}, '{Value}', line={Line}, col={Column})";
        }
    }
}