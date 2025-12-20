using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HaiLiDrvDemo
{
    /// <summary>
    /// 板块管理器 - 管理板块配置的保存和加载
    /// </summary>
    public class BoardManager
    {
        private string configFilePath;
        private List<BoardConfig> boards = new List<BoardConfig>();
        
        /// <summary>
        /// 获取配置文件的完整路径
        /// </summary>
        public string ConfigFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(configFilePath))
                {
                    string appDir = Application.StartupPath;
                    string configDir = Path.Combine(appDir, "Config");
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }
                    configFilePath = Path.Combine(configDir, "Boards.json");
                }
                return configFilePath;
            }
        }
        
        public BoardManager()
        {
            // ConfigFilePath 属性会在首次访问时自动初始化
        }
        
        /// <summary>
        /// 保存板块配置
        /// </summary>
        public void SaveBoards(List<BoardConfig> boardList)
        {
            try
            {
                boards = new List<BoardConfig>(boardList);
                
                // 使用简单的JSON格式保存（.NET 3.5兼容）
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[");
                
                for (int i = 0; i < boards.Count; i++)
                {
                    var board = boards[i];
                    sb.AppendLine("  {");
                    // 使用 StringBuilder 完全替代 string.Format，提高性能
                    string boardName = EscapeJson(board.Name ?? "");
                    sb.Append("    \"Name\": \"");
                    sb.Append(boardName);
                    sb.AppendLine("\",");
                    sb.AppendLine("    \"StockCodes\": [");
                    
                    for (int j = 0; j < board.StockCodes.Count; j++)
                    {
                        string stockCode = EscapeJson(board.StockCodes[j] ?? "");
                        sb.Append("      \"");
                        sb.Append(stockCode);
                        sb.Append("\"");
                        if (j < board.StockCodes.Count - 1)
                            sb.Append(",");
                        sb.AppendLine();
                    }
                    
                    sb.AppendLine("    ],");
                    sb.AppendLine("    \"StockNames\": {");
                    
                    // 保存股票名称映射
                    int nameIndex = 0;
                    foreach (var kvp in board.StockNames ?? new Dictionary<string, string>())
                    {
                        string key = EscapeJson(kvp.Key ?? "");
                        string value = EscapeJson(kvp.Value ?? "");
                        sb.Append("      \"");
                        sb.Append(key);
                        sb.Append("\": \"");
                        sb.Append(value);
                        sb.Append("\"");
                        if (nameIndex < board.StockNames.Count - 1)
                            sb.Append(",");
                        sb.AppendLine();
                        nameIndex++;
                    }
                    
                    sb.AppendLine("    },");
                    // 使用 StringBuilder 替代 string.Format
                    sb.Append("    \"Width\": ");
                    sb.Append(board.Width);
                    sb.AppendLine(",");
                    sb.Append("    \"Height\": ");
                    sb.Append(board.Height);
                    sb.Append("  }");
                    if (i < boards.Count - 1)
                        sb.Append(",");
                    sb.AppendLine();
                }
                
                sb.AppendLine("]");
                
                // 确保配置文件路径已初始化
                string filePath = ConfigFilePath;
                Logger.Instance.Info(string.Format("准备保存配置文件: {0}", filePath));
                
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                Logger.Instance.Info(string.Format("板块配置已保存: {0} 个板块到文件: {1}", boards.Count, filePath));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("保存板块配置失败: {0}", ex.Message));
            }
        }
        
        /// <summary>
        /// 加载板块配置
        /// </summary>
        public List<BoardConfig> LoadBoards()
        {
            try
            {
                // 确保配置文件路径已初始化
                string filePath = ConfigFilePath;
                Logger.Instance.Info(string.Format("准备加载配置文件: {0}", filePath));
                
                if (!File.Exists(filePath))
                {
                    Logger.Instance.Info(string.Format("板块配置文件不存在: {0}，返回空列表", filePath));
                    return new List<BoardConfig>();
                }
                
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                Logger.Instance.Info(string.Format("成功读取配置文件: {0}, 长度: {1} 字符", filePath, json.Length));
                
                // 显示配置文件的前500个字符用于调试
                if (json.Length > 0)
                {
                    int previewLength = Math.Min(500, json.Length);
                    Logger.Instance.Debug(string.Format("配置文件前{0}字符: {1}", previewLength, json.Substring(0, previewLength)));
                }
                
                boards = ParseJsonBoards(json);
                
                Logger.Instance.Info(string.Format("板块配置解析完成: ParseJsonBoards返回 {0} 个板块", boards.Count));
                
                // 验证解析结果
                if (boards == null)
                {
                    Logger.Instance.Error("ParseJsonBoards返回null，创建空列表");
                    boards = new List<BoardConfig>();
                }
                
                Logger.Instance.Info(string.Format("板块配置已加载: {0} 个板块", boards.Count));
                // 详细记录每个板块的股票代码数量
                    for (int i = 0; i < boards.Count; i++)
                    {
                        var board = boards[i];
                        if (board != null)
                        {
                        int stockCount = board.StockCodes != null ? board.StockCodes.Count : 0;
                        Logger.Instance.Info(string.Format("  板块[{0}]: Name=[{1}], StockCodes数量={2}", i, board.Name ?? "null", stockCount));
                        if (stockCount > 0)
                        {
                            Logger.Instance.Info(string.Format("    股票代码列表: {0}", string.Join(", ", board.StockCodes.ToArray())));
                        }
                        else
                        {
                            Logger.Instance.Warning(string.Format("    警告: 板块[{0}]没有股票代码！", board.Name ?? "null"));
                        }
                        }
                        else
                        {
                            Logger.Instance.Warning(string.Format("  板块[{0}]: null", i));
                        }
                }
                
                return boards;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("加载板块配置失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                return new List<BoardConfig>();
            }
        }
        
        /// <summary>
        /// 解析JSON格式的板块配置（简单实现，.NET 3.5兼容）
        /// </summary>
        private List<BoardConfig> ParseJsonBoards(string json)
        {
            List<BoardConfig> result = new List<BoardConfig>();
            
            try
            {
                Logger.Instance.Debug(string.Format("开始解析JSON，总长度: {0} 字符", json.Length));
                
                // 简单的JSON解析（适用于我们的简单格式）
                int startIndex = json.IndexOf('[');
                if (startIndex < 0)
                {
                    Logger.Instance.Warning("未找到JSON数组开始标记 '['");
                    return result;
                }
                
                Logger.Instance.Debug(string.Format("找到数组开始位置: {0}", startIndex));
                
                string content = json.Substring(startIndex + 1);
                int braceCount = 0;
                int startBrace = -1;
                int boardCount = 0;
                
                for (int i = 0; i < content.Length; i++)
                {
                    if (content[i] == '{')
                    {
                        if (braceCount == 0)
                        {
                            startBrace = i;
                            Logger.Instance.Debug(string.Format("找到板块开始位置: {0}", i));
                        }
                        braceCount++;
                    }
                    else if (content[i] == '}')
                    {
                        braceCount--;
                        if (braceCount == 0 && startBrace >= 0)
                        {
                            string boardJson = content.Substring(startBrace, i - startBrace + 1);
                            Logger.Instance.Debug(string.Format("提取板块JSON，位置: {0}-{1}, 长度: {2}", startBrace, i, boardJson.Length));
                            
                            // 显示提取的JSON的前100个字符用于调试
                            if (boardJson.Length > 0)
                            {
                                int previewLen = Math.Min(100, boardJson.Length);
                                string preview = boardJson.Substring(0, previewLen);
                                Logger.Instance.Debug(string.Format("提取的板块JSON前{0}字符: {1}", previewLen, preview.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")));
                                
                                // 检查是否包含Name字段
                                if (boardJson.Contains("\"Name\""))
                            {
                                    Logger.Instance.Debug("提取的板块JSON包含\"Name\"字段");
                            }
                            else
                            {
                                    Logger.Instance.Warning("提取的板块JSON不包含\"Name\"字段！");
                                }
                            }
                            
                            BoardConfig board = ParseBoardJson(boardJson);
                            if (board != null)
                            {
                                result.Add(board);
                                boardCount++;
                                Logger.Instance.Debug(string.Format("成功解析板块 #{0}: Name=[{1}], StockCodes={2}", boardCount, board.Name, board.StockCodes != null ? board.StockCodes.Count : 0));
                            }
                            else
                            {
                                Logger.Instance.Warning(string.Format("解析板块失败，位置: {0}-{1}", startBrace, i));
                            }
                            startBrace = -1;
                        }
                    }
                }
                
                Logger.Instance.Info(string.Format("JSON解析完成: 共解析到 {0} 个板块", result.Count));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("解析JSON失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
            
            return result;
        }
        
        /// <summary>
        /// 查找JSON键的位置（允许键名和冒号之间有空格）
        /// </summary>
        private int FindJsonKey(string json, string keyName, int startIndex = 0)
        {
            Logger.Instance.Debug(string.Format("FindJsonKey: 开始搜索键 '{0}'，JSON长度: {1}, 起始位置: {2}", keyName, json.Length, startIndex));
            
            // 显示搜索上下文
            if (json.Length > 0 && startIndex < json.Length)
            {
                int contextStart = Math.Max(0, startIndex);
                int contextLen = Math.Min(100, json.Length - contextStart);
                string context = json.Substring(contextStart, contextLen);
                Logger.Instance.Debug(string.Format("FindJsonKey: 搜索上下文: {0}", context.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")));
            }
            
            // 方法1：直接搜索 "keyName": 模式（最常见的情况，无空格）
            string pattern1 = "\"" + keyName + "\":";
            int pos1 = json.IndexOf(pattern1, startIndex);
            Logger.Instance.Debug(string.Format("FindJsonKey: 方法1搜索模式 '{0}'，结果: {1}", pattern1, pos1));
            if (pos1 >= 0)
            {
                int result = pos1 + pattern1.Length;
                Logger.Instance.Debug(string.Format("FindJsonKey: 找到键 '{0}' (无空格模式) 在位置 {1}，返回 {2}", keyName, pos1, result));
                return result;
            }
            
            // 方法2：搜索 "keyName" 然后跳过空格找冒号
            string pattern2 = "\"" + keyName + "\"";
            int pos2 = json.IndexOf(pattern2, startIndex);
            Logger.Instance.Debug(string.Format("FindJsonKey: 方法2搜索模式 '{0}'，结果: {1}", pattern2, pos2));
            if (pos2 >= 0)
            {
                int afterKey = pos2 + pattern2.Length;
                Logger.Instance.Debug(string.Format("FindJsonKey: 找到键名在位置 {0}，键名后位置: {1}", pos2, afterKey));
                
                // 跳过空白字符
                int whitespaceCount = 0;
                while (afterKey < json.Length && char.IsWhiteSpace(json[afterKey]))
                {
                    afterKey++;
                    whitespaceCount++;
                }
                Logger.Instance.Debug(string.Format("FindJsonKey: 跳过 {0} 个空白字符，当前位置: {1}, 字符: '{2}'", 
                    whitespaceCount, afterKey, (afterKey < json.Length ? json[afterKey].ToString() : "EOF")));
                
                // 检查冒号
                if (afterKey < json.Length && json[afterKey] == ':')
                {
                    int result = afterKey + 1;
                    Logger.Instance.Debug(string.Format("FindJsonKey: 找到键 '{0}' (有空格模式) 在位置 {1}，冒号在 {2}，返回 {3}", keyName, pos2, afterKey, result));
                    return result;
                }
                else
                {
                    Logger.Instance.Warning(string.Format("FindJsonKey: 键 '{0}' 后未找到冒号，当前位置: {1}, 字符: '{2}' (ASCII: {3})", 
                        keyName, afterKey, 
                        (afterKey < json.Length ? json[afterKey].ToString() : "EOF"),
                        (afterKey < json.Length ? ((int)json[afterKey]).ToString() : "N/A")));
                }
            }
            
            // 如果都找不到，尝试逐字符搜索（处理可能的编码问题）
            Logger.Instance.Debug(string.Format("FindJsonKey: 标准搜索未找到键 '{0}'，尝试逐字符搜索", keyName));
            for (int i = startIndex; i <= json.Length - pattern2.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern2.Length; j++)
                {
                    if (i + j >= json.Length || json[i + j] != pattern2[j])
                    {
                        found = false;
                        break;
                    }
                }
                
                if (found)
                {
                    int afterKey = i + pattern2.Length;
                    // 跳过空白字符
                    while (afterKey < json.Length && char.IsWhiteSpace(json[afterKey]))
                    {
                        afterKey++;
                    }
                    // 检查冒号
                    if (afterKey < json.Length && json[afterKey] == ':')
                    {
                        int result = afterKey + 1;
                        Logger.Instance.Debug(string.Format("FindJsonKey: 通过逐字符搜索找到键 '{0}' 在位置 {1}，返回 {2}", keyName, i, result));
                        return result;
                    }
                }
            }
            
            Logger.Instance.Warning(string.Format("FindJsonKey: 未找到键 '{0}'，JSON长度: {1}, 起始位置: {2}", keyName, json.Length, startIndex));
            if (json.Length < 300)
            {
                Logger.Instance.Debug(string.Format("FindJsonKey: 完整JSON内容: {0}", json.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")));
            }
            else
            {
                Logger.Instance.Debug(string.Format("FindJsonKey: JSON前200字符: {0}", json.Substring(0, 200).Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")));
            }
            return -1;
        }
        
        /// <summary>
        /// 解析单个板块的JSON
        /// </summary>
        private BoardConfig ParseBoardJson(string json)
        {
            try
            {
                BoardConfig board = new BoardConfig();
                Logger.Instance.Debug(string.Format("开始解析板块JSON，长度: {0} 字符", json.Length));
                
                // 记录完整的JSON内容用于调试（限制长度避免日志过大）
                if (json.Length < 500)
                {
                    Logger.Instance.Debug(string.Format("板块JSON完整内容: {0}", json));
                }
                else
                {
                    Logger.Instance.Debug(string.Format("板块JSON前500字符: {0}...", json.Substring(0, 500)));
                }
                
                // 提取名称（使用灵活的搜索方法）
                // 先检查JSON中是否真的包含Name字段
                if (!json.Contains("\"Name\""))
                {
                    Logger.Instance.Warning(string.Format("板块JSON不包含\"Name\"字段！JSON长度: {0}", json.Length));
                    if (json.Length < 300)
                    {
                        Logger.Instance.Debug(string.Format("完整JSON内容: {0}", json.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")));
                    }
                }
                
                int nameKeyPos = FindJsonKey(json, "Name");
                Logger.Instance.Debug(string.Format("FindJsonKey(\"Name\") 返回: {0}", nameKeyPos));
                if (nameKeyPos >= 0)
                {
                    // 跳过可能的空格
                    int nameValueStart = nameKeyPos;
                    while (nameValueStart < json.Length && char.IsWhiteSpace(json[nameValueStart]))
                    {
                        nameValueStart++;
                    }
                    
                    // 查找开始引号
                    if (nameValueStart < json.Length && json[nameValueStart] == '"')
                    {
                        nameValueStart++;  // 跳过开始引号
                        int nameEnd = nameValueStart;
                        bool inEscape = false;
                        
                        // 查找结束引号（处理转义字符）
                        while (nameEnd < json.Length)
                        {
                            if (inEscape)
                            {
                                inEscape = false;
                                nameEnd++;
                                continue;
                            }
                            
                            if (json[nameEnd] == '\\')
                            {
                                inEscape = true;
                                nameEnd++;
                                continue;
                            }
                            
                            if (json[nameEnd] == '"')
                            {
                                break;
                            }
                            
                            nameEnd++;
                        }
                        
                        if (nameEnd > nameValueStart && nameEnd < json.Length)
                        {
                            board.Name = UnescapeJson(json.Substring(nameValueStart, nameEnd - nameValueStart));
                            Logger.Instance.Debug(string.Format("解析到板块名称: {0}", board.Name));
                        }
                        else
                        {
                            Logger.Instance.Warning("未找到板块名称的结束引号");
                        }
                    }
                    else
                    {
                        Logger.Instance.Warning("未找到Name字段值的开始引号");
                    }
                }
                else
                {
                    Logger.Instance.Warning("未找到Name字段");
                }
                
                // 提取股票代码列表（使用灵活的搜索方法）
                // 先检查JSON中是否真的包含StockCodes字段
                if (!json.Contains("\"StockCodes\""))
                {
                    Logger.Instance.Warning(string.Format("板块JSON不包含\"StockCodes\"字段！JSON长度: {0}", json.Length));
                }
                
                int codesKeyPos = FindJsonKey(json, "StockCodes");
                Logger.Instance.Debug(string.Format("FindJsonKey(\"StockCodes\") 返回: {0}", codesKeyPos));
                if (codesKeyPos >= 0)
                    {
                    // 跳过可能的空格，查找左方括号
                    int codesStart = codesKeyPos;
                    while (codesStart < json.Length && char.IsWhiteSpace(json[codesStart]))
                        {
                        codesStart++;
                        }
                        
                    if (codesStart < json.Length && json[codesStart] == '[')
                        {
                        codesStart++;  // 跳过左方括号
                        Logger.Instance.Debug(string.Format("板块[{0}]找到StockCodes开始位置: {1}", board.Name ?? "未知", codesStart));
                        
                        // 查找匹配的右方括号（考虑嵌套结构）
                        int bracketCount = 1;
                        int codesEnd = codesStart;
                        bool foundEnd = false;
                        for (int i = codesStart; i < json.Length && bracketCount > 0; i++)
                            {
                                if (json[i] == '[')
                                    bracketCount++;
                                else if (json[i] == ']')
                                {
                                    bracketCount--;
                                    if (bracketCount == 0)
                                    {
                                        codesEnd = i;
                                    foundEnd = true;
                                    Logger.Instance.Debug(string.Format("板块[{0}]找到StockCodes结束位置: {1}", board.Name ?? "未知", codesEnd));
                                        break;
                                    }
                                }
                            }
                            
                        if (!foundEnd)
                        {
                            Logger.Instance.Warning(string.Format("板块[{0}]未找到匹配的右方括号，bracketCount={1}", board.Name ?? "未知", bracketCount));
                        }
                        
                        if (codesEnd > codesStart && foundEnd)
                        {
                            string codesStr = json.Substring(codesStart, codesEnd - codesStart);
                            Logger.Instance.Debug(string.Format("板块[{0}]解析股票代码字符串: [{1}]", board.Name ?? "未知", codesStr));
                            Logger.Instance.Debug(string.Format("板块[{0}]股票代码字符串长度: {1}", board.Name ?? "未知", codesStr.Length));
                                
                            // 显示字符串的每个字符（用于调试，限制前100个字符）
                            if (codesStr.Length > 0 && codesStr.Length <= 100)
                                {
                                StringBuilder charInfo = new StringBuilder();
                                for (int i = 0; i < codesStr.Length; i++)
                                {
                                    char c = codesStr[i];
                                    if (c == '\r')
                                        charInfo.Append("\\r");
                                    else if (c == '\n')
                                        charInfo.Append("\\n");
                                    else if (c == '\t')
                                        charInfo.Append("\\t");
                                    else if (c == ' ')
                                        charInfo.Append("_");  // 用下划线表示空格
                                    else
                                        charInfo.Append(c);
                                }
                                Logger.Instance.Debug(string.Format("板块[{0}]股票代码字符串字符详情: {1}", board.Name ?? "未知", charInfo.ToString()));
                            }
                            
                            // 如果字符串为空或只包含空白字符，说明数组为空
                            if (string.IsNullOrWhiteSpace(codesStr))
                            {
                                Logger.Instance.Info(string.Format("板块[{0}]StockCodes数组为空", board.Name ?? "未知"));
                            }
                            else
                            {
                                // 使用更简单可靠的方法：直接查找引号对
                                int searchIndex = 0;
                                int quotePairCount = 0;
                                while (searchIndex < codesStr.Length)
                                    {
                                    // 查找下一个引号
                                    int quoteStart = codesStr.IndexOf('"', searchIndex);
                                    if (quoteStart < 0)
                                    {
                                        Logger.Instance.Debug(string.Format("板块[{0}]未找到更多引号，搜索位置: {1}, 字符串长度: {2}", board.Name ?? "未知", searchIndex, codesStr.Length));
                                        break;
                                    }
                                    
                                    Logger.Instance.Debug(string.Format("板块[{0}]找到开始引号，位置: {1}, 字符: '{2}'", board.Name ?? "未知", quoteStart, codesStr[quoteStart]));
                                    
                                    // 查找匹配的结束引号（跳过转义字符）
                                    int quoteEnd = quoteStart + 1;
                                    bool foundEndQuote = false;
                                    bool inEscape = false;
                                    
                                    while (quoteEnd < codesStr.Length)
                                    {
                                        char c = codesStr[quoteEnd];
                                        
                                        if (inEscape)
                                        {
                                            inEscape = false;
                                            quoteEnd++;
                                            continue;
                                        }
                                        
                                        if (c == '\\')
                                        {
                                            inEscape = true;
                                            quoteEnd++;
                                            continue;
                                        }
                                        
                                        if (c == '"')
                                        {
                                            foundEndQuote = true;
                                            Logger.Instance.Debug(string.Format("板块[{0}]找到结束引号，位置: {1}", board.Name ?? "未知", quoteEnd));
                                            break;
                                        }
                                        
                                        quoteEnd++;
                                    }
                                    
                                    if (foundEndQuote)
                                        {
                                        quotePairCount++;
                                        // 提取引号内的内容
                                        string code = codesStr.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                                        Logger.Instance.Debug(string.Format("板块[{0}]提取到原始代码: [{1}], 长度: {2}", board.Name ?? "未知", code, code.Length));
                                        
                                        if (!string.IsNullOrEmpty(code))
                                        {
                                            // 先Trim，再处理转义字符
                                            string trimmedCode = code.Trim();
                                            string cleanCode = UnescapeJson(trimmedCode);
                                        
                                            Logger.Instance.Debug(string.Format("板块[{0}]清理后代码: [{1}], 长度: {2}", board.Name ?? "未知", cleanCode, cleanCode.Length));
                                        
                                        if (!string.IsNullOrEmpty(cleanCode))
                                        {
                                                board.StockCodes.Add(cleanCode);
                                                Logger.Instance.Info(string.Format("板块[{0}]成功解析到股票代码: [{1}]", board.Name ?? "未知", cleanCode));
                                            }
                                            else
                                            {
                                                Logger.Instance.Warning(string.Format("板块[{0}]清理后代码为空，原始代码: [{1}]", board.Name ?? "未知", code));
                                            }
                                        }
                                        else
                                        {
                                            Logger.Instance.Warning(string.Format("板块[{0}]提取的代码为空", board.Name ?? "未知"));
                                        }
                                        
                                        // 继续查找下一个引号
                                        searchIndex = quoteEnd + 1;
                                }
                                else
                                {
                                        // 未找到匹配的结束引号，退出
                                        Logger.Instance.Warning(string.Format("板块[{0}]未找到匹配的结束引号，开始引号位置: {1}, 字符串长度: {2}", board.Name ?? "未知", quoteStart, codesStr.Length));
                                        break;
                                    }
                                }
                                
                                Logger.Instance.Debug(string.Format("板块[{0}]引号匹配完成，共找到 {1} 对引号，解析到 {2} 个股票代码", board.Name ?? "未知", quotePairCount, board.StockCodes.Count));
                                
                                Logger.Instance.Info(string.Format("板块[{0}]解析完成: 共 {1} 个股票代码", board.Name ?? "未知", board.StockCodes.Count));
                                if (board.StockCodes.Count > 0)
                                {
                                    Logger.Instance.Info(string.Format("板块[{0}]股票代码列表: {1}", board.Name ?? "未知", string.Join(", ", board.StockCodes.ToArray())));
                                }
                                }
                            }
                            else
                            {
                            if (!foundEnd)
                            {
                                Logger.Instance.Warning(string.Format("板块[{0}]未找到匹配的右方括号", board.Name ?? "未知"));
                        }
                        else
                        {
                                Logger.Instance.Warning(string.Format("板块[{0}]StockCodes数组范围无效: codesStart={1}, codesEnd={2}", board.Name ?? "未知", codesStart, codesEnd));
                        }
                    }
                    }
                }
                else
                {
                    Logger.Instance.Warning(string.Format("板块[{0}]未找到StockCodes字段", board.Name ?? "未知"));
                }
                
                // 提取股票名称映射（使用灵活的搜索方法）
                int namesKeyPos = FindJsonKey(json, "StockNames");
                if (namesKeyPos >= 0)
                {
                    // 跳过可能的空格，查找左大括号
                    int namesStart = namesKeyPos;
                    while (namesStart < json.Length && char.IsWhiteSpace(json[namesStart]))
                        {
                        namesStart++;
                        }
                        
                    if (namesStart < json.Length && json[namesStart] == '{')
                        {
                        namesStart++;  // 跳过左大括号
                        // 查找匹配的右大括号（考虑嵌套结构）
                        int braceCount = 1;
                        int namesEnd = namesStart;
                        bool foundNamesEnd = false;
                        for (int i = namesStart; i < json.Length && braceCount > 0; i++)
                            {
                                if (json[i] == '{')
                                    braceCount++;
                                else if (json[i] == '}')
                                {
                                    braceCount--;
                                    if (braceCount == 0)
                                    {
                                    namesEnd = i;
                                    foundNamesEnd = true;
                                        break;
                                    }
                                }
                            }
                            
                        if (foundNamesEnd && namesEnd > namesStart)
                            {
                                string namesStr = json.Substring(namesStart, namesEnd - namesStart);
                                    // 解析键值对：\"code\":\"name\"
                                    string[] pairs = namesStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    
                                    foreach (string pair in pairs)
                                    {
                                int colonIndex = pair.IndexOf(':');
                                if (colonIndex > 0)
                                        {
                                    string code = pair.Substring(0, colonIndex).Trim().Trim('"', ' ', '\t', '\r', '\n');
                                    string name = pair.Substring(colonIndex + 1).Trim().Trim('"', ' ', '\t', '\r', '\n');
                                            if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(name))
                                            {
                                                board.StockNames[UnescapeJson(code)] = UnescapeJson(name);
                                            }
                                        }
                                    }
                                }
                    }
                }
                
                // 提取宽度（使用灵活的搜索方法）
                int widthKeyPos = FindJsonKey(json, "Width");
                if (widthKeyPos >= 0)
                                {
                    // 跳过可能的空格
                    int widthStart = widthKeyPos;
                    while (widthStart < json.Length && char.IsWhiteSpace(json[widthStart]))
                    {
                        widthStart++;
                    }
                    
                    int widthEnd = widthStart;
                    while (widthEnd < json.Length && json[widthEnd] != ',' && json[widthEnd] != '}')
                    {
                        widthEnd++;
                                }
                    
                    if (widthEnd > widthStart)
                            {
                        string widthStr = json.Substring(widthStart, widthEnd - widthStart).Trim();
                        int width;
                        if (int.TryParse(widthStr, out width))
                        {
                            board.Width = width;
                            }
                        }
                }
                
                // 提取高度（使用灵活的搜索方法）
                int heightKeyPos = FindJsonKey(json, "Height");
                if (heightKeyPos >= 0)
                {
                    // 跳过可能的空格
                    int heightStart = heightKeyPos;
                    while (heightStart < json.Length && char.IsWhiteSpace(json[heightStart]))
                    {
                        heightStart++;
                    }
                    
                    int heightEnd = heightStart;
                    while (heightEnd < json.Length && json[heightEnd] != ',' && json[heightEnd] != '}')
                    {
                        heightEnd++;
                    }
                    
                    if (heightEnd > heightStart)
                    {
                        string heightStr = json.Substring(heightStart, heightEnd - heightStart).Trim();
                        int height;
                        if (int.TryParse(heightStr, out height))
                        {
                            board.Height = height;
                }
                    }
                }

                return board;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("解析板块JSON失败: {0}", ex.Message));
                return null;
            }
        }

        private string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str))
                return "";
                
            return str.Replace("\\", "\\\\")
                     .Replace("\"", "\\\"")
                     .Replace("\n", "\\n")
                     .Replace("\r", "\\r")
                     .Replace("\t", "\\t");
        }
        
        private string UnescapeJson(string str)
        {
            if (string.IsNullOrEmpty(str))
                return "";
                
            return str.Replace("\\\"", "\"")
                     .Replace("\\\\", "\\")
                     .Replace("\\n", "\n")
                     .Replace("\\r", "\r")
                     .Replace("\\t", "\t");
        }
    }
    
    /// <summary>
    /// 板块配置
    /// </summary>
    public class BoardConfig
    {
        private string name;
        private List<string> stockCodes;
        private Dictionary<string, string> stockNames;
        private int width;
        private int height;

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public List<string> StockCodes
        {
            get { return stockCodes; }
            set { stockCodes = value; }
        }

        public Dictionary<string, string> StockNames
        {
            get { return stockNames; }
            set { stockNames = value; }
        }

        public int Width
        {
            get { return width; }
            set { width = value; }
        }

        public int Height
        {
            get { return height; }
            set { height = value; }
        }
        
        public BoardConfig()
        {
            Name = "板块1";
            StockCodes = new List<string>();
            StockNames = new Dictionary<string, string>();  // 初始化股票名称字典
            Width = 0;  // 0表示使用默认大小
            Height = 0;  // 0表示使用默认大小
        }
        
        public BoardConfig(string name) : this()
        {
            Name = name;
        }
    }
}

