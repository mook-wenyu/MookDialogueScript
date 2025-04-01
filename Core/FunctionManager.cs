using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MookDialogueScript
{
    /// <summary>
    /// 标记可在脚本中调用的函数
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ScriptFuncAttribute : Attribute
    {
        /// <summary>
        /// 在脚本中使用的函数名
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 函数描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 创建一个新的脚本函数特性
        /// </summary>
        /// <param name="name">在脚本中使用的函数名，如果为空则使用方法名</param>
        /// <param name="description">函数描述</param>
        public ScriptFuncAttribute(string name = "", string description = "")
        {
            Name = name;
            Description = description;
        }
    }

    public class FunctionManager
    {
        private Dictionary<string, Func<List<RuntimeValue>, Task<RuntimeValue>>> _compiledBuiltinFunctions = new Dictionary<string, Func<List<RuntimeValue>, Task<RuntimeValue>>>();
        private Dictionary<string, (MethodInfo Method, string Description)> _scriptFuncMethods = new Dictionary<string, (MethodInfo, string)>();

        /// <summary>
        /// 编译函数
        /// </summary>
        /// <param name="function">函数</param>
        /// <returns>编译后的函数</returns>
        private Func<List<RuntimeValue>, Task<RuntimeValue>> CompileFunction(Delegate function)
        {
            var methodInfo = function.Method;
            var parameters = methodInfo.GetParameters();

            return async (args) =>
            {
                // 准备参数
                var nativeArgs = new object[parameters.Length];
                for (int i = 0; i < parameters.Length && i < args.Count; i++)
                {
                    nativeArgs[i] = ConvertToNativeType(args[i], parameters[i].ParameterType);
                }

                // 调用函数
                object? result = null;
                if (methodInfo.ReturnType.IsAssignableFrom(typeof(Task<object>)) ||
                    methodInfo.ReturnType.IsGenericType && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    // 异步函数
                    dynamic task = function.DynamicInvoke(nativeArgs);
                    result = await task;
                }
                else if (methodInfo.ReturnType == typeof(Task))
                {
                    // 无返回值的异步函数
                    dynamic task = function.DynamicInvoke(nativeArgs);
                    await task;
                    result = null;
                }
                else
                {
                    // 同步函数
                    result = function.DynamicInvoke(nativeArgs);
                }

                // 转换返回值
                return ConvertToRuntimeValue(result);
            };
        }

        /// <summary>
        /// 扫描并注册所有标记了ScriptFunc特性的方法
        /// </summary>
        public void ScanAndRegisterScriptFunctions()
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
                ScanAssemblyForScriptFunctions(assembly);
            }
        }

        /// <summary>
        /// 扫描程序集中的脚本函数
        /// </summary>
        /// <param name="assembly">要扫描的程序集</param>
        private void ScanAssemblyForScriptFunctions(Assembly assembly)
        {
            try
            {
                // 获取程序集中的所有类型
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    // 获取类型中的所有公共静态方法
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

                    foreach (var method in methods)
                    {
                        // 检查方法是否标记了ScriptFunc特性
                        var attribute = method.GetCustomAttribute<ScriptFuncAttribute>();
                        if (attribute != null)
                        {
                            // 确定函数名
                            string funcName = attribute.Name ?? method.Name;

                            // 注册方法
                            _scriptFuncMethods[funcName] = (method, attribute.Description);

                            // 创建函数适配器
                            var adapter = CreateFunctionAdapter(method);
                            if (adapter != null)
                            {
                                // 注册函数适配器
                                _compiledBuiltinFunctions[funcName] = adapter;
                            }
                            else
                            {
                                Logger.LogError($"无法为函数 '{funcName}' 创建适配器，此函数将不可用");
                                throw new InvalidOperationException($"无法为函数 '{funcName}' 创建适配器，此函数将不可用");
                            }
                        }
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
        /// 为方法创建适配器函数
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <returns>适配器函数</returns>
        private Func<List<RuntimeValue>, Task<RuntimeValue>> CreateFunctionAdapter(MethodInfo method)
        {
            try
            {
                // 获取方法参数信息
                var parameters = method.GetParameters();
                var returnType = method.ReturnType;

                // 创建适配器函数
                Func<List<RuntimeValue>, Task<RuntimeValue>> adapter = async (args) =>
                {
                    try
                    {
                        // 准备调用参数
                        var nativeArgs = new object[parameters.Length];
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            // 如果参数索引超出传入的参数列表长度，使用默认值
                            if (i >= args.Count)
                            {
                                // 如果参数有默认值
                                if (parameters[i].HasDefaultValue)
                                {
                                    nativeArgs[i] = parameters[i].DefaultValue;
                                }
                                else
                                {
                                    // 否则使用类型的默认值
                                    nativeArgs[i] = parameters[i].ParameterType.IsValueType ?
                                        Activator.CreateInstance(parameters[i].ParameterType) : null;
                                }
                            }
                            else
                            {
                                nativeArgs[i] = ConvertToNativeType(args[i], parameters[i].ParameterType);
                            }
                        }

                        // 调用方法
                        object? result = null;

                        // 处理异步方法
                        if (returnType.IsAssignableFrom(typeof(Task<object>)) ||
                            (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)))
                        {
                            // 异步函数，有返回值
                            dynamic task = method.Invoke(null, nativeArgs);
                            result = await task;
                        }
                        else if (returnType == typeof(Task))
                        {
                            // 异步函数，无返回值
                            dynamic task = method.Invoke(null, nativeArgs);
                            await task;
                            result = null;
                        }
                        else
                        {
                            // 同步函数
                            result = method.Invoke(null, nativeArgs);
                        }

                        // 转换结果为RuntimeValue
                        return ConvertToRuntimeValue(result);
                    }
                    catch (Exception ex)
                    {
                        // 获取真正的异常（如果是TargetInvocationException）
                        if (ex is TargetInvocationException && ex.InnerException != null)
                        {
                            Logger.LogError($"调用函数时出错: {ex.InnerException.Message}");
                            throw new InvalidOperationException($"调用函数时出错: {ex.InnerException.Message}", ex.InnerException);
                        }
                        Logger.LogError($"调用函数时出错: {ex.Message}");
                        throw new InvalidOperationException($"调用函数时出错: {ex.Message}", ex);
                    }
                };

                return adapter;
            }
            catch (Exception ex)
            {
                Logger.LogError($"创建函数适配器时出错: {ex.Message}");
                throw new InvalidOperationException($"创建函数适配器时出错: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 注册对象实例的方法作为脚本函数
        /// </summary>
        /// <param name="objectName">对象名称（用作函数名称的前缀）</param>
        /// <param name="instance">对象实例</param>
        public void RegisterObjectFunctions(string objectName, object instance)
        {
            if (instance == null)
            {
                Logger.LogError($"注册对象实例的方法作为脚本函数时，对象实例不能为null");
                throw new ArgumentNullException(nameof(instance), "对象实例不能为null");
            }

            // 获取对象的类型
            Type type = instance.GetType();

            // 获取所有公共实例方法
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var method in methods)
            {
                // 跳过Object类的方法
                if (method.DeclaringType == typeof(object))
                    continue;

                // 函数名为：objectName__methodName
                string funcName = $"{objectName}__{method.Name}";

                // 注册方法
                try
                {
                    // 存储方法信息（用于展示在帮助中）
                    _scriptFuncMethods[funcName] = (method, $"对象 {objectName} 的方法");

                    // 创建实例方法的适配器
                    Func<List<RuntimeValue>, Task<RuntimeValue>> adapter = async (args) =>
                    {
                        try
                        {
                            // 获取方法参数信息
                            var parameters = method.GetParameters();

                            // 准备调用参数
                            var nativeArgs = new object[parameters.Length];
                            for (int i = 0; i < parameters.Length; i++)
                            {
                                // 如果参数索引超出传入的参数列表长度，使用默认值
                                if (i >= args.Count)
                                {
                                    // 如果参数有默认值
                                    if (parameters[i].HasDefaultValue)
                                    {
                                        nativeArgs[i] = parameters[i].DefaultValue;
                                    }
                                    else
                                    {
                                        // 否则使用类型的默认值
                                        nativeArgs[i] = parameters[i].ParameterType.IsValueType ?
                                            Activator.CreateInstance(parameters[i].ParameterType) : null;
                                    }
                                }
                                else
                                {
                                    nativeArgs[i] = ConvertToNativeType(args[i], parameters[i].ParameterType);
                                }
                            }

                            // 调用方法
                            object? result = null;
                            var returnType = method.ReturnType;

                            // 处理异步方法
                            if (returnType.IsAssignableFrom(typeof(Task<object>)) ||
                                (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)))
                            {
                                // 异步函数，有返回值
                                dynamic task = method.Invoke(instance, nativeArgs);
                                result = await task;
                            }
                            else if (returnType == typeof(Task))
                            {
                                // 异步函数，无返回值
                                dynamic task = method.Invoke(instance, nativeArgs);
                                await task;
                                result = null;
                            }
                            else
                            {
                                // 同步函数
                                result = method.Invoke(instance, nativeArgs);
                            }

                            // 转换结果为RuntimeValue
                            return ConvertToRuntimeValue(result);
                        }
                        catch (Exception ex)
                        {
                            // 获取真正的异常（如果是TargetInvocationException）
                            if (ex is TargetInvocationException && ex.InnerException != null)
                            {
                                Logger.LogError($"调用函数 '{funcName}' 时出错: {ex.InnerException.Message}");
                                throw new InvalidOperationException($"调用函数 '{funcName}' 时出错: {ex.InnerException.Message}", ex.InnerException);
                            }
                            Logger.LogError($"调用函数 '{funcName}' 时出错: {ex.Message}");
                            throw new InvalidOperationException($"调用函数 '{funcName}' 时出错: {ex.Message}", ex);
                        }
                    };

                    // 注册适配器
                    _compiledBuiltinFunctions[funcName] = adapter;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"注册 {funcName} 时出错: {ex.Message}");
                    throw new InvalidOperationException($"注册 {funcName} 时出错: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 注册内置函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="function">函数</param>
        public void RegisterFunction(string name, Delegate function)
        {
            // 创建适配器
            _compiledBuiltinFunctions[name] = CompileFunction(function);
        }

        /// <summary>
        /// 注册脚本函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="function">函数</param>
        public void RegisterScriptFunction(string name, Func<List<RuntimeValue>, Task<RuntimeValue>> function)
        {
            _compiledBuiltinFunctions[name] = function;
        }

        /// <summary>
        /// 获取所有已注册的脚本函数信息
        /// </summary>
        /// <returns>函数名和描述的字典</returns>
        public Dictionary<string, string> GetRegisteredScriptFunctions()
        {
            var result = new Dictionary<string, string>();

            // 添加通过特性注册的函数
            foreach (var pair in _scriptFuncMethods)
            {
                result[pair.Key] = pair.Value.Description;
            }

            // 添加手动注册的函数
            foreach (var key in _compiledBuiltinFunctions.Keys)
            {
                if (!result.ContainsKey(key))
                {
                    result[key] = "手动注册的函数";
                }
            }

            return result;
        }

        /// <summary>
        /// 调用函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="args">参数</param>
        /// <returns>返回值</returns>
        public async Task<RuntimeValue> CallFunction(string name, List<RuntimeValue> args)
        {
            if (_compiledBuiltinFunctions.ContainsKey(name))
            {
                return await _compiledBuiltinFunctions[name](args);
            }
            else
            {
                Logger.LogError($"函数 '{name}' 未找到");
                throw new InvalidOperationException($"函数 '{name}' 未找到");
            }
        }

        /// <summary>
        /// 将运行时值转换为原生类型
        /// </summary>
        /// <param name="value">运行时值</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>原生类型</returns>
        private object? ConvertToNativeType(RuntimeValue value, Type targetType)
        {
            if (value.Type == RuntimeValue.ValueType.Null)
            {
                return null;
            }
            
            if (targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(int) || targetType == typeof(long))
            {
                if (value.Type != RuntimeValue.ValueType.Number)
                {
                    Logger.LogError($"期望数字类型，但得到了 {value.Type}");
                    throw new InvalidCastException($"期望数字类型，但得到了 {value.Type}");
                }
                return Convert.ChangeType(value.Value, targetType);
            }
            else if (targetType == typeof(string))
            {
                if (value.Type != RuntimeValue.ValueType.String)
                {
                    Logger.LogError($"期望字符串类型，但得到了 {value.Type}");
                    throw new InvalidCastException($"期望字符串类型，但得到了 {value.Type}");
                }
                return value.Value;
            }
            else if (targetType == typeof(bool))
            {
                if (value.Type != RuntimeValue.ValueType.Boolean)
                {
                    Logger.LogError($"期望布尔类型，但得到了 {value.Type}");
                    throw new InvalidCastException($"期望布尔类型，但得到了 {value.Type}");
                }
                return value.Value;
            }
            else if (targetType.IsClass || (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) != null))
            {
                // 对于引用类型和可空值类型，null是有效的
                return null;
            }
            else
            {
                Logger.LogError($"不支持的参数类型转换: {targetType.Name}");
                throw new NotSupportedException($"不支持的参数类型转换: {targetType.Name}");
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

            if (value is double || value is int || value is float)
                return new RuntimeValue(Convert.ToDouble(value));
            else if (value is string strValue)
                return new RuntimeValue(strValue);
            else if (value is bool boolValue)
                return new RuntimeValue(boolValue);
            else
            {
                Logger.LogError($"不支持的返回值类型: {value.GetType().Name}");
                throw new NotSupportedException($"不支持的返回值类型: {value.GetType().Name}");
            }
        }

        /// <summary>
        /// 输出日志
        /// </summary>
        /// <param name="message">日志消息</param>
        [ScriptFunc("cs_log", "输出日志消息")]
        public static void CSLog(string message)
        {
            Logger.Log($"[LOG] {message}");
        }

        /// <summary>
        /// 连接字符串
        /// </summary>
        /// <param name="str1">第一个字符串</param>
        /// <param name="str2">第二个字符串（可选）</param>
        /// <param name="str3">第三个字符串（可选）</param>
        /// <param name="str4">第四个字符串（可选）</param>
        /// <param name="str5">第五个字符串（可选）</param>
        /// <param name="str6">第六个字符串（可选）</param>
        /// <param name="str7">第七个字符串（可选）</param>
        /// <param name="str8">第八个字符串（可选）</param>
        /// <returns>连接后的字符串</returns>
        [ScriptFunc("concat", "连接字符串")]
        public static string Concat(string str1, string str2 = "", string str3 = "", string str4 = "", string str5 = "", string str6 = "", string str7 = "", string str8 = "")
        {
            return str1 + str2 + str3 + str4 + str5 + str6 + str7 + str8;
        }
        
        /// <summary>
        /// 返回一个介于 0 和 1 之间的随机数
        /// </summary>
        /// <param name="digits">小数位数</param>
        /// <returns>随机浮点数</returns>
        [ScriptFunc("random")]
        public static double Random_Float(int digits = 2)
        {
            return Math.Round(new Random().NextDouble(), digits);
        }

        /// <summary>
        /// 返回一个介于 min 和 max 之间的随机数
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="digits">小数位数</param>
        /// <returns>随机浮点数</returns>
        [ScriptFunc("random_range")]
        public static double Random_Float_Range(float min, float max, int digits = 2)
        {
            return Math.Round(new Random().NextDouble() * (max - min) + min, digits);
        }

        /// <summary>
        /// 介于 1 和 sides 之间（含 1 和 sides ）的随机整数
        /// </summary>
        /// <param name="sides">骰子面数</param>
        /// <returns>随机整数</returns>
        [ScriptFunc("dice")]
        public static int Random_Dice(int sides)
        {
            return new Random().Next(1, sides + 1);
        }
    }
}