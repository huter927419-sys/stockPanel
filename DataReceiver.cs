using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HaiLiDrvDemo
{
    /// <summary>
    /// 数据接收器 - 负责从DLL接收原始数据
    /// </summary>
    public class DataReceiver
    {
        /// <summary>
        /// 实时行情数据接收事件
        /// </summary>
        public event EventHandler<StockDataEventArgs> StockDataReceived;

        /// <summary>
        /// 码表数据接收事件
        /// </summary>
        public event EventHandler<MarketTableEventArgs> MarketTableReceived;

        // F10功能已禁用
        // /// <summary>
        // /// F10数据接收事件
        // /// </summary>
        // public event EventHandler<StockBaseDataEventArgs> StockBaseDataReceived;

        /// <summary>
        /// 数据接收统计
        /// </summary>
        public class DataStatistics
        {
            private int _totalPacketsReceived;
            public int TotalPacketsReceived
            {
                get { return _totalPacketsReceived; }
                set { _totalPacketsReceived = value; }
            }
            private int _totalStocksProcessed;
            public int TotalStocksProcessed
            {
                get { return _totalStocksProcessed; }
                set { _totalStocksProcessed = value; }
            }
            private DateTime _lastUpdateTime;
            public DateTime LastUpdateTime
            {
                get { return _lastUpdateTime; }
                set { _lastUpdateTime = value; }
            }
            private int _errorCount;
            public int ErrorCount
            {
                get { return _errorCount; }
                set { _errorCount = value; }
            }
            private int _marketTableCount;
            public int MarketTableCount
            {
                get { return _marketTableCount; }
                set { _marketTableCount = value; }
            }
            private int _totalMarketTableStocks;
            public int TotalMarketTableStocks
            {
                get { return _totalMarketTableStocks; }
                set { _totalMarketTableStocks = value; }
            }
        }

        private DataStatistics statistics = new DataStatistics();

        public DataStatistics Statistics
        {
            get { return statistics; }
        }

        // 上次数据接收时间（用于检测是否停止接收）
        private DateTime lastDataReceiveTime = DateTime.MinValue;

        /// <summary>
        /// 处理实时行情数据（优化性能版本）
        /// </summary>
        public void ProcessRealTimeData(IntPtr lParam)
        {
            try
            {
                HaiLiDrvDemo.RCV_DATA pHeader = (HaiLiDrvDemo.RCV_DATA)Marshal.PtrToStructure(
                    lParam, 
                    typeof(HaiLiDrvDemo.RCV_DATA));

                // 验证m_pData指针是否有效（仅用于调试）
                if (pHeader.m_pData == IntPtr.Zero)
                {
                    Logger.Instance.Error(string.Format("ProcessRealTimeData: m_pData指针为空！lParam={0}, m_nPacketNum={1}", lParam.ToInt64(), pHeader.m_nPacketNum));
                    return;
                }
                
                // 前10条数据包详细记录，之后每100条记录一次
                bool isDetailedLog = statistics.TotalPacketsReceived < 10;
                
                if (isDetailedLog)
                {
                    Logger.Instance.Info(string.Format("ProcessRealTimeData: lParam={0}, m_pData={1}, m_nPacketNum={2}", lParam.ToInt64(), pHeader.m_pData.ToInt64(), pHeader.m_nPacketNum));
                    
                    // 验证第一条数据
                    if (pHeader.m_nPacketNum > 0 && pHeader.m_pData != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr firstRecordPtr = new IntPtr(pHeader.m_pData.ToInt64());
                            RCV_REPORT_STRUCTExV3 firstRecord = (RCV_REPORT_STRUCTExV3)Marshal.PtrToStructure(
                                firstRecordPtr, typeof(RCV_REPORT_STRUCTExV3));
                            string testCode = new string(firstRecord.m_szLabel).Trim('\0');
                            float testPrice = firstRecord.m_fNewPrice;
                            Logger.Instance.Info(string.Format("第一条数据验证: 代码={0}, 价格={1}, m_pData偏移={2}", testCode, testPrice, pHeader.m_pData.ToInt64() - lParam.ToInt64()));
                        }
                        catch (Exception verifyEx)
                        {
                            Logger.Instance.Warning(string.Format("验证第一条数据失败: {0}", verifyEx.Message));
                        }
                    }
                }
                
                // 每100条记录一次详细信息（用于调试）
                if (statistics.TotalPacketsReceived % 100 == 0 && pHeader.m_nPacketNum > 0)
                {
                    Logger.Instance.Debug(string.Format("ProcessRealTimeData: lParam={0}, m_pData={1}, m_nPacketNum={2}", lParam.ToInt64(), pHeader.m_pData.ToInt64(), pHeader.m_nPacketNum));
                }

                int previousCount = statistics.TotalPacketsReceived;
                statistics.TotalPacketsReceived += pHeader.m_nPacketNum;
                statistics.LastUpdateTime = DateTime.Now;
                lastDataReceiveTime = DateTime.Now;
                
                // 记录每次数据包接收的详细信息（用于调试）- 只在特定情况下记录，避免日志过多
                // 每1000条记录一次，避免日志队列积压
                if (statistics.TotalPacketsReceived % 1000 == 0)
                {
                    Logger.Instance.Debug(string.Format("收到数据包: 包含 {0} 条记录, 累计 {1} 条", pHeader.m_nPacketNum, statistics.TotalPacketsReceived));
                }
                
                // 批量收集数据，减少事件触发频率
                List<StockData> batchData = new List<StockData>(pHeader.m_nPacketNum);
                
                // 处理每个数据包
                for (int i = 0; i < pHeader.m_nPacketNum; i++)
                {
                    try
                    {
                        // 解析单条实时行情数据
                        // 注意：使用IntPtr.ToInt64()而不是(int)转换，以支持64位系统
                        IntPtr recordPtr = new IntPtr(pHeader.m_pData.ToInt64() + 158 * i);
                        
                        HaiLiDrvDemo.RCV_REPORT_STRUCTExV3 report = 
                            (HaiLiDrvDemo.RCV_REPORT_STRUCTExV3)Marshal.PtrToStructure(
                                recordPtr, 
                                typeof(HaiLiDrvDemo.RCV_REPORT_STRUCTExV3));

                        // 转换为业务对象
                        StockData stockData = DataConverter.ConvertFromReport(report);
                        
                        if (stockData != null)
                        {
                            statistics.TotalStocksProcessed++;
                            batchData.Add(stockData);
                        }
                        else
                        {
                            // 如果转换失败，记录警告（仅前几条，避免日志过多）
                            if (i < 3)
                            {
                                Logger.Instance.Warning(string.Format("股票数据转换失败: 索引={0}, m_pData={1}, recordPtr={2}", i, pHeader.m_pData.ToInt64(), recordPtr.ToInt64()));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        statistics.ErrorCount++;
                        // 前几条错误记录详细信息，后续只记录计数
                        if (i < 3)
                        {
                            Logger.Instance.Error(string.Format("数据包解析失败 [{0}]: {1}", i, ex.Message));
                            Logger.Instance.Error(string.Format("m_pData={0}, 记录大小=158, 索引={1}", pHeader.m_pData.ToInt64(), i));
                        }
                        // 错误只记录到Debug，不输出到UI日志，避免性能问题
                        System.Diagnostics.Debug.WriteLine(string.Format("数据包解析失败 [{0}]: {1}", i, ex.Message));
                    }
                }
                
                // 批量触发事件（减少事件触发频率，提高性能）
                if (batchData.Count > 0)
                {
                    // 使用之前声明的isDetailedLog变量
                    if (isDetailedLog)
                    {
                        Logger.Instance.Info(string.Format("ProcessRealTimeData: 处理了 {0}/{1} 条实时数据，累计 {2} 条", batchData.Count, pHeader.m_nPacketNum, statistics.TotalStocksProcessed));
                        if (batchData.Count > 0)
                        {
                            var firstStock = batchData[0];
                            Logger.Instance.Info(string.Format("示例数据: 股票={0}, 名称={1}, 价格={2}, 成交量={3}", firstStock.Code, firstStock.Name, firstStock.NewPrice, firstStock.Volume));
                        }
                    }
                    else if (statistics.TotalStocksProcessed % 100 == 0)
                    {
                        Logger.Instance.Debug(string.Format("ProcessRealTimeData: 处理了 {0} 条实时数据，累计 {1} 条", batchData.Count, statistics.TotalStocksProcessed));
                    }
                    
                    if (StockDataReceived != null)
                    {
                        foreach (var stockData in batchData)
                        {
                            StockDataReceived(this, new StockDataEventArgs(stockData));
                        }
                        
                        if (isDetailedLog)
                        {
                            Logger.Instance.Info(string.Format("已触发 {0} 次StockDataReceived事件", batchData.Count));
                        }
                    }
                    else
                    {
                        Logger.Instance.Warning("StockDataReceived事件未订阅！数据无法传递到UI");
                    }
                }
                else
                {
                    // 如果批量数据为空，记录警告（仅前几次）
                    if (statistics.TotalPacketsReceived <= 10)
                    {
                        Logger.Instance.Warning(string.Format("ProcessRealTimeData: 数据包包含 {0} 条记录，但转换后数据为空！", pHeader.m_nPacketNum));
                        Logger.Instance.Warning(string.Format("m_pData={0}, lParam={1}", pHeader.m_pData.ToInt64(), lParam.ToInt64()));
                    }
                }
                
                // 记录数据接收进度（更频繁地显示，每100条记录一次）
                int currentCount = statistics.TotalPacketsReceived;
                
                // 检查是否跨越了100的倍数（更频繁的日志）
                int previousHundred = previousCount / 100;
                int currentHundred = currentCount / 100;
                
                if (currentHundred > previousHundred)
                {
                    Logger.Instance.Info(string.Format("已接收 {0} 条数据包，已处理 {1} 只股票", currentCount, statistics.TotalStocksProcessed));
                }
                
                // 每1000条记录一次成功日志
                int previousThousand = previousCount / 1000;
                int currentThousand = currentCount / 1000;
                
                if (currentThousand > previousThousand)
                {
                    Logger.Instance.Success(string.Format("数据接收进度: {0} 条数据包，{1} 只股票", currentCount, statistics.TotalStocksProcessed));
                }
            }
            catch (Exception ex)
            {
                statistics.ErrorCount++;
                // 记录所有错误，不要吞掉异常
                Logger.Instance.Error(string.Format("实时数据处理失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                
                // 每100次错误记录一次汇总
                if (statistics.ErrorCount % 100 == 0)
                {
                    Logger.Instance.Error(string.Format("实时数据处理错误累计: {0} 次", statistics.ErrorCount));
                }
                System.Diagnostics.Debug.WriteLine(string.Format("实时数据处理失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 处理分钟数据（分时线数据）
        /// </summary>
        public void ProcessMinuteData(IntPtr lParam)
        {
            try
            {
                HaiLiDrvDemo.RCV_DATA pHeader = (HaiLiDrvDemo.RCV_DATA)Marshal.PtrToStructure(
                    lParam, 
                    typeof(HaiLiDrvDemo.RCV_DATA));

                if (pHeader.m_wDataType != StockDrv.FILE_MINUTE_EX)
                    return;
                
                Logger.Instance.Info(string.Format("收到分钟数据包，记录数: {0}", pHeader.m_nPacketNum));

                // 计算结构大小
                int minuteStructSize = Marshal.SizeOf(typeof(HaiLiDrvDemo.RCV_MINUTE_STRUCTEx));
                int headStructSize = Marshal.SizeOf(typeof(HaiLiDrvDemo.RCV_EKE_HEADEx));
                
                // 解析分钟数据 - 使用字典按股票代码分组
                Dictionary<string, List<MinuteData>> stockMinuteDataDict = new Dictionary<string, List<MinuteData>>();
                string currentStockCode = "";
                int currentOffset = 0;

                for (int i = 0; i < pHeader.m_nPacketNum; i++)
                {
                    try
                    {
                        // 检查是否是数据头（EKE_HEAD_TAG）
                        int timeValue = Marshal.ReadInt32(new IntPtr((int)pHeader.m_pData + currentOffset));
                        
                        if ((uint)timeValue == StockDrv.EKE_HEAD_TAG)
                        {
                            // 如果之前有股票数据，先触发事件（处理上一个股票的数据）
                            if (!string.IsNullOrEmpty(currentStockCode) && 
                                stockMinuteDataDict.ContainsKey(currentStockCode) &&
                                stockMinuteDataDict[currentStockCode].Count > 0)
                            {
                                Logger.Instance.Success(string.Format("分钟数据处理完成: {0}, 共 {1} 条数据", currentStockCode, stockMinuteDataDict[currentStockCode].Count));
                                if (MinuteDataReceived != null)
                                {
                                    MinuteDataReceived(this, new MinuteDataEventArgs(currentStockCode, stockMinuteDataDict[currentStockCode]));
                                }
                            }
                            
                            // 这是数据头，包含股票代码
                            HaiLiDrvDemo.RCV_EKE_HEADEx head = (HaiLiDrvDemo.RCV_EKE_HEADEx)Marshal.PtrToStructure(
                                new IntPtr((int)pHeader.m_pData + currentOffset),
                                typeof(HaiLiDrvDemo.RCV_EKE_HEADEx));
                            currentStockCode = new string(head.m_szLabel).Trim('\0');
                            
                            // 标准化股票代码
                            currentStockCode = DataConverter.NormalizeStockCode(currentStockCode);
                            
                            Logger.Instance.Debug(string.Format("分钟数据头: {0}", currentStockCode));
                            
                            // 为新股票创建数据列表
                            if (!stockMinuteDataDict.ContainsKey(currentStockCode))
                            {
                                stockMinuteDataDict[currentStockCode] = new List<MinuteData>();
                            }
                            
                            currentOffset += headStructSize;
                        }
                        else
                        {
                            // 这是实际的分钟数据
                            HaiLiDrvDemo.RCV_MINUTE_STRUCTEx minute = (HaiLiDrvDemo.RCV_MINUTE_STRUCTEx)Marshal.PtrToStructure(
                                new IntPtr((int)pHeader.m_pData + currentOffset),
                                typeof(HaiLiDrvDemo.RCV_MINUTE_STRUCTEx));

                            if (!string.IsNullOrEmpty(currentStockCode))
                            {
                                // 转换UTC时间为DateTime
                                DateTime tradeTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                                    .AddSeconds(minute.m_time)
                                    .ToLocalTime();

                                MinuteData data = new MinuteData(
                                    currentStockCode,
                                    tradeTime,
                                    minute.m_fPrice,
                                    minute.m_fVolume,
                                    minute.m_fAmount);

                                // 添加到对应股票的数据列表
                                if (!stockMinuteDataDict.ContainsKey(currentStockCode))
                                {
                                    stockMinuteDataDict[currentStockCode] = new List<MinuteData>();
                                }
                                stockMinuteDataDict[currentStockCode].Add(data);
                            }
                            
                            currentOffset += minuteStructSize;
                        }
                    }
                    catch (Exception ex)
                    {
                        statistics.ErrorCount++;
                        Logger.Instance.Error(string.Format("分钟数据解析失败 [{0}]: {1}", i, ex.Message));
                        // 即使出错也继续处理下一条
                        currentOffset += minuteStructSize;
                    }
                }

                // 处理最后一个股票的数据（如果还有未触发的数据）
                if (!string.IsNullOrEmpty(currentStockCode) && 
                    stockMinuteDataDict.ContainsKey(currentStockCode) &&
                    stockMinuteDataDict[currentStockCode].Count > 0)
                {
                    Logger.Instance.Success(string.Format("分钟数据处理完成: {0}, 共 {1} 条数据", currentStockCode, stockMinuteDataDict[currentStockCode].Count));
                    if (MinuteDataReceived != null)
                    {
                        MinuteDataReceived(this, new MinuteDataEventArgs(currentStockCode, stockMinuteDataDict[currentStockCode]));
                    }
                }
            }
            catch (Exception ex)
            {
                statistics.ErrorCount++;
                Logger.Instance.Error(string.Format("分钟数据处理失败: {0}", ex.Message));
                System.Diagnostics.Debug.WriteLine(string.Format("分钟数据处理失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 处理5分钟K线数据
        /// </summary>
        public void Process5MinuteData(IntPtr lParam)
        {
            try
            {
                HaiLiDrvDemo.RCV_DATA pHeader = (HaiLiDrvDemo.RCV_DATA)Marshal.PtrToStructure(
                    lParam, 
                    typeof(HaiLiDrvDemo.RCV_DATA));

                if (pHeader.m_wDataType != StockDrv.FILE_5MINUTE_EX)
                    return;

                // 解析5分钟数据
                List<Minute5Data> minute5DataList = new List<Minute5Data>();
                string currentStockCode = "";

                for (int i = 0; i < pHeader.m_nPacketNum; i++)
                {
                    try
                    {
                        // 检查是否是数据头
                        int timeValue = Marshal.ReadInt32(new IntPtr((int)pHeader.m_pData + 32 * i));
                        
                        if ((uint)timeValue == StockDrv.EKE_HEAD_TAG)
                        {
                            // 数据头
                            HaiLiDrvDemo.RCV_EKE_HEADEx head = (HaiLiDrvDemo.RCV_EKE_HEADEx)Marshal.PtrToStructure(
                                new IntPtr((int)pHeader.m_pData + 32 * i),
                                typeof(HaiLiDrvDemo.RCV_EKE_HEADEx));
                            currentStockCode = new string(head.m_szLabel).Trim('\0');
                        }
                        else
                        {
                            // 5分钟K线数据
                            HaiLiDrvDemo.RCV_HISMINUTE_STRUCTEx minute5 = (HaiLiDrvDemo.RCV_HISMINUTE_STRUCTEx)Marshal.PtrToStructure(
                                new IntPtr((int)pHeader.m_pData + 32 * i),
                                typeof(HaiLiDrvDemo.RCV_HISMINUTE_STRUCTEx));

                            if (!string.IsNullOrEmpty(currentStockCode))
                            {
                                DateTime tradeTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                                    .AddSeconds(minute5.m_time)
                                    .ToLocalTime();

                                Minute5Data data = new Minute5Data
                                {
                                    StockCode = currentStockCode,
                                    TradeTime = tradeTime,
                                    Open = minute5.m_fOpen,
                                    High = minute5.m_fHigh,
                                    Low = minute5.m_fLow,
                                    Close = minute5.m_fClose,
                                    Volume = minute5.m_fVolume,
                                    Amount = minute5.m_fAmount,
                                    ActiveBuyVol = minute5.m_fActiveBuyVol
                                };

                                minute5DataList.Add(data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        statistics.ErrorCount++;
                        System.Diagnostics.Debug.WriteLine(string.Format("5分钟数据解析失败 [{0}]: {1}", i, ex.Message));
                    }
                }

                // 如果有数据，触发事件
                if (minute5DataList.Count > 0 && !string.IsNullOrEmpty(currentStockCode))
                {
                    if (Minute5DataReceived != null)
                    {
                        Minute5DataReceived(this, new Minute5DataEventArgs(currentStockCode, minute5DataList));
                    }
                }
            }
            catch (Exception ex)
            {
                statistics.ErrorCount++;
                System.Diagnostics.Debug.WriteLine(string.Format("5分钟数据处理失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 分钟数据接收事件
        /// </summary>
        public event EventHandler<MinuteDataEventArgs> MinuteDataReceived;

        /// <summary>
        /// 5分钟数据接收事件
        /// </summary>
        public event EventHandler<Minute5DataEventArgs> Minute5DataReceived;

        /// <summary>
        /// 处理码表数据
        /// </summary>
        public void ProcessMarketTableData(IntPtr lParam)
        {
            try
            {
                HaiLiDrvDemo.HLMarketType mHeader = (HaiLiDrvDemo.HLMarketType)Marshal.PtrToStructure(
                    lParam, 
                    typeof(HaiLiDrvDemo.HLMarketType));

                string marketName = new string(mHeader.m_Name).Trim('\0');
                int stockCount = mHeader.m_nCount;
                
                // 更新统计信息
                statistics.MarketTableCount++;
                statistics.TotalMarketTableStocks += stockCount;
                
                Logger.Instance.Info(string.Format("收到码表数据包 #{0}: 市场={1}, 本包股票数量={2}, 累计码表股票数={3}", statistics.MarketTableCount, marketName, stockCount, statistics.TotalMarketTableStocks));
                
                Dictionary<string, string> codeDict = new Dictionary<string, string>();

                // 遍历所有股票代码
                for (int i = 0; i < mHeader.m_nCount; i++)
                {
                    try
                    {
                        HaiLiDrvDemo.RCV_TABLE_STRUCT table = (HaiLiDrvDemo.RCV_TABLE_STRUCT)Marshal.PtrToStructure(
                            new IntPtr((int)lParam + 54 + 44 * i), 
                            typeof(HaiLiDrvDemo.RCV_TABLE_STRUCT));

                        string stockCode = new string(table.m_szLabel).Trim('\0');
                        string stockName = new string(table.m_szName).Trim('\0');

                        if (!string.IsNullOrEmpty(stockCode) && !string.IsNullOrEmpty(stockName))
                        {
                            codeDict[stockCode] = stockName;
                        }
                    }
                    catch (Exception ex)
                    {
                        statistics.ErrorCount++;
                        System.Diagnostics.Debug.WriteLine(string.Format("码表项解析失败 [{0}]: {1}", i, ex.Message));
                    }
                }

                // 触发码表接收事件
                if (MarketTableReceived != null && codeDict.Count > 0)
                {
                    MarketTableReceived(this, new MarketTableEventArgs(codeDict));
                    Logger.Instance.Success(string.Format("码表数据已处理: 市场={0}, 有效股票数={1}, 当前总码表股票数={2}", marketName, codeDict.Count, statistics.TotalMarketTableStocks));
                }
            }
            catch (Exception ex)
            {
                statistics.ErrorCount++;
                Logger.Instance.Error(string.Format("码表数据处理失败: {0}", ex.Message));
                System.Diagnostics.Debug.WriteLine(string.Format("码表数据处理失败: {0}", ex.Message));
            }
        }

        // F10功能已禁用
        // /// <summary>
        // /// 处理F10数据（个股资料）
        // /// </summary>
        /*
        public void ProcessStockBaseData(IntPtr lParam)
        {
            try
            {
                if (lParam == IntPtr.Zero)
                {
                    Logger.Instance.Error("ProcessStockBaseData: lParam为空指针");
                    return;
                }

                RCV_DATA pHeader;
                try
                {
                    pHeader = (RCV_DATA)Marshal.PtrToStructure(
                        lParam, 
                        typeof(RCV_DATA));
                }
                catch (Exception structEx)
                {
                    Logger.Instance.Error(string.Format("ProcessStockBaseData: 读取RCV_DATA结构失败: {0}, lParam={1}", structEx.Message, lParam.ToInt64()));
                    return;
                }

                if (pHeader.m_wDataType != StockDrv.FILE_BASE_EX)
                {
                    Logger.Instance.Debug(string.Format("ProcessStockBaseData: 数据类型不匹配，期望={0}, 实际={1}", StockDrv.FILE_BASE_EX, pHeader.m_wDataType));
                    return;
                }

                // 获取文件名（从RCV_FILE_HEADEx结构中）
                string fileName = "";
                try
                {
                    fileName = new string(pHeader.m_File.m_szFileName).Trim('\0');
                }
                catch (Exception nameEx)
                {
                    Logger.Instance.Warning(string.Format("ProcessStockBaseData: 读取文件名失败: {0}", nameEx.Message));
                }

                int dataLength = (int)pHeader.m_File.m_dwLen;
                if (dataLength < 0)
                {
                    Logger.Instance.Warning(string.Format("ProcessStockBaseData: 数据长度为负数: {0}", dataLength));
                    dataLength = 0;
                }

                // 减少日志输出，只在调试时输出详细信息
                if (dataLength > 0)
                {
                    Logger.Instance.Debug(string.Format("收到F10数据: 文件名={0}, 长度={1} 字节, m_pData={2}", fileName, dataLength, pHeader.m_pData.ToInt64()));
                }
                else
                {
                    Logger.Instance.Info(string.Format("收到F10数据: 文件名={0}, 长度={1} 字节", fileName, dataLength));
                }

                // 从m_pData读取数据内容（文本格式）
                // 优化：延迟读取内容，只在匹配成功时才读取，提高性能
                string content = "";
                if (pHeader.m_pData != IntPtr.Zero && dataLength > 0)
                {
                    try
                    {
                        // 限制最大读取长度，防止异常数据
                        int maxLength = Math.Min(dataLength, 10 * 1024 * 1024); // 最大10MB
                        
                        // 优化：对于大文件，使用更高效的读取方式
                        if (maxLength > 100 * 1024)  // 大于100KB时使用字节数组方式
                        {
                            byte[] buffer = new byte[maxLength];
                            Marshal.Copy(pHeader.m_pData, buffer, 0, maxLength);
                            content = System.Text.Encoding.Default.GetString(buffer, 0, maxLength);
                        }
                        else
                        {
                            // 小文件直接使用Marshal.PtrToStringAnsi
                            content = Marshal.PtrToStringAnsi(pHeader.m_pData, maxLength);
                        }
                        
                        if (content == null)
                        {
                            content = "";
                            Logger.Instance.Warning("ProcessStockBaseData: 字符串转换返回null，数据可能无效");
                        }
                    }
                    catch (AccessViolationException avEx)
                    {
                        Logger.Instance.Error(string.Format("ProcessStockBaseData: 访问冲突，无法读取F10数据内容: {0}", avEx.Message));
                        Logger.Instance.Error(string.Format("m_pData={0}, dataLength={1}", pHeader.m_pData.ToInt64(), dataLength));
                        content = "";
                    }
                    catch (Exception readEx)
                    {
                        Logger.Instance.Error(string.Format("ProcessStockBaseData: 读取F10数据内容失败: {0}", readEx.Message));
                        content = "";
                    }
                }
                else
                {
                    if (pHeader.m_pData == IntPtr.Zero)
                    {
                        Logger.Instance.Warning("ProcessStockBaseData: m_pData指针为空");
                    }
                    if (dataLength <= 0)
                    {
                        Logger.Instance.Warning(string.Format("ProcessStockBaseData: 数据长度无效: {0}", dataLength));
                    }
                }

                // 从文件名中提取股票代码（如果文件名包含股票代码信息）
                // 例如：文件名可能是 "600026.txt" 或 "SH600026.txt"
                string stockCode = "";
                if (!string.IsNullOrEmpty(fileName))
                {
                    // 尝试从文件名提取股票代码
                    string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    stockCode = DataConverter.NormalizeStockCode(nameWithoutExt);
                }

                // 如果无法从文件名提取，尝试从数据内容中提取（某些情况下数据开头可能包含股票代码）
                if (string.IsNullOrEmpty(stockCode) && !string.IsNullOrEmpty(content))
                {
                    // 这里可以根据实际数据格式进行解析
                    // 暂时使用空字符串，由调用方提供股票代码
                }

                // 触发F10数据接收事件
                if (StockBaseDataReceived != null)
                {
                    StockBaseDataReceived(this, new StockBaseDataEventArgs(stockCode, fileName, content, dataLength));
                    Logger.Instance.Success(string.Format("F10数据处理完成: 股票={0}, 文件名={1}, 内容长度={2} 字符", stockCode, fileName, content?.Length ?? 0));
                }
            }
            catch (Exception ex)
            {
                statistics.ErrorCount++;
                Logger.Instance.Error(string.Format("处理F10数据异常: {0}", ex.Message));
                System.Diagnostics.Debug.WriteLine(string.Format("ProcessStockBaseData异常: {0}", ex.Message));
            }
        }
        */

        /// <summary>
        /// 重置统计信息
        /// </summary>
        public void ResetStatistics()
        {
            statistics = new DataStatistics();
        }
    }

    /// <summary>
    /// 股票数据事件参数
    /// </summary>
    public class StockDataEventArgs : EventArgs
    {
        private StockData _stockData;
        public StockData StockData
        {
            get { return _stockData; }
            private set { _stockData = value; }
        }

        public StockDataEventArgs(StockData stockData)
        {
            StockData = stockData;
        }
    }

    /// <summary>
    /// 码表数据事件参数
    /// </summary>
    public class MarketTableEventArgs : EventArgs
    {
        private Dictionary<string, string> _codeDictionary;
        public Dictionary<string, string> CodeDictionary
        {
            get { return _codeDictionary; }
            private set { _codeDictionary = value; }
        }

        public MarketTableEventArgs(Dictionary<string, string> codeDict)
        {
            CodeDictionary = codeDict;
        }
    }

    /// <summary>
    /// 分钟数据事件参数
    /// </summary>
    public class MinuteDataEventArgs : EventArgs
    {
        private string _stockCode;
        public string StockCode
        {
            get { return _stockCode; }
            private set { _stockCode = value; }
        }
        private List<MinuteData> _minuteDataList;
        public List<MinuteData> MinuteDataList
        {
            get { return _minuteDataList; }
            private set { _minuteDataList = value; }
        }

        public MinuteDataEventArgs(string stockCode, List<MinuteData> minuteDataList)
        {
            StockCode = stockCode;
            MinuteDataList = minuteDataList;
        }
    }

    /// <summary>
    /// 5分钟数据事件参数
    /// </summary>
    public class Minute5DataEventArgs : EventArgs
    {
        private string _stockCode;
        public string StockCode
        {
            get { return _stockCode; }
            private set { _stockCode = value; }
        }
        private List<Minute5Data> _minute5DataList;
        public List<Minute5Data> Minute5DataList
        {
            get { return _minute5DataList; }
            private set { _minute5DataList = value; }
        }

        public Minute5DataEventArgs(string stockCode, List<Minute5Data> minute5DataList)
        {
            StockCode = stockCode;
            Minute5DataList = minute5DataList;
        }
    }

    // F10功能已禁用
    // /// <summary>
    // /// F10数据事件参数
    // /// </summary>
    // public class StockBaseDataEventArgs : EventArgs
    // {
    //     public string StockCode { get; private set; }
    //     public string FileName { get; private set; }
    //     public string Content { get; private set; }
    //     public int DataLength { get; private set; }
    //
    //     public StockBaseDataEventArgs(string stockCode, string fileName, string content, int dataLength)
    //     {
    //         StockCode = stockCode;
    //         FileName = fileName;
    //         Content = content;
    //         DataLength = dataLength;
    //     }
    // }
}

