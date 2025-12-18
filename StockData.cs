using System;
using System.Collections.Generic;

namespace HaiLiDrvDemo
{
    /// <summary>
    /// 股票数据模型
    /// </summary>
    public class StockData
    {
        // 基本信息
        private string _code;
        public string Code
        {
            get { return _code; }
            set { _code = value; }
        }
        private string _name;
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }
        private UInt16 _marketType;
        public UInt16 MarketType
        {
            get { return _marketType; }
            set { _marketType = value; }
        }
        private int _tradeTime;
        public int TradeTime
        {
            get { return _tradeTime; }
            set { _tradeTime = value; }
        }

        // 价格数据
        private float _lastClose;
        public float LastClose
        {
            get { return _lastClose; }
            set { _lastClose = value; }
        }
        private float _open;
        public float Open
        {
            get { return _open; }
            set { _open = value; }
        }
        private float _high;
        public float High
        {
            get { return _high; }
            set { _high = value; }
        }
        private float _low;
        public float Low
        {
            get { return _low; }
            set { _low = value; }
        }
        private float _newPrice;
        public float NewPrice
        {
            get { return _newPrice; }
            set { _newPrice = value; }
        }

        // 成交数据
        private float _volume;
        public float Volume
        {
            get { return _volume; }
            set { _volume = value; }
        }
        private float _amount;
        public float Amount
        {
            get { return _amount; }
            set { _amount = value; }
        }

        // 计算数据
        private float _changePercent;
        public float ChangePercent
        {
            get { return _changePercent; }
            set { _changePercent = value; }
        }
        private float _changeAmount;
        public float ChangeAmount
        {
            get { return _changeAmount; }
            set { _changeAmount = value; }
        }

        // 时间戳
        private DateTime _updateTime;
        public DateTime UpdateTime
        {
            get { return _updateTime; }
            set { _updateTime = value; }
        }

        /// <summary>
        /// 计算涨跌幅
        /// </summary>
        public void CalculateChangePercent()
        {
            if (LastClose > 0)
            {
                ChangePercent = ((NewPrice - LastClose) / LastClose) * 100;
            }
            else
            {
                ChangePercent = 0;
            }
        }

        /// <summary>
        /// 计算涨跌额
        /// </summary>
        public void CalculateChangeAmount()
        {
            ChangeAmount = NewPrice - LastClose;
        }

        /// <summary>
        /// 检查是否满足显示条件：涨幅>3% 且 现价>开盘5%
        /// </summary>
        public bool ShouldDisplay()
        {
            bool condition1 = ChangePercent > 3.0f;  // 涨幅大于3%
            bool condition2 = Open > 0 && NewPrice > Open * 1.05f;  // 现价大于开盘5%
            return condition1 && condition2;
        }
    }

    /// <summary>
    /// 股票板块
    /// </summary>
    public class StockBoard
    {
        private string _boardName;
        public string BoardName
        {
            get { return _boardName; }
            set { _boardName = value; }
        }
        private List<string> _stockCodes;
        public List<string> StockCodes
        {
            get { return _stockCodes; }
            set { _stockCodes = value; }
        }
        private HashSet<string> _stockCodesSet;
        public HashSet<string> StockCodesSet
        {
            get { return _stockCodesSet; }
            set { _stockCodesSet = value; }
        }
        private Dictionary<string, StockData> _stockDataDict;
        public Dictionary<string, StockData> StockDataDict
        {
            get { return _stockDataDict; }
            set { _stockDataDict = value; }
        }

        public StockBoard()
        {
            StockCodes = new List<string>();
            StockCodesSet = new HashSet<string>();
            StockDataDict = new Dictionary<string, StockData>();
        }

        public StockBoard(string name) : this()
        {
            BoardName = name;
        }

        /// <summary>
        /// 添加股票代码
        /// </summary>
        public void AddStock(string code)
        {
            if (!StockCodesSet.Contains(code))
            {
                StockCodes.Add(code);
                StockCodesSet.Add(code);
            }
        }

        /// <summary>
        /// 删除股票代码
        /// </summary>
        public void RemoveStock(string code)
        {
            StockCodes.Remove(code);
            StockCodesSet.Remove(code);
            StockDataDict.Remove(code);
        }

        /// <summary>
        /// 更新股票数据（只更新板块中已添加的股票）
        /// </summary>
        public void UpdateStockData(StockData data)
        {
            // 只有当板块中包含该股票代码时，才更新数据
            if (StockCodes.Contains(data.Code))
            {
                // 如果字典中已有该股票，更新数据；否则创建新条目
                if (StockDataDict.ContainsKey(data.Code))
                {
                    // 更新现有数据，保留股票代码和名称
                    StockData existingData = StockDataDict[data.Code];
                    existingData.LastClose = data.LastClose;
                    existingData.Open = data.Open;
                    existingData.High = data.High;
                    existingData.Low = data.Low;
                    existingData.NewPrice = data.NewPrice;
                    existingData.Volume = data.Volume;
                    existingData.Amount = data.Amount;
                    existingData.UpdateTime = data.UpdateTime;
                    existingData.CalculateChangePercent();
                }
                else
                {
                    // 创建新数据对象（深拷贝）
                    StockData newData = new StockData
                    {
                        Code = data.Code,
                        Name = data.Name,
                        LastClose = data.LastClose,
                        Open = data.Open,
                        High = data.High,
                        Low = data.Low,
                        NewPrice = data.NewPrice,
                        Volume = data.Volume,
                        Amount = data.Amount,
                        UpdateTime = data.UpdateTime
                    };
                    newData.CalculateChangePercent();
                    StockDataDict[data.Code] = newData;
                }
            }
        }

        /// <summary>
        /// 获取满足条件的股票列表（按涨幅排序）
        /// </summary>
        public List<StockData> GetFilteredStocks()
        {
            List<StockData> result = new List<StockData>();
            foreach (var stock in StockDataDict.Values)
            {
                if (stock.ShouldDisplay())
                {
                    result.Add(stock);
                }
            }
            // 按涨幅降序排序
            result.Sort((x, y) => y.ChangePercent.CompareTo(x.ChangePercent));
            return result;
        }

        /// <summary>
        /// 清除所有数据（开盘前调用）
        /// </summary>
        public void ClearData()
        {
            StockDataDict.Clear();
        }
    }
}

