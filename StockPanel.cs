using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace HaiLiDrvDemo
{
    /// <summary>
    /// 股票板块面板控件
    /// </summary>
    public class StockPanel : Panel
    {
        private Label lblTitle;
        private DataGridView dgvStocks;
        private StockBoard board;
        private Button btnClose;

        private bool isSelected = false;
        
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                isSelected = value;
                UpdateSelectedStyle();
            }
        }

        public StockBoard Board
        {
            get { return board; }
            set
            {
                board = value;
                if (board != null)
                {
                    UpdateTitle();
                }
            }
        }

        private void UpdateTitle()
        {
            if (board != null && lblTitle != null)
            {
                int totalCount = board.StockCodes.Count;
                int filteredCount = board.GetFilteredStocks().Count;
                lblTitle.Text = string.Format("{0} ({1}/{2})", board.BoardName, filteredCount, totalCount);
            }
        }

        private void UpdateSelectedStyle()
        {
            if (isSelected)
            {
                this.BackColor = Color.FromArgb(60, 60, 60);
                this.BorderStyle = BorderStyle.Fixed3D;
            }
            else
            {
                this.BackColor = Color.FromArgb(40, 40, 40);
                this.BorderStyle = BorderStyle.FixedSingle;
            }
        }

        public StockPanel()
        {
            InitializeComponent();
            this.Click += StockPanel_Click;
            this.lblTitle.Click += StockPanel_Click;
            this.dgvStocks.Click += StockPanel_Click;
        }

        private void StockPanel_Click(object sender, EventArgs e)
        {
            // 触发选择事件
            if (PanelSelected != null)
            {
                PanelSelected(this, EventArgs.Empty);
            }
        }

        public event EventHandler PanelSelected;

        private void InitializeComponent()
        {
            this.BackColor = Color.FromArgb(40, 40, 40);
            this.BorderStyle = BorderStyle.FixedSingle;
            this.Size = new Size(200, 300);
            this.Cursor = Cursors.Hand;

            // 启用自动滚动
            this.AutoScroll = false;  // Panel本身不需要滚动，DataGridView会有自己的滚动条

            // 标题栏
            lblTitle = new Label();
            lblTitle.BackColor = Color.FromArgb(60, 60, 60);
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
            lblTitle.Location = new Point(0, 0);
            lblTitle.Size = new Size(this.Width - 20, 25);
            lblTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblTitle.Padding = new Padding(5, 0, 0, 0);
            lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(lblTitle);

            // 关闭按钮
            btnClose = new Button();
            btnClose.Text = "×";
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.BackColor = Color.FromArgb(60, 60, 60);
            btnClose.ForeColor = Color.White;
            btnClose.Font = new Font("Arial", 12, FontStyle.Bold);
            btnClose.Location = new Point(this.Width - 20, 0);
            btnClose.Size = new Size(20, 25);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Click += BtnClose_Click;
            this.Controls.Add(btnClose);

            // 数据表格
            dgvStocks = new DataGridView();
            dgvStocks.BackgroundColor = Color.FromArgb(40, 40, 40);
            dgvStocks.BorderStyle = BorderStyle.None;
            dgvStocks.AllowUserToAddRows = false;
            dgvStocks.AllowUserToDeleteRows = false;
            dgvStocks.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvStocks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvStocks.ColumnHeadersVisible = true;  // 确保列标题可见
            dgvStocks.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(50, 50, 50);
            dgvStocks.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvStocks.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei", 8);
            dgvStocks.DefaultCellStyle.BackColor = Color.FromArgb(40, 40, 40);
            dgvStocks.DefaultCellStyle.ForeColor = Color.White;
            dgvStocks.DefaultCellStyle.Font = new Font("Microsoft YaHei", 8);
            dgvStocks.DefaultCellStyle.SelectionBackColor = Color.FromArgb(70, 70, 70);
            dgvStocks.RowHeadersVisible = false;
            dgvStocks.ReadOnly = true;
            dgvStocks.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvStocks.Location = new Point(0, 25);
            dgvStocks.Size = new Size(this.Width, this.Height - 25);
            dgvStocks.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvStocks.ScrollBars = ScrollBars.Both;  // 启用水平和垂直滚动条
            dgvStocks.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;  // 防止自动调整行高
            this.Controls.Add(dgvStocks);

            // 添加列
            DataGridViewTextBoxColumn colName = new DataGridViewTextBoxColumn();
            colName.HeaderText = "名称";
            colName.Name = "colName";
            colName.ReadOnly = true;
            colName.Width = 100;

            DataGridViewTextBoxColumn colChange = new DataGridViewTextBoxColumn();
            colChange.HeaderText = "涨跌幅";
            colChange.Name = "colChange";
            colChange.ReadOnly = true;
            colChange.Width = 80;

            dgvStocks.Columns.Add(colName);
            dgvStocks.Columns.Add(colChange);
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            // 触发关闭事件
            if (PanelClosed != null)
            {
                PanelClosed(this, EventArgs.Empty);
            }
        }

        public event EventHandler PanelClosed;

        /// <summary>
        /// 刷新面板数据
        /// </summary>
        public void RefreshData()
        {
            if (board == null || dgvStocks == null)
            {
                return;
            }

            // 更新标题（显示数量）
            UpdateTitle();

            // 获取满足条件的股票列表
            List<StockData> filteredStocks = board.GetFilteredStocks();

            // 更新表格
            dgvStocks.SuspendLayout();  // 暂停布局以提高性能
            dgvStocks.Rows.Clear();

            foreach (var stock in filteredStocks)
            {
                int rowIndex = dgvStocks.Rows.Add();
                dgvStocks.Rows[rowIndex].Cells[0].Value = stock.Name;
                dgvStocks.Rows[rowIndex].Cells[1].Value = stock.ChangePercent.ToString("F2") + "%";

                // 根据涨跌幅设置颜色
                if (stock.ChangePercent > 0)
                {
                    dgvStocks.Rows[rowIndex].Cells[1].Style.ForeColor = Color.Red;
                }
                else if (stock.ChangePercent < 0)
                {
                    dgvStocks.Rows[rowIndex].Cells[1].Style.ForeColor = Color.Green;
                }
                else
                {
                    dgvStocks.Rows[rowIndex].Cells[1].Style.ForeColor = Color.White;
                }
            }

            dgvStocks.ResumeLayout();  // 恢复布局

            // 立即刷新，确保滚动条正确显示
            dgvStocks.PerformLayout();
            dgvStocks.Invalidate();
            dgvStocks.Refresh();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // 更新标题栏宽度
            if (lblTitle != null)
            {
                lblTitle.Width = this.Width - 20;
            }

            // 更新关闭按钮位置
            if (btnClose != null)
            {
                btnClose.Location = new Point(this.Width - 20, 0);
            }

            // 更新DataGridView大小（虽然有Anchor，但手动更新确保立即生效）
            if (dgvStocks != null)
            {
                dgvStocks.Size = new Size(this.Width, this.Height - 25);

                // 立即刷新界面，确保滚动条正确显示
                dgvStocks.PerformLayout();
                dgvStocks.Invalidate();
                dgvStocks.Update();
            }

            // 刷新整个面板
            this.PerformLayout();
            this.Invalidate();
            this.Refresh();
        }
    }
}

