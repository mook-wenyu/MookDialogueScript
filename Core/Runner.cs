using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MookDialogueScript
{
    /// <summary>
    /// 运行器，负责运行对话
    /// </summary>
    public class Runner
    {
        private readonly DialogueContext _context;
        private readonly Interpreter _interpreter;
        private readonly IDialogueLoader _loader;

        private string _currentSectionId = "";
        /// <summary>
        /// 获取当前对话的唯一标识符，如果为null则表示不在对话中
        /// </summary>
        public string CurrentSectionId => _currentSectionId;
        /// <summary>
        /// 是否在对话中
        /// </summary>
        public bool IsInDialogue => !string.IsNullOrEmpty(_currentSectionId);

        // 是否正在执行中（等待用户输入等状态）
        private bool _isExecuting = false;

        // 条件状态
        private Dictionary<ConditionNode, (int branch, int elifIndex, int contentIndex)> _conditionStates =
            new Dictionary<ConditionNode, (int, int, int)>();

        // 当前收集的选项列表
        private List<ChoiceNode> _currentChoices = new List<ChoiceNode>();

        // 是否正在收集选项
        private bool _isCollectingChoices = false;

        // 是否有可选择的选项
        public bool HasChoices => _isCollectingChoices && _currentChoices.Count > 0;

        // AST节点执行栈，用于跟踪嵌套执行
        private Stack<(ASTNode node, int index)> _executionStack = new Stack<(ASTNode node, int index)>();

        /// <summary>
        /// 对话开始事件
        /// </summary>
        public event Action? OnDialogueStarted;

        /// <summary>
        /// 节点开始事件
        /// </summary>
        public event Action<string>? OnNodeStarted;

        /// <summary>
        /// 选项选择事件
        /// </summary>
        public event Action<ChoiceNode, int>? OnOptionSelected;

        /// <summary>
        /// 对话显示事件
        /// </summary>
        public event Action<ContentNode>? OnDialogueDisplayed;

        /// <summary>
        /// 选项显示事件
        /// </summary>
        public event Action<List<ChoiceNode>>? OnChoicesDisplayed;

        /// <summary>
        /// 对话完成事件
        /// </summary>
        public event Action? OnDialogueCompleted;

        public Runner() : this(new DefaultDialogueLoader())
        {
        }

        public Runner(string rootDir) : this(new DefaultDialogueLoader(rootDir))
        {
        }

        public Runner(IDialogueLoader loader)
        {
            _context = new DialogueContext();
            _interpreter = new Interpreter(_context);
            _loader = loader;

            // 重置状态
            _conditionStates.Clear();
            _currentChoices.Clear();
            _isCollectingChoices = false;
            // 清空执行栈
            _executionStack.Clear();

            // 加载脚本
            _loader.LoadScripts(this);
        }

        /// <summary>
        /// 注册脚本
        /// </summary>
        /// <param name="script">脚本</param>
        public void RegisterScript(ScriptNode script)
        {
            _interpreter.RegisterNodes(script);
        }

        /// <summary>
        /// 获取节点中的下一个内容
        /// </summary>
        /// <param name="node">当前节点</param>
        /// <param name="currentIndex">当前内容索引</param>
        /// <returns>下一个内容索引，如果没有下一个内容则返回-1</returns>
        public int GetNextContentIndex(NodeDefinitionNode node, int currentIndex)
        {
            if (currentIndex < node.Content.Count - 1)
            {
                return currentIndex + 1;
            }
            return -1;
        }

        /// <summary>
        /// 注册变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public void RegisterVariable(string name, RuntimeValue value)
        {
            _context.SetVariable(name, value);
        }

        /// <summary>
        /// 获取变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>变量值</returns>
        public RuntimeValue GetVariable(string name)
        {
            return _context.GetVariable(name);
        }

        /// <summary>
        /// 注册对象
        /// </summary>
        /// <param name="name">对象名</param>
        /// <param name="instance">对象实例</param>
        public void RegisterObject(string name, object instance)
        {
            _context.RegisterObjectProperties(name, instance);
        }

        /// <summary>
        /// 设置变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public void SetVariable(string name, RuntimeValue value)
        {
            _context.SetVariable(name, value);
        }

        /// <summary>
        /// 注册函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="function">函数</param>
        public void RegisterFunction(string name, Delegate function)
        {
            _context.RegisterFunction(name, function);
        }

        /// <summary>
        /// 获取所有已注册的函数
        /// </summary>
        /// <returns>函数名和描述的字典</returns>
        public Dictionary<string, string> GetRegisteredFunctions()
        {
            return _context.GetRegisteredFunctions();
        }

        /// <summary>
        /// 注册对象函数
        /// </summary>
        /// <param name="objectName">对象名</param>
        /// <param name="instance">对象实例</param>
        public void RegisterObjectFunction(string objectName, object instance)
        {
            _context.RegisterObjectFunctions(objectName, instance);
        }

        /// <summary>
        /// 获取所有已注册的变量信息
        /// </summary>
        /// <returns>变量名和描述的字典</returns>
        public Dictionary<string, string> GetRegisteredVariables()
        {
            return _context.GetRegisteredVariables();
        }

        /// <summary>
        /// 获取所有脚本变量（用于保存游戏状态）
        /// </summary>
        /// <returns>脚本变量的字典</returns>
        public Dictionary<string, RuntimeValue> GetScriptVariables()
        {
            return _context.GetScriptVariables();
        }

        /// <summary>
        /// 加载脚本变量（用于加载游戏状态）
        /// </summary>
        /// <param name="variables">要加载的脚本变量字典</param>
        public void LoadScriptVariables(Dictionary<string, RuntimeValue> variables)
        {
            _context.LoadScriptVariables(variables);
        }

        /// <summary>
        /// 开始执行对话
        /// </summary>
        /// <param name="startNodeName">起始节点名称，默认为"start"</param>
        /// <param name="startContentIndex">起始内容索引，默认为0</param>
        /// <param name="force">是否强制开始，忽略当前执行状态</param>
        /// <returns>异步任务</returns>
        public async Task StartDialogue(string startNodeName = "start", int startContentIndex = 0, bool force = false)
        {
            // 如果当前有对话在进行，不允许开始新对话
            if (!force && !string.IsNullOrEmpty(_currentSectionId))
            {
                Logger.LogError("当前对话尚未结束，无法开始新的对话");
                throw new InvalidOperationException("当前对话尚未结束，无法开始新的对话");
            }

            NodeDefinitionNode startNode;
            try
            {
                startNode = _context.GetNode(startNodeName);
            }
            catch (Exception ex)
            {
                Logger.LogError($"找不到起始节点 '{startNodeName}'");
                throw new KeyNotFoundException($"找不到起始节点 '{startNodeName}'", ex);
            }

            // 生成新的对话标识符并记录小节开始
            _currentSectionId = Guid.NewGuid().ToString("N");

            // 触发对话开始事件
            OnDialogueStarted?.Invoke();

            // 重置选项收集状态
            _currentChoices.Clear();
            _isCollectingChoices = false;

            // 清空执行栈
            _executionStack.Clear();

            if (startContentIndex < 0 || startContentIndex >= startNode.Content.Count)
            {
                Logger.LogError($"起始内容索引无效: {startContentIndex}");
                throw new ArgumentOutOfRangeException(nameof(startContentIndex), $"起始内容索引无效: {startContentIndex}");
            }
            // 将当前节点压入栈
            _executionStack.Push((startNode, startContentIndex));

            // 触发节点开始事件
            OnNodeStarted?.Invoke(startNodeName);

            // 显示第一个内容
            await Continue();
        }

        /// <summary>
        /// 继续执行当前节点
        /// </summary>
        /// <returns>异步任务</returns>
        public async Task Continue()
        {
            if (_isExecuting)
            {
                Logger.LogError("当前正在执行中，请等待执行完成");
                throw new InvalidOperationException("当前正在执行中，请等待执行完成");
            }

            // 如果执行栈为空，无法继续执行
            if (_executionStack.Count == 0)
            {
                Logger.LogError("没有当前执行上下文");
                throw new InvalidOperationException("没有当前执行上下文");
            }

            // 如果正在收集选项，并且已经有选项，则处理选项
            if (_isCollectingChoices && _currentChoices.Count > 0)
            {
                Logger.LogError("当前正在等待选择选项");
                throw new InvalidOperationException("当前正在等待选择选项");
            }

            // 获取当前执行上下文
            var (node, index) = _executionStack.Peek();

            // 根据当前节点类型执行不同的逻辑
            if (node is NodeDefinitionNode currentNode)
            {
                // 处理单个内容
                await ProcessNextContent(currentNode, index);
            }
            else if (node is ChoiceNode choiceNode)
            {
                // 处理选项中的单个内容
                await ProcessNextContent(choiceNode, index);
            }
            else if (node is ConditionNode conditionNode)
            {
                // 处理条件内容
                await ProcessConditionContent(conditionNode, index);
            }
            else
            {
                Logger.LogError($"未知的节点类型: {node.GetType().Name}");
                throw new NotSupportedException($"未知的节点类型: {node.GetType().Name}");
            }
        }

        /// <summary>
        /// 处理下一个内容
        /// </summary>
        /// <param name="contentContainer">内容容器</param>
        /// <param name="index">当前索引</param>
        private async Task ProcessNextContent(ASTNode contentContainer, int index)
        {
            List<ContentNode> contents;

            if (contentContainer is NodeDefinitionNode nodeDef)
            {
                contents = nodeDef.Content;
            }
            else if (contentContainer is ChoiceNode choice)
            {
                contents = choice.Content;
            }
            else
            {
                Logger.LogError($"不支持的内容容器类型: {contentContainer.GetType().Name}");
                throw new NotSupportedException($"不支持的内容容器类型: {contentContainer.GetType().Name}");
            }

            // 检查索引是否有效
            if (index < 0 || index >= contents.Count)
            {
                // 如果是选项内容结束，回到主节点
                if (contentContainer is ChoiceNode)
                {
                    _executionStack.Pop(); // 弹出选项

                    // 继续执行主节点的下一个内容
                    await Continue();
                    return;
                }

                // 如果是主节点结束，结束对话
                await EndDialogue();
                return;
            }

            // 获取当前内容
            ContentNode content = contents[index];

            // 处理通用内容逻辑
            await ProcessContentCommon(content, contentContainer, index, contents);
        }

        /// <summary>
        /// 处理条件内容
        /// </summary>
        /// <param name="conditionNode">条件节点</param>
        /// <param name="index">当前索引</param>
        private async Task ProcessConditionContent(ConditionNode conditionNode, int index)
        {
            // 获取或初始化条件状态，如果条件节点不存在，则评估主条件
            if (!_conditionStates.TryGetValue(conditionNode, out var state))
            {
                // 评估主条件
                var condition = await _interpreter.EvaluateExpression(conditionNode.Condition);
                if (condition.Type != RuntimeValue.ValueType.Boolean)
                {
                    Logger.LogError("条件必须计算为布尔类型");
                    throw new InvalidCastException("条件必须计算为布尔类型");
                }

                if ((bool)condition.Value)
                {
                    state = (0, 0, 0); // then分支
                }
                else
                {
                    // 检查elif分支
                    bool elifExecuted = false;
                    for (int i = 0; i < conditionNode.ElifBranches.Count; i++)
                    {
                        var (elifCondition, _) = conditionNode.ElifBranches[i];
                        condition = await _interpreter.EvaluateExpression(elifCondition);
                        if (condition.Type != RuntimeValue.ValueType.Boolean)
                        {
                            Logger.LogError("Elif 条件必须计算为布尔类型");
                            throw new InvalidCastException("Elif 条件必须计算为布尔类型");
                        }

                        if ((bool)condition.Value)
                        {
                            state = (1, i, 0); // elif分支
                            elifExecuted = true;
                            break;
                        }
                    }

                    // 如果没有elif分支为真，且有else分支，执行else分支
                    if (!elifExecuted && conditionNode.ElseBranch != null)
                    {
                        state = (2, 0, 0); // else分支
                    }
                    else if (!elifExecuted)
                    {
                        // 没有条件匹配，跳过条件节点
                        _executionStack.Pop(); // 弹出条件节点
                        await Continue();
                        return;
                    }
                }
                _conditionStates[conditionNode] = state;
            }

            // 根据分支状态获取内容列表
            List<ContentNode> contents;
            if (state.branch == 0) // then分支
            {
                contents = conditionNode.ThenBranch;
            }
            else if (state.branch == 1) // elif分支
            {
                contents = conditionNode.ElifBranches[state.elifIndex].Item2;
            }
            else // else分支
            {
                contents = conditionNode.ElseBranch;
            }

            // 检查索引是否有效
            if (index < 0 || index >= contents.Count)
            {
                // 条件执行完毕，弹出条件节点
                _executionStack.Pop();
                _conditionStates.Remove(conditionNode);

                // 继续执行
                await Continue();
                return;
            }

            // 获取当前内容
            ContentNode content = contents[index];

            // 处理内容并更新条件状态
            await ProcessContentCommon(content, conditionNode, index, contents);

            // 如果是对话或旁白，更新条件状态
            if (content is DialogueNode || content is NarrationNode)
            {
                _conditionStates[conditionNode] = (state.branch, state.elifIndex, index + 1);
            }
        }

        /// <summary>
        /// 处理公共内容逻辑
        /// </summary>
        /// <param name="content">内容节点</param>
        /// <param name="contentContainer">内容容器</param>
        /// <param name="index">内容索引</param>
        /// <param name="contents">内容列表</param>
        private async Task ProcessContentCommon(
            ContentNode content,
            ASTNode contentContainer,
            int index,
            List<ContentNode> contents)
        {
            // 处理命令节点
            if (content is CommandNode commandNode)
            {
                // 设置执行状态
                _isExecuting = true;
                try
                {
                    // 执行命令
                    string nextNodeName = await _interpreter.ExecuteCommand(commandNode);
                    if (!string.IsNullOrEmpty(nextNodeName))
                    {
                        await JumpToNode(nextNodeName);
                        return;
                    }

                    // 更新执行栈
                    _executionStack.Pop();
                    _executionStack.Push((contentContainer, index + 1));

                    _isExecuting = false;
                    // 继续处理下一个内容
                    await Continue();
                }
                finally
                {
                    _isExecuting = false;
                }
            }
            // 处理选项节点
            else if (content is ChoiceNode choiceNode)
            {
                // 开始收集选项
                _isCollectingChoices = true;
                _currentChoices.Clear();

                // 收集当前选项
                await EvaluateChoiceCondition(choiceNode);
                _currentChoices.Add(choiceNode);

                // 收集后续选项
                int nextIndex = index + 1;
                while (nextIndex < contents.Count && contents[nextIndex] is ChoiceNode nextChoice)
                {
                    await EvaluateChoiceCondition(nextChoice);
                    _currentChoices.Add(nextChoice);
                    nextIndex++;
                }

                // 更新执行栈
                _executionStack.Pop();
                _executionStack.Push((contentContainer, nextIndex));

                // 触发选项显示事件
                if (_currentChoices.Count > 0)
                {
                    OnChoicesDisplayed?.Invoke(_currentChoices);
                }
                else
                {
                    _isCollectingChoices = false;
                    _isExecuting = false;
                    await Continue();
                }
                _isExecuting = false;
            }
            // 处理嵌套条件节点
            else if (content is ConditionNode conditionNode)
            {
                // 更新执行栈
                _executionStack.Pop();
                _executionStack.Push((contentContainer, index + 1)); // 更新容器索引
                _executionStack.Push((conditionNode, 0)); // 压入条件节点

                // 处理条件内容
                _isExecuting = false;
                await ProcessConditionContent(conditionNode, 0);
                _isExecuting = false;
            }
            // 处理对话或旁白
            else
            {
                // 设置执行状态
                _isExecuting = true;
                try
                {
                    // 执行内容
                    await ExecuteContent(content);

                    // 更新执行栈
                    _executionStack.Pop();
                    _executionStack.Push((contentContainer, index + 1));

                    // 检查下一个内容是否为选项
                    var nextContentType = await HasNextContent();
                    if (nextContentType == 2) // 2表示选项内容
                    {
                        _isExecuting = false;
                        await Continue();
                    }
                }
                finally
                {
                    _isExecuting = false;
                }
            }
        }


        /// <summary>
        /// 处理内容节点
        /// </summary>
        /// <param name="content">内容节点</param>
        /// <returns>下一个要执行的节点名称，如果有跳转则返回该节点名称，否则返回空字符串</returns>
        public async Task<string> ExecuteContent(ContentNode content)
        {
            switch (content)
            {
                case DialogueNode d:
                    ExecuteDialogue(d);
                    return string.Empty;

                case NarrationNode n:
                    ExecuteNarration(n);
                    return string.Empty;

                case ChoiceNode c:
                    await EvaluateChoiceCondition(c);
                    return string.Empty;

                case ConditionNode c:
                    return await ExecuteCondition(c);

                case CommandNode cmd:
                    return await _interpreter.ExecuteCommand(cmd);

                default:
                    Logger.LogError($"未知的内容类型 {content.GetType().Name}");
                    throw new NotSupportedException($"未知的内容类型 {content.GetType().Name}");
            }
        }

        /// <summary>
        /// 执行对话节点
        /// </summary>
        /// <param name="node">对话节点</param>
        private void ExecuteDialogue(DialogueNode node)
        {
            // 触发对话显示事件
            OnDialogueDisplayed?.Invoke(node);
        }

        /// <summary>
        /// 执行旁白节点
        /// </summary>
        /// <param name="node">旁白节点</param>
        private void ExecuteNarration(NarrationNode node)
        {
            // 触发对话显示事件
            OnDialogueDisplayed?.Invoke(node);
        }

        /// <summary>
        /// 评估选项条件
        /// </summary>
        /// <param name="node">选项节点</param>
        /// <returns>如果选项条件满足，返回true；否则返回false</returns>
        public async Task<bool> EvaluateChoiceCondition(ChoiceNode node)
        {
            // 如果有条件，先检查条件
            if (node.Condition != null)
            {
                var condition = await _interpreter.EvaluateExpression(node.Condition);
                if (condition.Type != RuntimeValue.ValueType.Boolean)
                {
                    Logger.LogError("选项条件必须计算为布尔类型");
                    throw new InvalidCastException("选项条件必须计算为布尔类型");
                }
                return (bool)condition.Value;
            }
            return true;
        }

        /// <summary>
        /// 执行条件节点
        /// </summary>
        /// <param name="node">条件节点</param>
        /// <returns>下一个要执行的节点名称，如果有跳转则返回该节点名称，否则返回空字符串</returns>
        public async Task<string> ExecuteCondition(ConditionNode node)
        {
            // 检查主条件
            var condition = await _interpreter.EvaluateExpression(node.Condition);
            if (condition.Type != RuntimeValue.ValueType.Boolean)
            {
                Logger.LogError("条件必须计算为布尔类型");
                throw new InvalidCastException("条件必须计算为布尔类型");
            }

            if ((bool)condition.Value)
            {
                // 如果条件为真，返回第一个内容的执行结果
                if (node.ThenBranch.Count > 0)
                {
                    return await ExecuteContent(node.ThenBranch[0]);
                }
                return string.Empty;
            }

            // 检查elif分支
            foreach (var (elifCondition, elifContent) in node.ElifBranches)
            {
                condition = await _interpreter.EvaluateExpression(elifCondition);
                if (condition.Type != RuntimeValue.ValueType.Boolean)
                {
                    Logger.LogError("Elif条件必须计算为布尔类型");
                    throw new InvalidCastException("Elif条件必须计算为布尔类型");
                }

                if ((bool)condition.Value)
                {
                    // 如果elif条件为真，返回第一个内容的执行结果
                    if (elifContent.Count > 0)
                    {
                        return await ExecuteContent(elifContent[0]);
                    }
                    return string.Empty;
                }
            }

            // 如果所有条件都不满足，执行else分支
            if (node.ElseBranch is { Count: > 0 })
            {
                return await ExecuteContent(node.ElseBranch[0]);
            }

            return string.Empty;
        }

        /// <summary>
        /// 选择选项
        /// </summary>
        /// <param name="index">选项索引</param>
        /// <returns>异步任务</returns>
        public async Task SelectChoice(int index)
        {
            if (_isExecuting)
            {
                Logger.LogError("当前正在执行中，请等待执行完成");
                throw new InvalidOperationException("当前正在执行中，请等待执行完成");
            }

            if (!_isCollectingChoices || _currentChoices.Count == 0)
            {
                Logger.LogError("当前没有可选择的选项");
                throw new InvalidOperationException("当前没有可选择的选项");
            }

            if (index < 0 || index >= _currentChoices.Count)
            {
                Logger.LogError($"选项索引无效: {index}");
                throw new ArgumentOutOfRangeException(nameof(index), $"选项索引无效: {index}");
            }

            var selectedChoice = _currentChoices[index];

            // 触发选项选择事件
            OnOptionSelected?.Invoke(selectedChoice, index);

            // 重置选项收集状态
            _isCollectingChoices = false;
            _currentChoices.Clear();

            // 如果选项有内容，将其压入执行栈
            if (selectedChoice.Content.Count > 0)
            {
                // 将选项内容压入栈
                _executionStack.Push((selectedChoice, 0));

                // 执行选项的第一个内容
                await Continue();
            }
            else
            {
                // 选项没有内容，继续执行当前节点的下一个内容
                await Continue();
            }
        }

        /// <summary>
        /// 跳转到指定节点
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>异步任务</returns>
        public async Task JumpToNode(string nodeName)
        {
            try
            {
                var node = _context.GetNode(nodeName);
                _currentChoices.Clear();
                _isCollectingChoices = false;
                _isExecuting = false;

                // 清空执行栈
                _executionStack.Clear();

                // 将新节点压入栈
                _executionStack.Push((node, 0));

                // 触发节点开始事件
                OnNodeStarted?.Invoke(nodeName);

                // 开始执行
                await Continue();
            }
            catch (Exception ex)
            {
                Logger.LogError($"跳转到节点 {nodeName} 失败: {ex.Message}");
                throw new Exception($"跳转到节点 {nodeName} 失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取当前节点的内容
        /// </summary>
        /// <returns>内容节点列表</returns>
        public List<ContentNode> GetCurrentNodeContents()
        {
            if (_executionStack.Count == 0 || !(_executionStack.Peek().node is NodeDefinitionNode node))
            {
                Logger.LogError("没有当前节点");
                throw new InvalidOperationException("没有当前节点");
            }

            return node.Content;
        }

        /// <summary>
        /// 获取当前选项列表
        /// </summary>
        /// <returns>选项列表</returns>
        public List<ChoiceNode> GetCurrentChoices()
        {
            return _currentChoices;
        }

        /// <summary>
        /// 结束对话
        /// </summary>
        /// <param name="force">是否强制结束，忽略当前执行状态</param>
        /// <returns>异步任务</returns>
        public async Task EndDialogue(bool force = false)
        {
            if (!force && _isExecuting)
            {
                Logger.LogError("当前正在执行中，请等待执行完成");
                throw new InvalidOperationException("当前正在执行中，请等待执行完成");
            }

            _currentChoices.Clear();
            _isCollectingChoices = false;
            _currentSectionId = string.Empty;

            // 清空执行栈
            _executionStack.Clear();

            OnDialogueCompleted?.Invoke();
            await Task.CompletedTask;
        }

        /// <summary>
        /// 构建对话文本
        /// </summary>
        /// <param name="dialogueNode">对话节点</param>
        /// <returns>构建好的对话文本</returns>
        public async Task<string> BuildDialogueText(DialogueNode dialogueNode)
        {
            return await _interpreter.BuildText(dialogueNode.Text);
        }

        /// <summary>
        /// 构建旁白文本
        /// </summary>
        /// <param name="narrationNode">旁白节点</param>
        /// <returns>构建好的旁白文本</returns>
        public async Task<string> BuildNarrationText(NarrationNode narrationNode)
        {
            return await _interpreter.BuildText(narrationNode.Text);
        }

        /// <summary>
        /// 构建选项文本
        /// </summary>
        /// <param name="choiceNode">选项节点</param>
        /// <returns>选项文本</returns>
        public async Task<string> BuildChoiceText(ChoiceNode choiceNode)
        {
            return await _interpreter.BuildText(choiceNode.Text);
        }

        /// <summary>
        /// 构建文本
        /// </summary>
        /// <param name="textSegments">文本段列表</param>
        /// <param name="callback">回调函数</param>
        public void BuildText(List<TextSegmentNode> textSegments, Action<string> callback)
        {
            _interpreter.BuildText(textSegments, callback);
        }

        /// <summary>
        /// 检查是否还有下一个可显示的内容（对话、选项或跳转命令）
        /// </summary>
        /// <returns>
        /// 返回整数表示下一个内容的类型：
        /// 0 - 没有任何内容了
        /// 1 - 对话内容
        /// 2 - 选项内容
        /// 3 - 跳转命令
        /// 4 - 其他命令
        /// </returns>
        public async Task<int> HasNextContent()
        {
            // 如果执行栈为空，说明没有更多内容
            if (_executionStack.Count == 0)
            {
                return 0;
            }

            // 获取当前执行上下文的副本
            var stackCopy = new Stack<(ASTNode node, int index)>(_executionStack.Reverse());

            while (stackCopy.Count > 0)
            {
                var (currentNode, currentIndex) = stackCopy.Pop();
                List<ContentNode> contents = new List<ContentNode>();

                // 根据节点类型获取内容列表
                if (currentNode is NodeDefinitionNode nodeDef)
                {
                    contents = nodeDef.Content;
                }
                else if (currentNode is ChoiceNode choice)
                {
                    contents = choice.Content;
                }
                else if (currentNode is ConditionNode condition)
                {
                    // 对于条件节点，需要评估条件并获取对应分支的内容
                    contents = await GetConditionBranchContents(condition);
                    if (contents == null)
                    {
                        continue; // 没有匹配的条件分支，检查下一个栈帧
                    }
                }
                else
                {
                    continue; // 不支持的节点类型，检查下一个栈帧
                }

                // 检查从当前索引开始的内容
                for (int i = currentIndex; i < contents.Count; i++)
                {
                    var content = contents[i];
                    var result = await CheckContentType(content);
                    if (result > 0) // 找到了有效内容
                    {
                        return result;
                    }
                    else if (result == -1) // 需要继续检查条件节点
                    {
                        stackCopy.Push((content as ConditionNode, 0));
                        break; // 退出当前内容循环，处理新压入的条件节点
                    }
                }
            }

            return 0; // 没有找到任何内容
        }

        /// <summary>
        /// 获取条件节点对应分支的内容
        /// </summary>
        private async Task<List<ContentNode>> GetConditionBranchContents(ConditionNode condition)
        {
            // 评估主条件
            var result = await _interpreter.EvaluateExpression(condition.Condition);
            if (result.Type != RuntimeValue.ValueType.Boolean)
            {
                Logger.LogError("条件必须计算为布尔类型");
                throw new InvalidCastException("条件必须计算为布尔类型");
            }

            if ((bool)result.Value)
            {
                return condition.ThenBranch;
            }

            // 检查elif分支
            foreach (var (elifCondition, elifContent) in condition.ElifBranches)
            {
                var elifResult = await _interpreter.EvaluateExpression(elifCondition);
                if (elifResult.Type != RuntimeValue.ValueType.Boolean)
                {
                    Logger.LogError("Elif条件必须计算为布尔类型");
                    throw new InvalidCastException("Elif条件必须计算为布尔类型");
                }

                if ((bool)elifResult.Value)
                {
                    return elifContent;
                }
            }

            // 如果有else分支则返回else分支内容
            if (condition.ElseBranch != null)
            {
                return condition.ElseBranch;
            }

            return null; // 没有匹配的条件分支
        }

        /// <summary>
        /// 检查内容节点的类型
        /// </summary>
        /// <returns>
        /// 返回值说明：
        /// >0 - 内容类型（1-对话，2-选项，3-跳转，4-其他命令）
        /// 0 - 继续检查下一个内容
        /// -1 - 需要检查条件节点
        /// </returns>
        private async Task<int> CheckContentType(ContentNode content)
        {
            // 对话和旁白是直接可显示的
            if (content is DialogueNode || content is NarrationNode)
            {
                return 1; // 对话内容
            }
            // 选项需要检查是否有可用的选项
            else if (content is ChoiceNode choiceNode)
            {
                if (await EvaluateChoiceCondition(choiceNode))
                {
                    return 2; // 选项内容
                }
            }
            // 命令节点需要检查是否是跳转命令
            else if (content is CommandNode commandNode)
            {
                if (content is JumpCommandNode)
                {
                    return 3; // 跳转命令
                }
                return 4; // 其他命令
            }
            // 条件节点需要递归检查
            else if (content is ConditionNode)
            {
                return -1; // 需要继续检查条件节点
            }

            return 0; // 继续检查下一个内容
        }
    }

}