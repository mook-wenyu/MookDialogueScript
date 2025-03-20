using System;
using System.IO;
using MookDialogueScript;
using UnityEngine;

public class UnityDialogueLoader : IDialogueLoader
{
    private readonly string _rootDir;
    private readonly string[] _fileExtensions = {".txt", ".mds"};

    public UnityDialogueLoader() : this("DialogueScripts")
    {
    }

    public UnityDialogueLoader(string rootDir)
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
    public void LoadScripts(Runner runner)
    {
        // 加载所有对话脚本
        var assets = Resources.LoadAll<TextAsset>(_rootDir);
        foreach (var asset in assets)
        {
            try
            {
                LoadScriptContent(asset.text, runner, asset.name);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载脚本文件 {asset.name} 时出错: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 加载脚本内容
    /// </summary>
    /// <param name="scriptContent">脚本内容</param>
    /// <param name="runner">运行器</param>
    /// <param name="filePath">文件路径(调试用)</param>
    /// <returns>异步任务</returns>
    private void LoadScriptContent(string scriptContent,
        Runner runner, string filePath = "")
    {
        try
        {
            // 创建词法分析器
            var lexer = new Lexer(scriptContent);

            // 创建语法分析器
            var parser = new Parser(lexer);
            var scriptNode = parser.Parse();

            // 注册脚本节点
            runner.RegisterScript(scriptNode);
        }
        catch (Exception ex)
        {
            string fileInfo = string.IsNullOrEmpty(filePath) ? "" : $" (文件: {filePath})";
            Console.WriteLine($"解析脚本内容时出错{fileInfo}: {ex.Message}");
        }
    }
}
