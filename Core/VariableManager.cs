using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace MookDialogueScript
{
    /// <summary>
    /// 标记可在脚本中访问的变量
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class ScriptVarAttribute : Attribute
    {
        /// <summary>
        /// 在脚本中使用的变量名
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 变量描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 变量是否只读
        /// </summary>
        public bool IsReadOnly { get; }

        /// <summary>
        /// 创建一个新的脚本变量特性
        /// </summary>
        /// <param name="name">在脚本中使用的变量名，如果为null则使用属性/字段名</param>
        /// <param name="description">变量描述</param>
        /// <param name="isReadOnly">是否只读</param>
        public ScriptVarAttribute(string name = "", string description = "", bool isReadOnly = false)
        {
            Name = name;
            Description = description;
            IsReadOnly = isReadOnly;
        }
    }

    public class VariableManager
    {
        private Dictionary<string, RuntimeValue> _scriptVariables = new Dictionary<string, RuntimeValue>();
        private Dictionary<string, (Func<object> getter, Action<object> setter)> _builtinVariables = new Dictionary<string, (Func<object> getter, Action<object> setter)>();
        private Dictionary<string, string> _variableDescriptions = new Dictionary<string, string>();

        /// <summary>
        /// 注册内置变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="getter">获取变量值的委托</param>
        /// <param name="setter">设置变量值的委托</param>
        /// <param name="description">变量描述</param>
        public void RegisterBuiltinVariable(string name, Func<object> getter, Action<object> setter, string description = "")
        {
            _builtinVariables[name] = (getter, setter);
            if (!string.IsNullOrEmpty(description))
            {
                _variableDescriptions[name] = description;
            }
        }

        /// <summary>
        /// 扫描并注册所有标记了ScriptVar特性的静态属性和字段
        /// </summary>
        public void ScanAndRegisterScriptVariables()
        {
            // 获取当前程序集
            var currentAssembly = Assembly.GetExecutingAssembly();

            // 获取所有引用了当前程序集的程序集
            var referencingAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetReferencedAssemblies().Any(r => r.FullName == currentAssembly.FullName))
                .ToList();

            // 添加当前程序集
            referencingAssemblies.Add(currentAssembly);

            // 扫描所有程序集
            foreach (var assembly in referencingAssemblies)
            {
                ScanAssemblyForScriptVariables(assembly);
            }
        }

        /// <summary>
        /// 扫描程序集中的脚本变量
        /// </summary>
        /// <param name="assembly">要扫描的程序集</param>
        private void ScanAssemblyForScriptVariables(Assembly assembly)
        {
            try
            {
                // 获取程序集中的所有类型
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    try
                    {
                        // 扫描静态属性
                        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static);
                        foreach (var property in properties)
                        {
                            try
                            {
                                var attribute = property.GetCustomAttribute<ScriptVarAttribute>();
                                if (attribute != null)
                                {
                                    // 确定变量名
                                    string varName = attribute.Name ?? property.Name;

                                    // 创建getter
                                    Func<object> getter = () => property.GetValue(null);

                                    // 创建setter (如果不是只读的)
                                    Action<object> setter = attribute.IsReadOnly ?
                                        (obj) => Logger.LogError($"变量 '{varName}' 是只读的") :
                                        (obj) => property.SetValue(null, obj);

                                    // 注册变量
                                    RegisterBuiltinVariable(varName, getter, setter, attribute.Description);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"扫描属性 {property.Name} 时出错: {ex.Message}");
                                throw new InvalidOperationException($"扫描属性 {property.Name} 时出错: {ex.Message}", ex);
                            }
                        }

                        // 扫描静态字段
                        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
                        foreach (var field in fields)
                        {
                            try
                            {
                                var attribute = field.GetCustomAttribute<ScriptVarAttribute>();
                                if (attribute != null)
                                {
                                    // 确定变量名
                                    string varName = attribute.Name ?? field.Name;

                                    // 创建getter
                                    Func<object> getter = () => field.GetValue(null);

                                    // 创建setter (如果不是只读的和const)
                                    Action<object> setter;
                                    if (attribute.IsReadOnly || field.IsInitOnly)
                                    {
                                        setter = (obj) => throw new InvalidOperationException($"变量 '{varName}' 是只读的");
                                    }
                                    else
                                    {
                                        setter = (obj) => field.SetValue(null, obj);
                                    }

                                    // 注册变量
                                    RegisterBuiltinVariable(varName, getter, setter, attribute.Description);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"扫描字段 {field.Name} 时出错: {ex.Message}");
                                throw new InvalidOperationException($"扫描字段 {field.Name} 时出错: {ex.Message}", ex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"扫描类型 {type.FullName} 时出错: {ex.Message}");
                        throw new InvalidOperationException($"扫描类型 {type.FullName} 时出错: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"扫描程序集 {assembly.FullName} 时出错: {ex.Message}");
                throw new InvalidOperationException($"扫描程序集 {assembly.FullName} 时出错: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 注册对象实例的属性作为脚本变量
        /// </summary>
        /// <param name="objectName">对象名称（用作变量名称的前缀）</param>
        /// <param name="instance">对象实例</param>
        public void RegisterObjectProperties(string objectName, object instance)
        {
            if (instance == null){
                Logger.LogError("对象实例不能为null");
                throw new ArgumentNullException(nameof(instance), "对象实例不能为null");
            }

            // 获取对象的类型
            Type type = instance.GetType();

            // 获取所有公共实例属性
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                // 变量名为：objectName__propertyName
                string varName = $"{objectName}__{property.Name}";

                // 创建getter
                Func<object> getter = () => property.GetValue(instance);

                // 创建setter
                Action<object> setter;
                if (!property.CanWrite)
                {
                    setter = (obj) => Logger.LogError($"属性 '{varName}' 是只读的");
                }
                else
                {
                    setter = (obj) => property.SetValue(instance, obj);
                }

                // 注册变量
                RegisterBuiltinVariable(varName, getter, setter, $"对象 {objectName} 的属性");
            }
        }

        /// <summary>
        /// 获取所有脚本变量
        /// </summary>
        /// <returns>脚本变量</returns>
        public Dictionary<string, RuntimeValue> GetScriptVariables()
        {
            return _scriptVariables;
        }

        /// <summary>
        /// 设置所有脚本变量
        /// </summary>
        /// <param name="variables">脚本变量</param>
        public void LoadScriptVariables(Dictionary<string, RuntimeValue> variables)
        {
            _scriptVariables = variables;
        }

        /// <summary>
        /// 获取所有已注册的变量信息
        /// </summary>
        /// <returns>变量名和描述的字典</returns>
        public Dictionary<string, string> GetRegisteredVariables()
        {
            var result = new Dictionary<string, string>(_variableDescriptions);

            // 添加脚本变量
            foreach (var key in _scriptVariables.Keys)
            {
                if (!result.ContainsKey(key))
                {
                    result[key] = "脚本变量";
                }
            }

            return result;
        }

        /// <summary>
        /// 设置变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public void SetVariable(string name, RuntimeValue value)
        {
            if (_builtinVariables.ContainsKey(name))
            {
                _builtinVariables[name].setter(ConvertToNativeType(value));
            }
            else
            {
                _scriptVariables[name] = value;
            }
        }

        /// <summary>
        /// 获取变量值
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>变量值</returns>
        public RuntimeValue GetVariable(string name)
        {
            if (_builtinVariables.ContainsKey(name))
            {
                return ConvertToRuntimeValue(_builtinVariables[name].getter());
            }

            if (!_scriptVariables.ContainsKey(name))
            {
                Logger.LogError($"变量 '{name}' 未找到");
                throw new KeyNotFoundException($"变量 '{name}' 未找到");
            }

            return _scriptVariables[name];
        }

        /// <summary>
        /// 检查变量是否存在
        /// </summary>
        /// <param name="name">变量名</param>
        /// <returns>是否存在</returns>
        public bool HasVariable(string name)
        {
            return _scriptVariables.ContainsKey(name) || _builtinVariables.ContainsKey(name);
        }

        /// <summary>
        /// 将运行时值转换为对象
        /// </summary>
        /// <param name="value">运行时值</param>
        /// <returns>对象</returns>
        private object? ConvertToNativeType(RuntimeValue value)
        {
            switch (value.Type)
            {
                case RuntimeValue.ValueType.Number:
                    double numValue = (double)value.Value;
                    // 检查是否是整数
                    if (Math.Abs(numValue - Math.Round(numValue)) < double.Epsilon)
                    {
                        // 如果是整数且在int范围内
                        if (numValue >= int.MinValue && numValue <= int.MaxValue)
                            return (int)numValue;
                        // 如果是整数但超出int范围
                        else
                            return (long)numValue;
                    }
                    // 如果是小数
                    return numValue;

                case RuntimeValue.ValueType.String:
                    return value.Value;

                case RuntimeValue.ValueType.Boolean:
                    return value.Value;
                    
                case RuntimeValue.ValueType.Null:
                    return null;

                default:
                    Logger.LogError($"不支持的运行时值类型: {value.Type}");
                    throw new NotSupportedException($"不支持的运行时值类型: {value.Type}");
            }
        }

        /// <summary>
        /// 将对象转换为运行时值
        /// </summary>
        /// <param name="value">对象</param>
        /// <returns>运行时值</returns>
        private RuntimeValue ConvertToRuntimeValue(object? value)
        {
            if (value == null)
                return RuntimeValue.Null;
            
            if (value is double || value is int || value is float || value is long)
                return new RuntimeValue(Convert.ToDouble(value));
            else if (value is string)
                return new RuntimeValue((string)value);
            else if (value is bool)
                return new RuntimeValue((bool)value);
            else
            {
                Logger.LogError($"不支持的内置变量类型: {value.GetType().Name}");
                throw new NotSupportedException($"不支持的内置变量类型: {value.GetType().Name}");
            }
        }
    }
}