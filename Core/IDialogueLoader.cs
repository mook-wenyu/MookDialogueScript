using System;
using System.IO;
using System.Linq;

namespace MookDialogueScript
{
    /// <summary>
    /// 对话脚本加载器接口
    /// </summary>
    public interface IDialogueLoader
    {
        /// <summary>
        /// 加载脚本
        /// </summary>
        /// <param name="dialogueManager">对话管理器</param>
        /// <returns>异步任务</returns>
        public void LoadScripts(Runner dialogueManager);
    }

    /// <summary>
    /// 默认对话脚本加载器
    /// </summary>
    public class DefaultDialogueLoader : IDialogueLoader
    {
        private readonly string _rootDir;
        private readonly string[] _fileExtensions = { ".txt", ".mds" };

        public DefaultDialogueLoader() : this("DialogueScripts")
        {
        }

        /// <summary>
        /// 创建一个默认对话脚本加载器
        /// </summary>
        /// <param name="rootDir">脚本文件根目录</param>
        public DefaultDialogueLoader(string rootDir)
        {
            _rootDir = rootDir;

            if (!Directory.Exists(_rootDir))
            {
                Directory.CreateDirectory(_rootDir);
            }
        }

        /// <summary>
        /// 加载脚本
        /// </summary>
        /// <param name="context">对话上下文</param>
        /// <param name="dialogueManager">对话管理器</param>
        /// <returns>异步任务</returns>
        public void LoadScripts(Runner dialogueManager)
        {
            // 获取所有符合条件的文件
            var files = Directory.GetFiles(_rootDir, "*.*", SearchOption.AllDirectories)
                .Where(f => _fileExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            // 加载所有文件
            foreach (var file in files)
            {
                try
                {
                    string scriptContent = File.ReadAllText(file);
                    LoadScriptContent(scriptContent, dialogueManager, file);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"加载脚本文件 {file} 时出错: {ex.Message}");
                    throw new IOException($"加载脚本文件 {file} 时出错: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 加载脚本内容
        /// </summary>
        /// <param name="scriptContent">脚本内容</param>
        /// <param name="context">对话上下文</param>
        /// <param name="dialogueManager">对话管理器</param>
        /// <param name="filePath">文件路径(调试用)</param>
        /// <returns>异步任务</returns>
        private void LoadScriptContent(string scriptContent,
            Runner dialogueManager, string filePath = "")
        {
            try
            {
                // 创建词法分析器
                var lexer = new Lexer(scriptContent);

                // 创建语法分析器
                var parser = new Parser(lexer);
                var scriptNode = parser.Parse();

                // 注册脚本节点
                dialogueManager.RegisterScript(scriptNode);
            }
            catch (Exception ex)
            {
                string fileInfo = string.IsNullOrEmpty(filePath) ? "" : $" (文件: {filePath})";
                Logger.LogError($"解析脚本内容时出错{fileInfo}: {ex.Message}");
                throw new InvalidOperationException($"解析脚本内容时出错{fileInfo}: {ex.Message}", ex);
            }
        }
    }

}