using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MookDialogueScript
{
    /// <summary>
    /// 测试用的静态变量类
    /// </summary>
    public static class TestStaticVars
    {
        [ScriptVar(description: "游戏版本号")]
        public static string GameVersion { get; } = "1.0.0";

        [ScriptVar(description: "游戏难度", isReadOnly: false)]
        public static int GameDifficulty { get; set; } = 1;

        [ScriptVar("MAX_PLAYERS", "最大玩家数", true)]
        public static readonly int MaxPlayers = 4;

        [ScriptVar("DEBUG_MODE", "调试模式")]
        public static bool IsDebugMode = false;
    }

    /// <summary>
    /// 测试用的对象类
    /// </summary>
    public class TestObject
    {
        public string Name { get; set; }

        public int Age { get; set; }

        public bool IsActive { get; set; }

        public TestObject(string name, int age = 20)
        {
            Name = name;
            Age = age;
            IsActive = true;
        }

        public string SayHello(string person)
        {
            return $"你好，{person}！我是{Name}。";
        }

        public double Add(double a, double b)
        {
            return a + b;
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                // 创建对话管理器
                var dialogueManager = new Runner();

                // 注册事件处理器
                RegisterEventHandlers(dialogueManager);

                // 注册一些测试变量
                dialogueManager.RegisterVariable("playerName", new RuntimeValue("玩家"));
                dialogueManager.RegisterVariable("playerLevel", new RuntimeValue(10));
                dialogueManager.RegisterVariable("isHero", new RuntimeValue(true));

                // 开始执行对话
                await dialogueManager.StartDialogue("start");

                // 主循环，手动控制对话流程
                bool isRunning = true;
                while (isRunning && dialogueManager.IsInDialogue)
                {
                    // 如果有选项，处理选项选择
                    if (dialogueManager.HasChoices)
                    {
                        var choices = dialogueManager.GetCurrentChoices();
                        Console.WriteLine("\n请选择一个选项 (输入数字):");

                        // 显示所有选项
                        for (int i = 0; i < choices.Count; i++)
                        {
                            var choiceNode = choices[i];
                            // 简单显示选项文本
                            string text = "选项";
                            if (choiceNode.Text.Count > 0)
                            {
                                text = await dialogueManager.BuildChoiceText(choiceNode);
                            }
                            Console.WriteLine($"{i + 1}. {text}");
                        }

                        Console.Write("\n> ");
                        string choiceInput = Console.ReadLine();

                        if (choiceInput?.ToLower() == "q")
                        {
                            isRunning = false;
                            await dialogueManager.EndDialogue();
                            continue;
                        }

                        if (int.TryParse(choiceInput, out int choice) && choice >= 1 && choice <= choices.Count)
                        {
                            await dialogueManager.SelectChoice(choice - 1);
                        }
                        else
                        {
                            Console.WriteLine("无效的选择，请重新输入。");
                        }
                    }
                    else
                    {
                        Console.WriteLine("\n按Enter继续对话，输入'q'退出...");
                        Console.Write("> ");
                        string input = Console.ReadLine();

                        if (input?.ToLower() == "q")
                        {
                            isRunning = false;
                            await dialogueManager.EndDialogue();
                            continue;
                        }

                        // 继续对话
                        await dialogueManager.Continue();
                    }
                }

                Console.WriteLine("\n脚本执行完毕");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }

            // 示例：保存和加载脚本变量
            ExampleSaveLoadVariables();
        }

        /// <summary>
        /// 注册事件处理器
        /// </summary>
        /// <param name="dialogueManager">对话管理器</param>
        private static void RegisterEventHandlers(Runner dialogueManager)
        {
            dialogueManager.OnDialogueStarted += () =>
            {
                Console.WriteLine("对话开始");
            };

            dialogueManager.OnNodeStarted += (nodeName) =>
            {
                Console.WriteLine($"进入节点: {nodeName}");
            };

            dialogueManager.OnOptionSelected += (choice, index) =>
            {
                Console.WriteLine($"[选择] 选择了选项 {index + 1} 节点: {dialogueManager.BuildChoiceText(choice)}");
            };

            dialogueManager.OnDialogueCompleted += () =>
            {
                Console.WriteLine("对话结束");
            };

            dialogueManager.OnChoicesDisplayed += async (choices) =>
            {
                Console.WriteLine("\n可选项:");
                for (int i = 0; i < choices.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {await dialogueManager.BuildChoiceText(choices[i])}");
                }
            };

            dialogueManager.OnDialogueDisplayed += async (content) =>
            {
                if (content is DialogueNode dialogue)
                {
                    string text = await dialogueManager.BuildDialogueText(dialogue);
                    Console.WriteLine($"{dialogue.Speaker}{(dialogue.Emotion != null ? $" [{dialogue.Emotion}]" : "")}: {text}");
                }
                else if (content is NarrationNode narration)
                {
                    string text = await dialogueManager.BuildNarrationText(narration);
                    Console.WriteLine(text);
                }
            };
        }


        /// <summary>
        /// 获取当前时间
        /// </summary>
        /// <returns>当前时间字符串</returns>
        [ScriptFunc(description: "获取当前系统时间")]
        public static string GetCurrentTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// 计算两个数的和
        /// </summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>两数之和</returns>
        [ScriptFunc("plus", "计算两个数的和")]
        public static double Plus(double a, double b)
        {
            return a + b;
        }

        /// <summary>
        /// 计算两个数的乘积
        /// </summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>两数之乘积</returns>
        [ScriptFunc("multiply", "计算两个数的乘积")]
        public static double Multiply(double a, double b)
        {
            return a * b;
        }

        /// <summary>
        /// 计算两个数的商
        /// </summary>
        /// <param name="a">被除数</param>
        /// <param name="b">除数</param>
        /// <returns>两数之商</returns>
        [ScriptFunc("divide", "计算两个数的商")]
        public static double Divide(double a, double b)
        {
            if (b == 0)
            {
                Console.WriteLine("错误：除数不能为零！");
                return 0;
            }
            return a / b;
        }

        // 示例：注册对象实例的函数
        private static void ExampleRegisterObjectFunctions()
        {
            Console.WriteLine("===== 对象实例函数示例 =====");

            // 创建对话上下文
            var context = new DialogueContext();

            // 创建测试对象
            var testObj = new TestObject("测试对象");

            // 注册测试对象的函数
            context.RegisterObjectFunctions("test", testObj);

            // 显示注册的函数
            var functions = context.GetRegisteredFunctions();
            Console.WriteLine("注册的函数:");
            foreach (var func in functions)
            {
                Console.WriteLine($"- {func.Key}: {func.Value}");
            }

            Console.WriteLine("\n调用函数示例:");
            try
            {
                // 调用对象的方法
                var result1 = context.CallFunction("test$SayHello", new List<RuntimeValue> { new RuntimeValue("小明") }).Result;
                Console.WriteLine($"test$SayHello(\"小明\") = {result1}");

                var result2 = context.CallFunction("test$Add", new List<RuntimeValue> { new RuntimeValue(10.5), new RuntimeValue(20.3) }).Result;
                Console.WriteLine($"test$Add(10.5, 20.3) = {result2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }

            Console.WriteLine();
        }

        // 示例：注册对象实例的属性
        private static void ExampleRegisterObjectProperties()
        {
            Console.WriteLine("===== 对象实例属性示例 =====");

            // 创建对话上下文
            var context = new DialogueContext();

            // 创建测试对象
            var testObj = new TestObject("张三", 25);

            // 注册测试对象的属性
            context.RegisterObjectProperties("player", testObj);

            // 显示注册的变量
            var variables = context.GetRegisteredVariables();
            Console.WriteLine("注册的变量:");
            foreach (var var in variables)
            {
                Console.WriteLine($"- {var.Key}: {var.Value}");
            }

            Console.WriteLine("\n读取和修改变量示例:");
            try
            {
                // 读取对象属性
                var name = context.GetVariable("player$Name");
                var age = context.GetVariable("player$Age");
                var isActive = context.GetVariable("player$IsActive");

                Console.WriteLine($"player$Name = {name}");
                Console.WriteLine($"player$Age = {age}");
                Console.WriteLine($"player$IsActive = {isActive}");

                // 修改对象属性
                context.SetVariable("player$Name", new RuntimeValue("李四"));
                context.SetVariable("player$Age", new RuntimeValue(30));

                // 再次读取
                name = context.GetVariable("player$Name");
                age = context.GetVariable("player$Age");

                Console.WriteLine($"\n修改后:");
                Console.WriteLine($"player$Name = {name}");
                Console.WriteLine($"player$Age = {age}");

                // 检查对象实例是否真的被修改
                Console.WriteLine($"\n对象实例的实际值:");
                Console.WriteLine($"testObj.Name = {testObj.Name}");
                Console.WriteLine($"testObj.Age = {testObj.Age}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }

            Console.WriteLine();
        }

        // 示例：使用ScriptVarAttribute注册的静态变量
        private static void ExampleScriptVarAttribute()
        {
            Console.WriteLine("===== 静态变量特性示例 =====");

            // 创建对话上下文
            var context = new DialogueContext();

            // 显示注册的变量
            var variables = context.GetRegisteredVariables();
            Console.WriteLine("注册的变量:");
            foreach (var var in variables)
            {
                Console.WriteLine($"- {var.Key}: {var.Value}");
            }

            Console.WriteLine("\n读取和修改变量示例:");
            try
            {
                // 读取静态变量
                var version = context.GetVariable("GameVersion");
                var difficulty = context.GetVariable("GameDifficulty");
                var maxPlayers = context.GetVariable("MAX_PLAYERS");
                var debugMode = context.GetVariable("DEBUG_MODE");

                Console.WriteLine($"GameVersion = {version}");
                Console.WriteLine($"GameDifficulty = {difficulty}");
                Console.WriteLine($"MAX_PLAYERS = {maxPlayers}");
                Console.WriteLine($"DEBUG_MODE = {debugMode}");

                // 修改静态变量
                context.SetVariable("GameDifficulty", new RuntimeValue(3));
                context.SetVariable("DEBUG_MODE", new RuntimeValue(true));

                // 再次读取
                difficulty = context.GetVariable("GameDifficulty");
                debugMode = context.GetVariable("DEBUG_MODE");

                Console.WriteLine($"\n修改后:");
                Console.WriteLine($"GameDifficulty = {difficulty}");
                Console.WriteLine($"DEBUG_MODE = {debugMode}");

                // 检查静态变量是否真的被修改
                Console.WriteLine($"\n静态变量的实际值:");
                Console.WriteLine($"TestStaticVars.GameDifficulty = {TestStaticVars.GameDifficulty}");
                Console.WriteLine($"TestStaticVars.IsDebugMode = {TestStaticVars.IsDebugMode}");

                // 尝试修改只读变量 (会抛出异常)
                try
                {
                    context.SetVariable("GameVersion", new RuntimeValue("2.0.0"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n尝试修改只读变量:");
                    Console.WriteLine($"错误: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }

            Console.WriteLine();
        }

        // 示例：保存和加载脚本变量
        private static void ExampleSaveLoadVariables()
        {
            Console.WriteLine("===== 保存和加载变量示例 =====");

            // 创建对话上下文
            var context = new DialogueContext();

            // 设置一些变量
            context.SetVariable("playerName", new RuntimeValue("王五"));
            context.SetVariable("playerLevel", new RuntimeValue(5));
            context.SetVariable("playerGold", new RuntimeValue(1000));
            context.SetVariable("isQuest1Completed", new RuntimeValue(true));

            // 显示当前变量
            Console.WriteLine("初始变量值:");
            DisplayVariables(context);

            // 获取变量状态以便保存
            var savedVariables = context.GetScriptVariables();
            Console.WriteLine($"已保存 {savedVariables.Count} 个变量");

            // 修改一些变量
            context.SetVariable("playerName", new RuntimeValue("赵六"));
            context.SetVariable("playerLevel", new RuntimeValue(10));
            context.SetVariable("playerGold", new RuntimeValue(500));

            // 显示修改后的变量
            Console.WriteLine("\n修改后的变量值:");
            DisplayVariables(context);

            // 加载先前保存的变量
            context.LoadScriptVariables(savedVariables);

            // 显示恢复后的变量
            Console.WriteLine("\n加载存档后的变量值:");
            DisplayVariables(context);

            Console.WriteLine();
        }

        // 辅助方法：显示当前上下文中的所有变量
        private static void DisplayVariables(DialogueContext context)
        {
            var variables = context.GetScriptVariables();
            foreach (var variable in variables)
            {
                Console.WriteLine($"- {variable.Key} = {variable.Value}");
            }
        }
    }
}
