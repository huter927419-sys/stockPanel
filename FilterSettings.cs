using System;

namespace HaiLiDrvDemo
{
    /// <summary>
    /// 数据筛选条件设置
    /// </summary>
    [Serializable]
    public class FilterSettings
    {
        private bool enableChangePercentFilter;
        private decimal minChangePercent;
        private bool enableIntradayChangeFilter;
        private decimal minIntradayChangePercent;

        /// <summary>
        /// 是否启用涨幅筛选
        /// </summary>
        public bool EnableChangePercentFilter
        {
            get { return enableChangePercentFilter; }
            set { enableChangePercentFilter = value; }
        }

        /// <summary>
        /// 最小涨幅百分比（例如：3 表示 3%）
        /// </summary>
        public decimal MinChangePercent
        {
            get { return minChangePercent; }
            set { minChangePercent = value; }
        }

        /// <summary>
        /// 是否启用日内涨幅筛选（现价相对开盘价）
        /// </summary>
        public bool EnableIntradayChangeFilter
        {
            get { return enableIntradayChangeFilter; }
            set { enableIntradayChangeFilter = value; }
        }

        /// <summary>
        /// 最小日内涨幅百分比（例如：5 表示现价比开盘价高 5%）
        /// </summary>
        public decimal MinIntradayChangePercent
        {
            get { return minIntradayChangePercent; }
            set { minIntradayChangePercent = value; }
        }

        public FilterSettings()
        {
            EnableChangePercentFilter = false;
            MinChangePercent = 3.0m;
            EnableIntradayChangeFilter = false;
            MinIntradayChangePercent = 5.0m;
        }

        /// <summary>
        /// 检查股票数据是否符合筛选条件
        /// 注意：多个筛选条件是"或"（OR）的关系，只要满足其中一个条件就通过
        /// </summary>
        public bool PassFilter(StockData stock)
        {
            if (stock == null)
                return false;

            // 如果没有任何筛选条件启用，则显示所有数据
            if (!EnableChangePercentFilter && !EnableIntradayChangeFilter)
                return true;

            bool passChangePercent = false;
            bool passIntradayChange = false;

            // 涨幅筛选
            if (EnableChangePercentFilter)
            {
                decimal changePercent = (decimal)stock.ChangePercent;
                passChangePercent = changePercent >= MinChangePercent;
            }

            // 日内涨幅筛选（现价相对开盘价）
            if (EnableIntradayChangeFilter)
            {
                if (stock.Open > 0)
                {
                    decimal intradayChange = (decimal)(((stock.NewPrice - stock.Open) / stock.Open) * 100);
                    passIntradayChange = intradayChange >= MinIntradayChangePercent;
                }
            }

            // "或"关系：只要满足其中一个条件就通过
            // 如果只启用了一个筛选条件，则只检查该条件
            // 如果启用了两个筛选条件，则只要满足其中一个就通过
            if (EnableChangePercentFilter && EnableIntradayChangeFilter)
            {
                // 两个条件都启用，只要满足其中一个就通过
                return passChangePercent || passIntradayChange;
            }
            else if (EnableChangePercentFilter)
            {
                // 只启用涨幅筛选
                return passChangePercent;
            }
            else if (EnableIntradayChangeFilter)
            {
                // 只启用日内涨幅筛选
                return passIntradayChange;
            }

            return true;
        }

        /// <summary>
        /// 创建副本
        /// </summary>
        public FilterSettings Clone()
        {
            FilterSettings copy = new FilterSettings();
            copy.EnableChangePercentFilter = this.EnableChangePercentFilter;
            copy.MinChangePercent = this.MinChangePercent;
            copy.EnableIntradayChangeFilter = this.EnableIntradayChangeFilter;
            copy.MinIntradayChangePercent = this.MinIntradayChangePercent;
            return copy;
        }
    }
}
