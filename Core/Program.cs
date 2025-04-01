using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MookDialogueScript
{
    /// <summary>
    /// 游戏系统类 - 只包含一些静态变量
    /// </summary>
    public static class GameSystem
    {
        // 静态变量示例
        [ScriptVar("GameVersion", "游戏版本号")]
        public static string GameVersion { get; } = "1.0.0";

        [ScriptVar("GameDifficulty", "游戏难度", isReadOnly: false)]
        public static int GameDifficulty { get; set; } = 1;

        [ScriptVar("IsDebugMode", "调试模式")]
        public static bool IsDebugMode = false;
    }

    /// <summary>
    /// 玩家类 - 用于演示对象属性和方法
    /// </summary>
    public class Player
    {
        // 属性
        public string Name { get; set; }
        public int Level { get; set; }
        public int Health { get; set; }
        public bool IsAlive { get; set; }

        // 构造函数
        public Player(string name, int level = 1)
        {
            Name = name;
            Level = level;
            Health = level * 10;
            IsAlive = true;
        }

        // 方法
        public string GetStatus()
        {
            return $"{Name} (Lv.{Level}) - HP: {Health}";
        }

        public void TakeDamage(int amount)
        {
            Health -= amount;
            if (Health <= 0)
            {
                Health = 0;
                IsAlive = false;
            }
        }

        public void Heal(int amount)
        {
            if (IsAlive)
            {
                Health += amount;
                Health = Math.Min(Health, Level * 10);
            }
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

                // 注册玩家对象
                var player = new Player("勇者");
                // 使用Runner的RegisterVariable方法注册玩家属性
                dialogueManager.RegisterObject("player", player);

                // 注册一些测试变量
                dialogueManager.RegisterVariable("gold", new RuntimeValue(100));
                dialogueManager.RegisterVariable("has_key", new RuntimeValue(false));

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
                            string text = await dialogueManager.BuildChoiceText(choiceNode);
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
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// 注册事件处理器
        /// </summary>
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
                Console.WriteLine($"[选择] 选择了选项 {index + 1}");
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
                    Console.WriteLine($"{text}");
                }
            };
        }
    }
}
