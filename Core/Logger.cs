using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace MookDialogueScript
{
    public static class Logger
    {
        private static ILogger _logger = new DefaultLogger();

        public static void SetLogger(ILogger logger) => _logger = logger;

        public static void Log(object message,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => _logger.Log(message, member, file, line);
        public static void LogWarning(object message,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => _logger.LogWarning(message, member, file, line);
        public static void LogError(object message,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => _logger.LogError(message, member, file, line);
    }

    // 默认实现（普通C#环境）
    public class DefaultLogger : ILogger
    {
        public void Log(object message,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => Console.WriteLine($"{Path.GetFileName(file)}:{line} - [INFO] {message}");
        public void LogWarning(object message,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => Console.WriteLine($"{Path.GetFileName(file)}:{line} - [WARNING] {message}");
        public void LogError(object message,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => Console.Error.WriteLine($"{Path.GetFileName(file)}:{line} - [ERROR] {message}");
    }
}
