using UnityEditor;
using UnityEngine;
using System.IO;
using ExcelDataReader;
using System.Text;
using System.Data;
using System;
using System.Collections.Generic;

public class ExcelToJsonConverter : EditorWindow
{
    private string _excelPath = "";
    private string _jsonOutputPath = "Assets/Resources/Data/";
    private bool _showProgressBar = false;
    private float _progress = 0f;

    [MenuItem("Excel/Excel转Json")]
    public static void ShowWindow()
    {
        var window = GetWindow<ExcelToJsonConverter>("Excel转Json");
        window.minSize = new Vector2(500, 200);
        
        // 居中窗口（仅X轴），保持Y轴位置不变
        float screenWidth = Screen.currentResolution.width;
        float windowX = (screenWidth - 500) / 5;
        float windowY = (window.position.y + 60);
        window.position = new Rect(windowX, windowY, 500, 250);
    }

    private void OnEnable()
    {
        // 注册编码提供程序，以支持中文等非ASCII字符
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    private void OnGUI()
    {
        GUILayout.Label("Excel转Json", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // 输入 Excel 文件路径
        GUILayout.BeginHorizontal();
        GUILayout.Label("Excel文件路径:", GUILayout.Width(120));
        _excelPath = EditorGUILayout.TextField(_excelPath, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("浏览", GUILayout.Width(100)))
        {
            _excelPath = EditorUtility.OpenFilePanel("Select Excel File", "", "xlsx,xls");
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 输出路径设置
        GUILayout.BeginHorizontal();
        GUILayout.Label("JSON输出路径:", GUILayout.Width(120));
        _jsonOutputPath = EditorGUILayout.TextField(_jsonOutputPath, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("选择", GUILayout.Width(100)))
        {
            string selectedPath = EditorUtility.SaveFolderPanel("选择JSON输出文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // 转换为相对路径（如果在Assets内）
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    _jsonOutputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
                else
                {
                    _jsonOutputPath = selectedPath;
                }
            }
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(15);

        // 转换按钮
        EditorGUI.BeginDisabledGroup(_showProgressBar);
        if (GUILayout.Button("转 换", GUILayout.Height(30)))
        {
            if (File.Exists(_excelPath))
            {
                try
                {
                    // 确保输出目录存在
                    if (!Directory.Exists(_jsonOutputPath))
                    {
                        Directory.CreateDirectory(_jsonOutputPath);
                    }

                    _showProgressBar = true;
                    _progress = 0f;
                    EditorApplication.delayCall += () => 
                    {
                        try
                        {
                            ConvertExcelToJson(_excelPath);
                            AssetDatabase.Refresh();
                            Debug.Log("Excel 转换完成！JSON 文件保存至: " + _jsonOutputPath);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"转换失败: {ex.Message}\n{ex.StackTrace}");
                        }
                        finally
                        {
                            _showProgressBar = false;
                        }
                    };
                }
                catch (Exception ex)
                {
                    Debug.LogError($"初始化转换失败: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError("Excel 文件不存在！");
            }
        }
        EditorGUI.EndDisabledGroup();

        // 显示进度条
        if (_showProgressBar)
        {
            EditorGUI.ProgressBar(new Rect(10, GUILayoutUtility.GetLastRect().y + 30, position.width - 20, 20), _progress, "转换中...");
            GUILayout.Space(30);
            Repaint();
        }
    }

    private void ConvertExcelToJson(string filePath)
    {
        try
        {
            // 检查文件是否被占用
            FileStream testStream = null;
            try
            {
                testStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                Debug.LogError("Excel文件当前被其他程序占用，请关闭相关程序后再试！");
                EditorUtility.DisplayDialog("错误", "Excel文件当前被其他程序占用，请关闭相关程序后再试！", "确定");
                return;
            }
            finally
            {
                testStream?.Dispose();
            }

            // 读取 Excel 文件
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                        {
                            UseHeaderRow = true // 使用第一行作为字段名
                        }
                    });

                    int totalTables = result.Tables.Count;
                    // 遍历所有工作表
                    for (int tableIndex = 0; tableIndex < totalTables; tableIndex++)
                    {
                        DataTable table = result.Tables[tableIndex];
                        _progress = (float)tableIndex / totalTables;
                        
                        if (table.Rows.Count == 0 || table.Columns.Count == 0)
                        {
                            Debug.LogWarning($"表 {table.TableName} 为空，已跳过");
                            continue;
                        }

                        StringBuilder json = new StringBuilder();
                        json.Append("[");

                        // 遍历数据行（跳过表头）
                        for (int i = 0; i < table.Rows.Count; i++)
                        {
                            json.Append("{");
                            for (int j = 0; j < table.Columns.Count; j++)
                            {
                                string key = table.Columns[j].ColumnName;
                                string value = table.Rows[i][j].ToString();

                                json.Append($"\"{EscapeJsonString(key)}\": ");

                                // 处理空值
                                if (string.IsNullOrEmpty(value) || value.ToLower() == "null")
                                {
                                    json.Append("null");
                                }
                                // 处理布尔值
                                else if (value.ToLower() == "true" || value.ToLower() == "false")
                                {
                                    json.Append(value.ToLower());
                                }
                                // 处理数值
                                else if (double.TryParse(value, out double num))
                                {
                                    json.Append(num.ToString());
                                }
                                // 处理JSON数组或对象（如果值以[或{开头）
                                else if ((value.StartsWith("[") && value.EndsWith("]")) || 
                                         (value.StartsWith("{") && value.EndsWith("}")))
                                {
                                    json.Append(value);
                                }
                                // 处理字符串
                                else
                                {
                                    json.Append($"\"{EscapeJsonString(value)}\"");
                                }

                                if (j < table.Columns.Count - 1) json.Append(",");
                            }
                            json.Append("}");
                            if (i < table.Rows.Count - 1) json.Append(",");
                        }

                        json.Append("]");

                        // 保存 JSON 文件
                        string outputFile = Path.Combine(_jsonOutputPath, $"{SanitizeFileName(table.TableName)}.json");
                        File.WriteAllText(outputFile, json.ToString(), Encoding.UTF8);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"转换失败: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // 转义JSON字符串
    private string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t")
                  .Replace("\b", "\\b")
                  .Replace("\f", "\\f");
    }

    // 净化文件名（移除不合法字符）
    private string SanitizeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }
}