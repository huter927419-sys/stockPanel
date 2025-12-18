using System;
using System.Collections.Generic;
using System.Text;

namespace HaiLiDrvDemo
{
    /// <summary>
    /// 拼音首字母转换工具（轻量级实现，支持常用汉字）
    /// </summary>
    public static class PinyinHelper
    {
        /// <summary>
        /// 获取汉字字符串的拼音首字母
        /// </summary>
        public static string GetInitials(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            StringBuilder result = new StringBuilder();
            
            foreach (char c in text)
            {
                if (IsChinese(c))
                {
                    string initial = GetCharInitial(c);
                    if (!string.IsNullOrEmpty(initial))
                    {
                        result.Append(initial);
                    }
                }
                else if (char.IsLetterOrDigit(c))
                {
                    // 保留字母和数字
                    result.Append(char.ToUpper(c));
                }
            }
            
            return result.ToString();
        }
        
        /// <summary>
        /// 判断是否为汉字
        /// </summary>
        private static bool IsChinese(char c)
        {
            return c >= 0x4e00 && c <= 0x9fff;
        }
        
        /// <summary>
        /// 获取单个汉字的拼音首字母（基于Unicode范围判断）
        /// </summary>
        private static string GetCharInitial(char c)
        {
            int code = (int)c;
            
            // 使用Unicode范围判断拼音首字母（简化版，基于常用汉字分布）
            // 这是一个近似方法，对于精确度要求高的场景，建议使用专业拼音库
            
            // A: 啊(0x554a) - 按(0x6309) 等
            if ((code >= 0x554a && code <= 0x55b5) || // 啊-啊
                (code >= 0x5b89 && code <= 0x5b9a) || // 安-按
                (code >= 0x6697 && code <= 0x66f2))   // 暗-昂
                return "A";
            
            // B: 把(0x628a) - 不(0x4e0d) 等
            if ((code >= 0x62e5 && code <= 0x62f1) || // 把-把
                (code >= 0x767d && code <= 0x767e) || // 白-百
                (code >= 0x73ed && code <= 0x73ed) || // 班
                (code >= 0x5343 && code <= 0x5343) || // 半
                (code >= 0x5305 && code <= 0x5305) || // 包
                (code >= 0x4fdd && code <= 0x4fdd) || // 保
                (code >= 0x62b1 && code <= 0x62b1) || // 报
                (code >= 0x5317 && code <= 0x5317) || // 北
                (code >= 0x8d1d && code <= 0x8d1d) || // 贝
                (code >= 0x88c5 && code <= 0x88c5) || // 被
                (code >= 0x672c && code <= 0x672c) || // 本
                (code >= 0x6bd4 && code <= 0x6bd4) || // 比
                (code >= 0x5fc3 && code <= 0x5fc3) || // 必
                (code >= 0x95ed && code <= 0x95ed) || // 闭
                (code >= 0x58f9 && code <= 0x58f9) || // 币
                (code >= 0x4e0d && code <= 0x4e0d))   // 不
                return "B";
            
            // C: 擦(0x64e6) - 错(0x9519) 等
            if ((code >= 0x64e6 && code <= 0x64e6) || // 擦
                (code >= 0x91c7 && code <= 0x91c7) || // 采
                (code >= 0x8d22 && code <= 0x8d22) || // 财
                (code >= 0x53c2 && code <= 0x53c2) || // 参
                (code >= 0x83dc && code <= 0x83dc) || // 菜
                (code >= 0x83b1 && code <= 0x83b1) || // 莱
                (code >= 0x83f2 && code <= 0x83f2) || // 菲
                (code >= 0x83f1 && code <= 0x83f1) || // 菲
                (code >= 0x83f0 && code <= 0x83f0) || // 菲
                (code >= 0x83ef && code <= 0x83ef) || // 华
                (code >= 0x83ee && code <= 0x83ee) || // 华
                (code >= 0x83ed && code <= 0x83ed) || // 华
                (code >= 0x83ec && code <= 0x83ec) || // 华
                (code >= 0x83eb && code <= 0x83eb) || // 华
                (code >= 0x83ea && code <= 0x83ea) || // 华
                (code >= 0x83e9 && code <= 0x83e9) || // 华
                (code >= 0x83e8 && code <= 0x83e8) || // 华
                (code >= 0x83e7 && code <= 0x83e7) || // 华
                (code >= 0x83e6 && code <= 0x83e6) || // 华
                (code >= 0x83e5 && code <= 0x83e5) || // 华
                (code >= 0x83e4 && code <= 0x83e4) || // 华
                (code >= 0x83e3 && code <= 0x83e3) || // 华
                (code >= 0x83e2 && code <= 0x83e2) || // 华
                (code >= 0x83e1 && code <= 0x83e1) || // 华
                (code >= 0x83e0 && code <= 0x83e0) || // 华
                (code >= 0x83df && code <= 0x83df) || // 华
                (code >= 0x83de && code <= 0x83de) || // 华
                (code >= 0x83dd && code <= 0x83dd) || // 华
                (code >= 0x83dc && code <= 0x83dc) || // 菜
                (code >= 0x9519 && code <= 0x9519))   // 错
                return "C";
            
            // 使用更简单的方法：基于常用股票名称汉字建立映射表
            return GetInitialByCommonStockChars(c);
        }
        
        /// <summary>
        /// 基于常用股票名称汉字获取拼音首字母（针对股票名称优化）
        /// </summary>
        private static string GetInitialByCommonStockChars(char c)
        {
            // 常用股票名称汉字拼音首字母映射表（部分常用字）
            Dictionary<char, string> commonChars = new Dictionary<char, string>
            {
                // A
                {'安', "A"}, {'奥', "A"},
                // B
                {'北', "B"}, {'百', "B"}, {'保', "B"}, {'本', "B"}, {'比', "B"}, {'不', "B"}, {'报', "B"}, {'被', "B"}, {'币', "B"}, {'闭', "B"},
                // C
                {'财', "C"}, {'参', "C"}, {'菜', "C"}, {'错', "C"}, {'从', "C"}, {'成', "C"}, {'城', "C"}, {'创', "C"}, {'出', "C"}, {'初', "C"},
                // D
                {'大', "D"}, {'多', "D"}, {'东', "D"}, {'电', "D"}, {'地', "D"}, {'当', "D"}, {'对', "D"}, {'第', "D"}, {'的', "D"}, {'等', "D"},
                // E
                {'额', "E"}, {'而', "E"}, {'二', "E"},
                // F
                {'发', "F"}, {'复', "F"}, {'方', "F"}, {'分', "F"}, {'风', "F"}, {'服', "F"}, {'非', "F"}, {'飞', "F"}, {'富', "F"}, {'福', "F"},
                // G
                {'该', "G"}, {'过', "G"}, {'国', "G"}, {'工', "G"}, {'公', "G"}, {'股', "G"}, {'高', "G"}, {'光', "G"}, {'广', "G"}, {'贵', "G"},
                // H
                {'还', "H"}, {'或', "H"}, {'和', "H"}, {'华', "H"}, {'海', "H"}, {'航', "H"}, {'化', "H"}, {'环', "H"}, {'汇', "H"}, {'恒', "H"},
                // J
                {'及', "J"}, {'就', "J"}, {'金', "J"}, {'建', "J"}, {'交', "J"}, {'集', "J"}, {'机', "J"}, {'技', "J"}, {'基', "J"}, {'际', "J"},
                // K
                {'开', "K"}, {'快', "K"}, {'可', "K"}, {'科', "K"}, {'控', "K"}, {'矿', "K"}, {'空', "K"}, {'口', "K"},
                // L
                {'来', "L"}, {'落', "L"}, {'联', "L"}, {'利', "L"}, {'力', "L"}, {'龙', "L"}, {'路', "L"}, {'绿', "L"}, {'蓝', "L"}, {'乐', "L"},
                // M
                {'吗', "M"}, {'没', "M"}, {'美', "M"}, {'民', "M"}, {'明', "M"}, {'名', "M"}, {'模', "M"}, {'摩', "M"}, {'马', "M"}, {'买', "M"},
                // N
                {'那', "N"}, {'年', "N"}, {'能', "N"}, {'内', "N"}, {'南', "N"}, {'农', "N"}, {'宁', "N"}, {'你', "N"}, {'牛', "N"}, {'女', "N"},
                // O
                {'哦', "O"}, {'欧', "O"},
                // P
                {'怕', "P"}, {'平', "P"}, {'浦', "P"}, {'普', "P"}, {'配', "P"}, {'盘', "P"}, {'品', "P"}, {'票', "P"}, {'片', "P"}, {'批', "P"},
                // Q
                {'起', "Q"}, {'去', "Q"}, {'企', "Q"}, {'汽', "Q"}, {'轻', "Q"}, {'清', "Q"}, {'全', "Q"}, {'前', "Q"}, {'强', "Q"}, {'桥', "Q"},
                // R
                {'然', "R"}, {'如', "R"}, {'人', "R"}, {'日', "R"}, {'融', "R"}, {'软', "R"}, {'热', "R"}, {'润', "R"}, {'瑞', "R"}, {'荣', "R"},
                // S
                {'三', "S"}, {'所', "S"}, {'上', "S"}, {'深', "S"}, {'生', "S"}, {'实', "S"}, {'市', "S"}, {'时', "S"}, {'首', "S"}, {'收', "S"},
                // T
                {'他', "T"}, {'同', "T"}, {'天', "T"}, {'通', "T"}, {'投', "T"}, {'太', "T"}, {'特', "T"}, {'台', "T"}, {'铁', "T"}, {'体', "T"},
                // W
                {'外', "W"}, {'我', "W"}, {'万', "W"}, {'网', "W"}, {'物', "W"}, {'文', "W"}, {'五', "W"}, {'武', "W"}, {'微', "W"}, {'卫', "W"},
                // X
                {'下', "X"}, {'新', "X"}, {'信', "X"}, {'行', "X"}, {'小', "X"}, {'先', "X"}, {'现', "X"}, {'线', "X"}, {'系', "X"}, {'西', "X"},
                // Y
                {'也', "Y"}, {'有', "Y"}, {'一', "Y"}, {'银', "Y"}, {'业', "Y"}, {'医', "Y"}, {'药', "Y"}, {'用', "Y"}, {'运', "Y"}, {'元', "Y"},
                // Z
                {'在', "Z"}, {'最', "Z"}, {'中', "Z"}, {'招', "Z"}, {'证', "Z"}, {'正', "Z"}, {'智', "Z"}, {'制', "Z"}, {'重', "Z"}, {'众', "Z"}
            };
            
            if (commonChars.ContainsKey(c))
            {
                return commonChars[c];
            }
            
            // 如果不在常用字表中，使用Unicode范围近似判断（简化处理）
            // 对于精确度要求高的场景，建议使用专业拼音库如NPinyin
            return "";
        }
    }
}

