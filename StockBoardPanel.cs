using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HaiLiDrvDemo
{
    /// <summary>
    /// 板块面板 - 显示一个板块的股票列表
    /// </summary>
    public class StockBoardPanel : Panel
    {
        private string boardName;
        private List<string> stockCodes = new List<string>();
        private DataGridView dgvStocks;
        private TextBox txtBoardName;
        private Button btnAdd;
        private Button btnDelete;
        // 已移除关闭按钮 btnDeleteBoard
        private Panel headerPanel;
        
        private DataManager dataManager;
        private System.Windows.Forms.Timer refreshTimer;

        // 筛选设置
        private FilterSettings filterSettings = null;

        // 加载标志：用于区分是加载数据还是用户操作
        private bool isLoading = false;
        
        // 虚拟模式支持（用于大数据量场景）
        private List<StockDisplayItem> virtualModeData = new List<StockDisplayItem>();
        private bool useVirtualMode = false;  // 当数据量超过100条时启用虚拟模式
        
        // ControlAdded事件处理引用（用于取消订阅，防止内存泄漏）
        private ControlEventHandler controlAddedHandler;
        
        // 调整大小相关变量
        private bool isResizing = false;
        private ResizeDirection resizeDirection = ResizeDirection.None;
        private Point resizeStartPoint;
        private Size resizeStartSize;
        private const int RESIZE_MARGIN = 8;  // 边缘检测区域宽度（增大以便更容易触发）
        
        // 调整方向枚举
        private enum ResizeDirection
        {
            None,
            Right,
            Bottom,
            BottomRight
        }
        
        // 自定义事件参数类
        public class BoardNameChangedEventArgs : EventArgs
        {
            private string _newName;
            public string NewName
            {
                get { return _newName; }
                private set { _newName = value; }
            }

            public BoardNameChangedEventArgs(string newName)
            {
                NewName = newName;
            }
        }

        public class BoardNameValidatingEventArgs : EventArgs
        {
            private string _newName;
            public string NewName
            {
                get { return _newName; }
                private set { _newName = value; }
            }

            private StockBoardPanel _currentPanel;
            public StockBoardPanel CurrentPanel
            {
                get { return _currentPanel; }
                private set { _currentPanel = value; }
            }

            private bool _isValid;
            public bool IsValid
            {
                get { return _isValid; }
                set { _isValid = value; }
            }

            public BoardNameValidatingEventArgs(string newName, StockBoardPanel currentPanel)
            {
                NewName = newName;
                CurrentPanel = currentPanel;
                IsValid = true;  // 默认有效
            }
        }
        
        public event EventHandler<BoardNameChangedEventArgs> BoardNameChanged;
        public event EventHandler<BoardNameValidatingEventArgs> BoardNameValidating;
        public event EventHandler DeleteBoardRequested;  // 删除板块请求事件

        public string BoardName
        {
            get { return boardName; }
            set
            {
                if (boardName != value)
                {
                    boardName = value;
                    if (txtBoardName != null)
                    {
                        txtBoardName.Text = value;
                    }
                    // 只有在非加载状态下才触发事件（避免加载配置时触发保存）
                    if (!isLoading)
                    {
                        OnBoardNameChanged(value);
                    }
                    else
                    {
                        Logger.Instance.Debug(string.Format("板块[{0}]正在加载配置，跳过BoardNameChanged事件", boardName));
                    }
                }
            }
        }
        
        public List<string> StockCodes
        {
            get { return new List<string>(stockCodes); }
        }
        
        public StockBoardPanel(string name, DataManager dataMgr)
        {
            this.boardName = name;
            this.dataManager = dataMgr;
            this.stockCodes = new List<string>();
            
            InitializeComponent();
            SetupRefreshTimer();
            SetupResizeHandlers();
            
            // 订阅Resize事件，确保大小改变时正确刷新
            this.Resize += StockBoardPanel_Resize;
        }
        
        /// <summary>
        /// 面板大小改变时的处理
        /// </summary>
        private void StockBoardPanel_Resize(object sender, EventArgs e)
        {
            // 强制刷新面板和所有子控件（使用Refresh确保立即重绘）
            this.Invalidate(true);
            this.Refresh();
            
            // 确保 DataGridView 列标题可见（面板大小改变时可能被重置）
            if (dgvStocks != null && !dgvStocks.IsDisposed)
            {
                EnsureColumnHeadersVisible();
            }
            
            // 刷新内部控件
            foreach (Control control in this.Controls)
            {
                if (control != null && !control.IsDisposed)
                {
                    control.Invalidate(true);
                    control.Refresh();
                }
            }
            
            // 找到父容器并刷新
            Control parent = this.Parent;
            while (parent != null && !(parent is TableLayoutPanel))
            {
                parent = parent.Parent;
            }
            if (parent != null && !parent.IsDisposed)
            {
                parent.Invalidate(true);
                parent.Refresh();
            }
        }
        
        /// <summary>
        /// 设置调整大小的事件处理
        /// </summary>
        private void SetupResizeHandlers()
        {
            this.MouseDown += StockBoardPanel_MouseDown;
            this.MouseMove += StockBoardPanel_MouseMove;
            this.MouseUp += StockBoardPanel_MouseUp;
            this.MouseLeave += StockBoardPanel_MouseLeave;
            
            // 为所有子控件也添加鼠标事件处理（用于边缘检测和调整大小）
            // 使用实例方法引用，避免lambda捕获导致的内存泄漏
            controlAddedHandler = (s, e) =>
            {
                SetupChildControlResizeHandlers(e.Control);
            };
            this.ControlAdded += controlAddedHandler;
            
            // 为已存在的子控件设置事件处理
            foreach (Control child in this.Controls)
            {
                SetupChildControlResizeHandlers(child);
            }
        }
        
        /// <summary>
        /// 为子控件设置调整大小事件处理
        /// </summary>
        private void SetupChildControlResizeHandlers(Control child)
        {
            if (child == null) return;
            
            // 鼠标移动事件 - 用于边缘检测和光标显示
            child.MouseMove += (sender, args) =>
            {
                if (!isResizing)
                {
                    Point panelPos = this.PointToClient(((Control)sender).PointToScreen(args.Location));
                    ResizeDirection direction = GetResizeDirection(panelPos);
                    SetResizeCursor(direction);
                }
            };
            
            // 鼠标按下事件 - 用于开始调整大小
            child.MouseDown += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left)
                {
                    Point panelPos = this.PointToClient(((Control)sender).PointToScreen(args.Location));
                    resizeDirection = GetResizeDirection(panelPos);
                    if (resizeDirection != ResizeDirection.None)
                    {
                        isResizing = true;
                        resizeStartPoint = this.PointToScreen(panelPos);
                        resizeStartSize = this.Size;
                        this.Capture = true;
                        // 注意：MouseEventArgs 不支持 Handled 属性，通过 Capture 已经可以控制事件
                    }
                }
            };
            
            // 鼠标抬起事件 - 用于结束调整大小
            child.MouseUp += (sender, args) =>
            {
                if (isResizing && args.Button == MouseButtons.Left)
                {
                    isResizing = false;
                    resizeDirection = ResizeDirection.None;
                    this.Capture = false;
                    this.Cursor = Cursors.Default;
                }
            };
            
            // 鼠标离开事件
            child.MouseLeave += (sender, args) =>
            {
                if (!isResizing)
                {
                    this.Cursor = Cursors.Default;
                }
            };
        }
        
        /// <summary>
        /// 检测鼠标位置是否在调整大小的边缘
        /// </summary>
        private ResizeDirection GetResizeDirection(Point mousePos)
        {
            // 检查是否在右边缘（右侧5像素内）
            bool nearRight = mousePos.X >= this.Width - RESIZE_MARGIN && mousePos.X <= this.Width;
            // 检查是否在底边缘（底部5像素内）
            bool nearBottom = mousePos.Y >= this.Height - RESIZE_MARGIN && mousePos.Y <= this.Height;
            
            // 排除标题栏区域（前30像素），避免与标题栏按钮冲突
            bool notInHeader = mousePos.Y >= 30;
            
            if (notInHeader)
            {
                if (nearRight && nearBottom)
                    return ResizeDirection.BottomRight;
                else if (nearRight)
                    return ResizeDirection.Right;
                else if (nearBottom)
                    return ResizeDirection.Bottom;
            }
            
            return ResizeDirection.None;
        }
        
        /// <summary>
        /// 根据调整方向设置光标
        /// </summary>
        private void SetResizeCursor(ResizeDirection direction)
        {
            switch (direction)
            {
                case ResizeDirection.Right:
                    this.Cursor = Cursors.SizeWE;
                    break;
                case ResizeDirection.Bottom:
                    this.Cursor = Cursors.SizeNS;
                    break;
                case ResizeDirection.BottomRight:
                    this.Cursor = Cursors.SizeNWSE;
                    break;
                default:
                    this.Cursor = Cursors.Default;
                    break;
            }
        }
        
        private void StockBoardPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                resizeDirection = GetResizeDirection(e.Location);
                if (resizeDirection != ResizeDirection.None)
                {
                    isResizing = true;
                    resizeStartPoint = this.PointToScreen(e.Location);
                    resizeStartSize = this.Size;
                    this.Capture = true;  // 捕获鼠标事件
                }
            }
        }
        
        private void StockBoardPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (isResizing)
            {
                // 正在调整大小
                Point currentScreenPos = this.PointToScreen(e.Location);
                int deltaX = currentScreenPos.X - resizeStartPoint.X;
                int deltaY = currentScreenPos.Y - resizeStartPoint.Y;
                
                int newWidth = resizeStartSize.Width;
                int newHeight = resizeStartSize.Height;
                
                if (resizeDirection == ResizeDirection.Right || resizeDirection == ResizeDirection.BottomRight)
                {
                    newWidth = Math.Max(150, resizeStartSize.Width + deltaX);  // 最小宽度150
                }
                
                if (resizeDirection == ResizeDirection.Bottom || resizeDirection == ResizeDirection.BottomRight)
                {
                    newHeight = Math.Max(100, resizeStartSize.Height + deltaY);  // 最小高度100
                }
                
                // 更新面板大小
                Size newSize = new Size(newWidth, newHeight);
                if (this.Size != newSize)
                {
                    this.Size = newSize;
                    
                    // 强制刷新面板显示（使用Refresh确保立即重绘）
                    this.Invalidate(true);
                    this.Refresh();
                    
                    // 更新 TableLayoutPanel 中的列和行大小
                    UpdateTableLayoutSize();
                    
                    // 处理待处理的Windows消息，确保UI实时更新
                    Application.DoEvents();
                }
            }
            else
            {
                // 检测边缘并更新光标
                ResizeDirection direction = GetResizeDirection(e.Location);
                SetResizeCursor(direction);
            }
        }
        
        /// <summary>
        /// 处理窗口消息，用于在调整大小时捕获全局鼠标事件
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEMOVE = 0x0200;
            const int WM_LBUTTONUP = 0x0201;
            
            // 只在调整大小状态下处理相关消息
            if (isResizing)
            {
                if (m.Msg == WM_MOUSEMOVE)
                {
                    // 使用Control.MousePosition获取全局鼠标位置（更可靠）
                    Point screenPos = Control.MousePosition;
                    int deltaX = screenPos.X - resizeStartPoint.X;
                    int deltaY = screenPos.Y - resizeStartPoint.Y;
                    
                    int newWidth = resizeStartSize.Width;
                    int newHeight = resizeStartSize.Height;
                    
                    if (resizeDirection == ResizeDirection.Right || resizeDirection == ResizeDirection.BottomRight)
                    {
                        newWidth = Math.Max(150, resizeStartSize.Width + deltaX);
                    }
                    
                    if (resizeDirection == ResizeDirection.Bottom || resizeDirection == ResizeDirection.BottomRight)
                    {
                        newHeight = Math.Max(100, resizeStartSize.Height + deltaY);
                    }
                    
                    Size newSize = new Size(newWidth, newHeight);
                    if (this.Size != newSize)
                    {
                        this.Size = newSize;
                        
                        // 强制刷新面板显示（使用Refresh确保立即重绘）
                        this.Invalidate(true);
                        this.Refresh();
                        
                        UpdateTableLayoutSize();
                        
                        // 处理待处理的Windows消息，确保UI实时更新
                        Application.DoEvents();
                    }
                }
                else if (m.Msg == WM_LBUTTONUP)
                {
                    // 鼠标抬起，结束调整大小
                    isResizing = false;
                    resizeDirection = ResizeDirection.None;
                    this.Capture = false;
                    this.Cursor = Cursors.Default;
                    
                    // 调整大小完成后，触发最终布局更新
                    UpdateTableLayoutSize();
                    
                    // 强制刷新所有相关控件
                    this.Invalidate(true);
                    this.Refresh();
                    
                    // 找到父容器并刷新
                    Control parent = this.Parent;
                    while (parent != null && !(parent is TableLayoutPanel))
                    {
                        parent = parent.Parent;
                    }
                    if (parent != null)
                    {
                        parent.Invalidate(true);
                        parent.Refresh();
                    }
                    
                    // 延迟一点触发大小改变事件，确保大小已经更新完成
                    // 检查句柄是否已创建，如果已创建则使用BeginInvoke，否则直接调用
                    if (this.IsHandleCreated)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            // 触发大小改变事件，通知父窗体保存配置
                            OnSizeChanged();
                        }));
                    }
                    else
                    {
                        // 句柄未创建，直接调用（在构造函数中可能发生）
                        OnSizeChanged();
                    }
                }
            }
            
            base.WndProc(ref m);
        }
        
        private void StockBoardPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (isResizing)
            {
                isResizing = false;
                resizeDirection = ResizeDirection.None;
                this.Capture = false;  // 释放鼠标捕获
                this.Cursor = Cursors.Default;
                
                // 调整大小完成后，触发最终布局更新和自动重排
                UpdateTableLayoutSize();
                
                // 强制刷新所有相关控件
                this.Invalidate(true);
                this.Refresh();
                
                // 找到父容器并刷新
                Control parent = this.Parent;
                while (parent != null && !(parent is TableLayoutPanel))
                {
                    parent = parent.Parent;
                }
                if (parent != null)
                {
                    parent.Invalidate(true);
                    parent.Refresh();
                }
                
                // 延迟一点触发大小改变事件，确保大小已经更新完成
                // 检查句柄是否已创建，如果已创建则使用BeginInvoke，否则直接调用
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        // 触发大小改变事件，通知父窗体保存配置
                        OnSizeChanged();
                    }));
                }
                else
                {
                    // 句柄未创建，直接调用（在构造函数中可能发生）
                    OnSizeChanged();
                }
            }
        }
        
        /// <summary>
        /// 面板大小改变事件（用于触发配置保存）
        /// </summary>
        public event EventHandler PanelSizeChanged;
        
        /// <summary>
        /// 触发大小改变事件
        /// </summary>
        protected virtual void OnSizeChanged()
        {
            PanelSizeChanged.Invoke(this, EventArgs.Empty);
        }
        
        private void StockBoardPanel_MouseLeave(object sender, EventArgs e)
        {
            if (!isResizing)
            {
                this.Cursor = Cursors.Default;
            }
        }
        
        /// <summary>
        /// 更新 TableLayoutPanel 中对应列和行的大小，并触发自动重排
        /// </summary>
        private void UpdateTableLayoutSize()
        {
            // 找到父容器 TableLayoutPanel
            Control parent = this.Parent;
            while (parent != null && !(parent is TableLayoutPanel))
            {
                parent = parent.Parent;
            }
            
            TableLayoutPanel tableLayout = parent as TableLayoutPanel; if (tableLayout != null)
            {
                // 获取当前面板在 TableLayoutPanel 中的位置
                TableLayoutPanelCellPosition position = tableLayout.GetPositionFromControl(this);
                
                if (position.Row >= 0 && position.Column >= 0)
                {
                    // 计算新的列宽和行高（考虑 Margin）
                    int newWidth = this.Width + this.Margin.Left + this.Margin.Right;
                    int newHeight = this.Height + this.Margin.Top + this.Margin.Bottom;
                    
                    bool columnChanged = false;
                    bool rowChanged = false;
                    
                    // 更新列宽（使用绝对大小）
                    if (tableLayout.ColumnStyles[position.Column].SizeType != SizeType.Absolute)
                    {
                        tableLayout.ColumnStyles[position.Column] = new ColumnStyle(SizeType.Absolute, newWidth);
                        columnChanged = true;
                    }
                    else if (Math.Abs(tableLayout.ColumnStyles[position.Column].Width - newWidth) > 1)
                    {
                        tableLayout.ColumnStyles[position.Column].Width = newWidth;
                        columnChanged = true;
                    }
                    
                    // 更新行高（使用绝对大小）
                    if (tableLayout.RowStyles[position.Row].SizeType != SizeType.Absolute)
                    {
                        tableLayout.RowStyles[position.Row] = new RowStyle(SizeType.Absolute, newHeight);
                        rowChanged = true;
                    }
                    else if (Math.Abs(tableLayout.RowStyles[position.Row].Height - newHeight) > 1)
                    {
                        tableLayout.RowStyles[position.Row].Height = newHeight;
                        rowChanged = true;
                    }
                    
                    // 如果列或行大小改变，触发重新布局
                    if (columnChanged || rowChanged)
                    {
                        // 强制TableLayoutPanel重新计算布局
                        tableLayout.SuspendLayout();
                        
                        // 确保所有面板都正确填充其单元格
                        foreach (Control control in tableLayout.Controls)
                        {
                            StockBoardPanel panel = control as StockBoardPanel; if (panel != null)
                            {
                                // 确保所有面板都正确填充单元格
                                if (panel.Dock != DockStyle.Fill)
                                {
                                    panel.Dock = DockStyle.Fill;
                                }
                            }
                        }
                        
                        tableLayout.ResumeLayout(true);
                        
                        // 强制刷新TableLayoutPanel和所有子控件（使用Refresh确保立即重绘）
                        tableLayout.Invalidate(true);
                        tableLayout.Refresh();
                        
                        // 刷新所有子面板
                        foreach (Control control in tableLayout.Controls)
                        {
                            StockBoardPanel panel = control as StockBoardPanel; if (panel != null)
                            {
                                panel.Invalidate(true);
                                panel.Refresh();
                            }
                        }
                        
                        // 确保当前面板也刷新
                        this.Invalidate(true);
                        this.Refresh();
                        
                        // 找到父窗体并刷新
                        Form parentForm = this.FindForm();
                        if (parentForm != null)
                        {
                            parentForm.Invalidate(true);
                            parentForm.Update();
                        }
                        
                        // 处理待处理的Windows消息，确保UI更新
                        Application.DoEvents();
                    }
                }
            }
        }
        
        private void InitializeComponent()
        {
            this.BackColor = Color.FromArgb(40, 40, 40);
            this.BorderStyle = BorderStyle.FixedSingle;
            
            // 确保面板不使用 Dock，防止被拉伸变形
            this.Dock = DockStyle.None;
            this.Anchor = AnchorStyles.None;
            
            // 启用双缓冲和自动重绘，提高绘制性能
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | 
                         ControlStyles.UserPaint | 
                         ControlStyles.DoubleBuffer | 
                         ControlStyles.ResizeRedraw, true);
            
            // 标题栏面板
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Color.FromArgb(50, 50, 50)
            };
            
            // 创建一个容器面板来放置按钮（靠右对齐，避免与名称框重叠）
            Panel buttonContainer = new Panel
            {
                Dock = DockStyle.Right,  // 靠右对齐，避免与左侧的名称框重叠
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Width = 80,  // 稍微增加宽度，让按钮有更多居中空间
                Visible = true
            };
            
            // 板块名称输入框（靠左，必须在 buttonContainer 之后添加，确保在最上层）
            // 确保使用实际的 boardName 值，如果为空则使用默认值
            string displayName = !string.IsNullOrEmpty(boardName) ? boardName : "板块1";
            txtBoardName = new TextBox
            {
                Location = new Point(5, 5),
                Size = new Size(80, 20),  // 名称框宽度
                Text = displayName,  // 使用实际的名称
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei", 8.5F, FontStyle.Regular),  // 设置合适的字体大小
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Visible = true,  // 确保可见
                TabIndex = 0,  // 设置Tab顺序
                ReadOnly = false  // 允许编辑
            };
            txtBoardName.TextChanged += TxtBoardName_TextChanged;
            txtBoardName.LostFocus += TxtBoardName_LostFocus;
            
            // 立即设置名称框的文本，确保显示
            if (txtBoardName != null)
            {
                txtBoardName.Text = displayName;
            }
            
            // 添加按钮（根据按钮实际大小设置，确保符号清晰可见）
            btnAdd = new Button
            {
                Text = "+",
                Size = new Size(24, 24),  // 简洁的正方形按钮
                BackColor = Color.FromArgb(55, 55, 55),  // 深灰色背景，低调融入界面
                ForeColor = Color.FromArgb(200, 200, 200),  // 浅灰色文字
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 14, FontStyle.Bold),  // Consolas等宽字体，符号居中更好
                Anchor = AnchorStyles.None,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Visible = true,
                TextAlign = ContentAlignment.MiddleCenter
            };
            // 设置扁平样式外观 - 无边框，简洁设计
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 75, 75);  // 悬停时稍微变亮
            btnAdd.FlatAppearance.MouseDownBackColor = Color.FromArgb(45, 45, 45);  // 点击时变暗
            btnAdd.Padding = new Padding(0, -1, 0, 1);  // 微调居中
            btnAdd.Click += BtnAdd_Click;
            
            // 删除按钮
            btnDelete = new Button
            {
                Text = "−",  // 使用Unicode减号
                Size = new Size(24, 24),  // 简洁的正方形按钮
                BackColor = Color.FromArgb(55, 55, 55),  // 深灰色背景，低调融入界面
                ForeColor = Color.FromArgb(200, 200, 200),  // 浅灰色文字
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 14, FontStyle.Bold),  // Consolas等宽字体，符号居中更好
                Anchor = AnchorStyles.None,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Visible = true,
                TextAlign = ContentAlignment.MiddleCenter
            };
            // 设置扁平样式外观 - 无边框，简洁设计
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 75, 75);  // 悬停时稍微变亮
            btnDelete.FlatAppearance.MouseDownBackColor = Color.FromArgb(45, 45, 45);  // 点击时变暗
            btnDelete.Padding = new Padding(0, -1, 0, 1);  // 微调居中
            btnDelete.Click += BtnDelete_Click;
            
            // 将按钮添加到容器中
            buttonContainer.Controls.Add(btnAdd);
            buttonContainer.Controls.Add(btnDelete);
            
            // 按钮布局事件（在右侧容器中居中排列）
            buttonContainer.Resize += (s, e) =>
            {
                // 简洁的正方形按钮设计
                int buttonSize = 24;  // 24x24正方形按钮
                int buttonSpacing = 4;  // 按钮间距
                int totalButtonsWidth = buttonSize * 2 + buttonSpacing;

                // 垂直居中
                int y = (buttonContainer.Height - buttonSize) / 2;

                // 计算按钮位置（在容器内水平居中）
                int centerX = buttonContainer.Width / 2;
                int startX = centerX - totalButtonsWidth / 2;

                // 更新按钮大小
                btnAdd.Size = new Size(buttonSize, buttonSize);
                btnDelete.Size = new Size(buttonSize, buttonSize);

                // 使用Consolas等宽字体，14号粗体
                btnAdd.Font = new Font("Consolas", 14, FontStyle.Bold);
                btnDelete.Font = new Font("Consolas", 14, FontStyle.Bold);

                // 确保文本居中
                btnAdd.TextAlign = ContentAlignment.MiddleCenter;
                btnDelete.TextAlign = ContentAlignment.MiddleCenter;
                btnAdd.Padding = new Padding(0, -1, 0, 1);  // 微调居中
                btnDelete.Padding = new Padding(0, -1, 0, 1);  // 微调居中

                // 设置按钮位置（在容器内居中排列）
                btnAdd.Location = new Point(startX, y);
                btnDelete.Location = new Point(startX + buttonSize + buttonSpacing, y);
            };
            
            // 先添加 buttonContainer（靠右对齐）
            headerPanel.Controls.Add(buttonContainer);
            
            // 然后添加 txtBoardName（靠左，在 buttonContainer 之前添加，确保在正确的Z-order）
            headerPanel.Controls.Add(txtBoardName);
            
            // 确保 txtBoardName 在最上层，不被 buttonContainer 遮挡
            txtBoardName.BringToFront();
            
            // 触发一次按钮布局更新，确保按钮初始位置正确
            buttonContainer.PerformLayout();
            
            // 触发一次布局更新，确保按钮初始位置正确
            headerPanel.Resize += (s, e) =>
            {
                buttonContainer.PerformLayout();
            };
            
            this.Controls.Add(headerPanel);
            
            // 数据表格
            dgvStocks = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(40, 40, 40),
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                ColumnHeadersVisible = true  // 确保列标题始终显示，即使没有数据
            };

            // 先设置高度和样式，再设置HeightSizeMode（顺序很重要！）
            dgvStocks.ColumnHeadersHeight = 28;  // 先设置表头高度
            dgvStocks.EnableHeadersVisualStyles = false;  // 禁用系统样式，使用自定义样式
            dgvStocks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;  // 最后禁用调整大小
            
            // 启用双缓冲，减少闪烁和残影（使用反射设置受保护属性）
            // DoubleBuffered 是 Control 的受保护属性，需要通过反射设置
            try
            {
                System.Reflection.PropertyInfo doubleBufferedProperty = typeof(Control).GetProperty("DoubleBuffered", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (doubleBufferedProperty != null)
                {
                    doubleBufferedProperty.SetValue(dgvStocks, true, null);
                    Logger.Instance.Debug(string.Format("板块[{0}] DataGridView双缓冲已启用", boardName));
                }
            }
            catch (Exception ex)
            {
                // 如果设置失败，记录日志但不影响功能
                Logger.Instance.Debug(string.Format("板块[{0}] 设置DataGridView双缓冲失败: {1}", boardName, ex.Message));
            }

            // 设置表格样式
            dgvStocks.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(50, 50, 50);
            dgvStocks.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvStocks.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
            dgvStocks.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvStocks.DefaultCellStyle.BackColor = Color.FromArgb(40, 40, 40);
            dgvStocks.DefaultCellStyle.ForeColor = Color.White;
            dgvStocks.DefaultCellStyle.Font = new Font("Microsoft YaHei", 9);
            dgvStocks.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);  // 蓝色高亮，类似Windows选中效果
            dgvStocks.DefaultCellStyle.SelectionForeColor = Color.White;  // 白色文字

            // 设置交替行颜色，增强可读性
            dgvStocks.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 45);  // 稍微亮一点的灰色
            dgvStocks.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);  // 选中时保持蓝色
            dgvStocks.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;

            dgvStocks.RowTemplate.Height = 24;  // 设置行高
            dgvStocks.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;  // 手动控制列宽
            
            // 添加列：序号、名称、涨幅
            InitializeDataGridViewColumns();

            // 添加到控件
            this.Controls.Add(dgvStocks);

            // 最后再次确保列标题可见（在添加到控件树之后）
            dgvStocks.ColumnHeadersVisible = true;
            dgvStocks.ColumnHeadersHeight = 28;
            
            // 订阅事件，确保列标题始终显示
            dgvStocks.HandleCreated += DgvStocks_HandleCreated;
            dgvStocks.Paint += DgvStocks_Paint;
            dgvStocks.VisibleChanged += DgvStocks_VisibleChanged;
            
            // 初始化时显示空表格（只有列标题，没有数据行）
            // 这样即使没有数据，用户也能看到列名称
            dgvStocks.Rows.Clear();
            
            // 强制刷新显示列标题（多次设置确保生效）
            EnsureColumnHeadersVisible();
            
            // 延迟再次确保（处理可能的异步初始化问题）
            // 注意：不能在构造函数中使用 BeginInvoke，因为句柄还未创建
            // 使用 HandleCreated 事件来延迟执行
            if (dgvStocks != null)
            {
                dgvStocks.HandleCreated += (s, e) =>
                {
                    // 句柄创建后，再次确保列标题可见
                    if (dgvStocks != null && !dgvStocks.IsDisposed)
                    {
                        EnsureColumnHeadersVisible();
                    }
                };
            }
        }
        
        /// <summary>
        /// 确保列标题可见（提取为独立方法，便于重用）
        /// </summary>
        private void EnsureColumnHeadersVisible()
        {
            if (dgvStocks == null || dgvStocks.IsDisposed)
            {
                Logger.Instance.Warning("DataGridView为null或已释放，无法确保列标题可见");
                return;
            }
            
            try
            {
                // 关键步骤1：强制设置列标题可见和高度（必须在最前面）
                dgvStocks.ColumnHeadersVisible = true;
                dgvStocks.ColumnHeadersHeight = 28;  // 确保列标题高度不为0（关键！）
                dgvStocks.EnableHeadersVisualStyles = false;  // 禁用系统样式，使用自定义样式
                dgvStocks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;  // 禁用自动调整
                
                // 关键步骤2：确保列存在（如果没有列，列标题不会显示）
                if (dgvStocks.Columns.Count == 0)
                {
                    Logger.Instance.Warning("DataGridView没有列，重新初始化列");
                    InitializeDataGridViewColumns();
                }
                
                // 关键步骤3：确保每列的标题样式都正确设置
                foreach (DataGridViewColumn col in dgvStocks.Columns)
                {
                    if (col != null && col.HeaderCell != null)
                    {
                        // 创建新的样式对象（必须创建新对象，不能直接修改）
                        DataGridViewCellStyle headerStyle = new DataGridViewCellStyle();
                        headerStyle.BackColor = Color.FromArgb(50, 50, 50);
                        headerStyle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
                        headerStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        headerStyle.SelectionBackColor = Color.FromArgb(50, 50, 50);
                        headerStyle.WrapMode = DataGridViewTriState.False;  // 不换行
                        
                        // 除了涨幅列用红色，其他列用白色
                        if (col.Name == "ChangePercent")
                        {
                            headerStyle.ForeColor = Color.Red;
                            headerStyle.SelectionForeColor = Color.Red;
                        }
                        else
                        {
                            headerStyle.ForeColor = Color.White;
                            headerStyle.SelectionForeColor = Color.White;
                        }
                        
                        // 应用样式（赋值方式，确保生效）
                        col.HeaderCell.Style = headerStyle;
                        
                        // 确保 HeaderText 不为空
                        if (string.IsNullOrEmpty(col.HeaderText))
                        {
                            // 根据列名设置默认标题
                            switch (col.Name)
                            {
                                case "Index":
                                    col.HeaderText = "序号";
                                    break;
                                case "StockName":
                                    col.HeaderText = "名称";
                                    break;
                                case "ChangePercent":
                                    col.HeaderText = "涨幅";
                                    break;
                            }
                        }
                    }
                }
                
                // 关键步骤4：再次强制设置（双重保险）
                dgvStocks.ColumnHeadersVisible = true;
                dgvStocks.ColumnHeadersHeight = 28;
                dgvStocks.EnableHeadersVisualStyles = false;
                
                // 关键步骤5：强制刷新（使用多种方法确保生效）
                // 注意：不要使用 SuspendLayout/ResumeLayout，这可能会影响数据行的显示
                // .NET Framework 3.5 不支持 InvalidateColumnHeaders()，使用 Invalidate() 刷新整个控件
                dgvStocks.PerformLayout();
                dgvStocks.Invalidate(true);  // 刷新整个控件（包括列标题和数据行）
                dgvStocks.Update();
                
                // 关键步骤6：验证设置是否生效
                if (dgvStocks.ColumnHeadersVisible == false || dgvStocks.ColumnHeadersHeight == 0)
                {
                    Logger.Instance.Warning(string.Format("列标题设置失败: Visible={0}, Height={1}", dgvStocks.ColumnHeadersVisible, dgvStocks.ColumnHeadersHeight));
                    // 再次强制设置
                    dgvStocks.ColumnHeadersVisible = true;
                    dgvStocks.ColumnHeadersHeight = 28;
                }
                
                Logger.Instance.Debug(string.Format("列标题可见性已确保: Visible={0}, Height={1}, 列数={2}", dgvStocks.ColumnHeadersVisible, dgvStocks.ColumnHeadersHeight, dgvStocks.Columns.Count));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("确保列标题可见失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }
        
        /// <summary>
        /// DataGridView HandleCreated 事件处理（控件句柄创建时）
        /// </summary>
        private void DgvStocks_HandleCreated(object sender, EventArgs e)
        {
            EnsureColumnHeadersVisible();
        }
        
        /// <summary>
        /// DataGridView Paint 事件处理（确保列标题始终显示）
        /// </summary>
        private void DgvStocks_Paint(object sender, PaintEventArgs e)
        {
            if (dgvStocks != null && !dgvStocks.IsDisposed)
            {
                // 检查列标题是否被隐藏或高度为0
                if (dgvStocks.ColumnHeadersVisible == false || dgvStocks.ColumnHeadersHeight == 0)
                {
                    // 如果列标题被隐藏或高度为0，立即恢复
                    Logger.Instance.Debug(string.Format("Paint事件检测到列标题问题: Visible={0}, Height={1}，正在修复", dgvStocks.ColumnHeadersVisible, dgvStocks.ColumnHeadersHeight));
                    EnsureColumnHeadersVisible();
                }
            }
        }
        
        /// <summary>
        /// DataGridView VisibleChanged 事件处理
        /// </summary>
        private void DgvStocks_VisibleChanged(object sender, EventArgs e)
        {
            if (dgvStocks != null && dgvStocks.Visible)
            {
                EnsureColumnHeadersVisible();
            }
        }
        
        private void SetupRefreshTimer()
        {
            refreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 2000  // 每2秒刷新一次（减少刷新频率，提高性能）
            };
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
        }
        
        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshData();
        }
        
        /// <summary>
        /// 添加股票代码
        /// </summary>
        public void AddStock(string stockCode)
        {
            try
            {
                if (string.IsNullOrEmpty(stockCode))
                    return;
                
                string normalizedCode = DataConverter.NormalizeStockCode(stockCode);
                
                if (string.IsNullOrEmpty(normalizedCode))
                {
                    Logger.Instance.Warning(string.Format("股票代码标准化失败: {0}", stockCode));
                    return;
                }
                
                if (!stockCodes.Contains(normalizedCode))
                {
                    stockCodes.Add(normalizedCode);
                    
                    // 获取股票名称（用于日志和保存）
                    string stockName = "";
                    if (dataManager != null)
                    {
                        stockName = dataManager.GetStockName(normalizedCode) ?? "";
                    }
                    
                    Logger.Instance.Info(string.Format("板块[{0}]添加股票: {1}", boardName, normalizedCode) + 
                        (!string.IsNullOrEmpty(stockName) ? string.Format(" ({0})", stockName) : ""));
                    
                    // 安全地刷新数据
                    try
                    {
                        RefreshData();
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error(string.Format("刷新数据失败: {0}", ex.Message));
                        Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                    }
                    
                    // 立即触发保存事件（确保股票信息被保存到配置文件）
                    try
                    {
                        OnStockListChanged();  // 触发保存事件，自动保存到配置文件
                        Logger.Instance.Success(string.Format("板块[{0}]股票 {1} 已添加并触发保存", boardName, normalizedCode));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error(string.Format("触发保存事件失败: {0}", ex.Message));
                        Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                    }
                }
                else
                {
                    Logger.Instance.Info(string.Format("股票 {0} 已存在于板块[{1}]中", normalizedCode, boardName));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("添加股票失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                throw;  // 重新抛出异常，让调用者知道发生了错误
            }
        }
        
        /// <summary>
        /// 删除股票代码
        /// </summary>
        public void RemoveStock(string stockCode)
        {
            try
            {
                if (string.IsNullOrEmpty(stockCode))
                    return;
                
                string normalizedCode = DataConverter.NormalizeStockCode(stockCode);
                
                if (string.IsNullOrEmpty(normalizedCode))
                {
                    Logger.Instance.Warning(string.Format("股票代码标准化失败: {0}", stockCode));
                    return;
                }
                
                if (stockCodes.Remove(normalizedCode))
                {
                    // 安全地刷新数据
                    try
                    {
                        RefreshData();
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error(string.Format("刷新数据失败: {0}", ex.Message));
                    }
                    
                    Logger.Instance.Info(string.Format("板块[{0}]删除股票: {1}", boardName, normalizedCode));
                    
                    // 安全地触发保存事件
                    try
                    {
                        OnStockListChanged();  // 触发保存事件
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error(string.Format("触发保存事件失败: {0}", ex.Message));
                    }
                }
                else
                {
                    Logger.Instance.Info(string.Format("股票 {0} 不在板块[{1}]中", normalizedCode, boardName));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("删除股票失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }
        
        /// <summary>
        /// 股票列表改变事件（用于触发保存）
        /// </summary>
        public event EventHandler StockListChanged;
        
        protected virtual void OnStockListChanged()
        {
            try
            {
                // 如果正在加载配置，不触发保存事件（避免覆盖配置文件）
                if (isLoading)
                {
                    Logger.Instance.Debug(string.Format("板块[{0}]正在加载配置，跳过保存事件触发", boardName));
                    return;
                }
                
                Logger.Instance.Info(string.Format("板块[{0}]股票列表已改变，当前股票数量: {1}", boardName, stockCodes.Count));
                if (stockCodes.Count > 0)
                {
                    Logger.Instance.Info(string.Format("板块[{0}]当前股票列表: {1}", boardName, string.Join(", ", stockCodes.Take(10).ToArray()) + 
                        (stockCodes.Count > 10 ? "..." : "")));
                }
                
                if (StockListChanged != null)
                {
                    StockListChanged.Invoke(this, EventArgs.Empty);
                    Logger.Instance.Success(string.Format("板块[{0}]已触发保存事件，股票信息将保存到配置文件", boardName));
                }
                else
                {
                    Logger.Instance.Warning(string.Format("板块[{0}]的StockListChanged事件未订阅，无法自动保存", boardName));
                    Logger.Instance.Warning("提示：请确保在Form1中订阅了StockListChanged事件");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("触发股票列表改变事件失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }
        
        /// <summary>
        /// 设置股票代码列表（用于加载配置，不会触发保存事件）
        /// </summary>
        public void SetStockCodes(List<string> codes)
        {
            SetStockCodes(codes, false);  // 默认不触发保存事件
        }
        
        /// <summary>
        /// 设置股票代码列表（重载方法，可控制是否触发保存）
        /// </summary>
        /// <param name="codes">股票代码列表</param>
        /// <param name="triggerSave">是否触发保存事件（true=用户操作，false=加载数据）</param>
        public void SetStockCodes(List<string> codes, bool triggerSave)
        {
            try
            {
                // 设置加载标志
                bool wasLoading = isLoading;
                isLoading = !triggerSave;  // 如果不是用户操作，则标记为加载中
                
                Logger.Instance.Info(string.Format("板块[{0}]开始设置股票代码: 输入数量={1}", boardName, codes != null ? codes.Count : 0));
                if (codes != null && codes.Count > 0)
                {
                    Logger.Instance.Debug(string.Format("板块[{0}]输入股票代码列表: {1}", boardName, string.Join(", ", codes.Take(10).ToArray()) + (codes.Count > 10 ? "..." : "")));
                }
                
                stockCodes.Clear();
                if (codes != null && codes.Count > 0)
                {
                    int addedCount = 0;
                    int skippedCount = 0;
                    foreach (var code in codes)
                    {
                        if (string.IsNullOrEmpty(code))
                        {
                            skippedCount++;
                            Logger.Instance.Debug(string.Format("板块[{0}]跳过空股票代码", boardName));
                            continue;
                        }
                            
                        string normalizedCode = DataConverter.NormalizeStockCode(code);
                        Logger.Instance.Debug(string.Format("板块[{0}]标准化股票代码: [{1}] -> [{2}]", boardName, code, normalizedCode));
                        
                        if (!string.IsNullOrEmpty(normalizedCode) && !stockCodes.Contains(normalizedCode))
                        {
                            stockCodes.Add(normalizedCode);
                            addedCount++;
                            Logger.Instance.Debug(string.Format("板块[{0}]成功添加股票代码: [{1}]", boardName, normalizedCode));
                        }
                        else
                        {
                            skippedCount++;
                            if (string.IsNullOrEmpty(normalizedCode))
                            {
                                Logger.Instance.Warning(string.Format("板块[{0}]标准化后代码为空，原始代码: [{1}]", boardName, code));
                            }
                            else if (stockCodes.Contains(normalizedCode))
                            {
                                Logger.Instance.Debug(string.Format("板块[{0}]股票代码已存在，跳过: [{1}]", boardName, normalizedCode));
                            }
                        }
                    }
                    Logger.Instance.Info(string.Format("板块[{0}]已设置 {1} 只股票代码（从 {2} 个代码中，跳过 {3} 个）", boardName, addedCount, codes.Count, skippedCount) + 
                        (triggerSave ? " [用户操作]" : " [加载配置]"));
                    if (addedCount > 0)
                    {
                        Logger.Instance.Info(string.Format("板块[{0}]最终股票代码列表: {1}", boardName, string.Join(", ", stockCodes.ToArray())));
                    }
                    else
                    {
                        Logger.Instance.Warning(string.Format("板块[{0}]警告: 输入了 {1} 个代码，但最终没有添加任何股票代码！", boardName, codes.Count));
                    }
                }
                else
                {
                    Logger.Instance.Info(string.Format("板块[{0}]设置股票代码列表为空（codes为null或空）", boardName) + 
                        (triggerSave ? " [用户操作]" : " [加载配置]"));
                }
                
                RefreshData();
                
                // 恢复加载标志
                isLoading = wasLoading;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("设置股票代码列表失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                isLoading = false;  // 确保异常时恢复标志
            }
        }

        /// <summary>
        /// 设置筛选条件
        /// </summary>
        /// <param name="settings">筛选设置（如果为null则清除筛选）</param>
        public void SetFilterSettings(FilterSettings settings)
        {
            try
            {
                if (settings == null)
                {
                    filterSettings = null;
                    Logger.Instance.Info(string.Format("板块[{0}]已清除筛选条件", boardName));
                }
                else
                {
                    // 克隆设置以避免引用问题
                    filterSettings = settings.Clone();
                    Logger.Instance.Info(string.Format(
                        "板块[{0}]已设置筛选条件 - 涨幅筛选: {1}({2}%), 日内涨幅筛选: {3}({4}%)",
                        boardName,
                        filterSettings.EnableChangePercentFilter,
                        filterSettings.MinChangePercent,
                        filterSettings.EnableIntradayChangeFilter,
                        filterSettings.MinIntradayChangePercent
                    ));
                }

                // 立即刷新数据以应用筛选
                RefreshData();
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("设置筛选条件失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 刷新数据显示
        /// </summary>
        private void RefreshData()
        {
            if (dgvStocks == null || dataManager == null)
            {
                Logger.Instance.Warning(string.Format("板块[{0}]刷新数据失败: dgvStocks={1}, dataManager={2}", boardName, dgvStocks, dataManager));
                return;
            }
                
            try
            {
                // 使用对象池减少GC压力
                var displayItems = new List<StockDisplayItem>();
                
                foreach (var code in stockCodes)
                {
                    var stockData = dataManager.GetStockData(code);
                    var codeDict = dataManager.GetStockCodeDictionary();
                    string name = codeDict.ContainsKey(code) ? codeDict[code] : "";

                    // 应用筛选条件
                    if (filterSettings != null && stockData != null)
                    {
                        if (!filterSettings.PassFilter(stockData))
                        {
                            continue;
                        }
                    }

                    // 从对象池获取对象，而不是创建新对象
                    var item = StockDisplayItemPool.Get();
                    item.StockCode = code;
                    item.StockName = name;
                    item.NewPrice = stockData != null ? stockData.NewPrice : 0;
                    item.ChangePercent = stockData != null ? stockData.ChangePercent : 0;

                    displayItems.Add(item);
                }
                
                // 仅在记录数变化时记录日志（减少日志输出）
                // Logger.Instance.Info(string.Format("板块[{0}]刷新数据完成: 共 {1} 条记录待显示", boardName, displayItems.Count));
                
                // 按涨跌幅排序（降序：涨幅高的排在前面）
                // 如果涨幅相同，则按股票代码排序（确保排序稳定）
                if (displayItems.Count > 1)
                {
                    displayItems = displayItems
                        .OrderByDescending(x => x.ChangePercent)
                        .ThenBy(x => x.StockCode)  // 涨幅相同时按代码排序，确保排序稳定
                        .ToList();
                }
                
                // 更新表格（使用自定义方法以添加序号）
                // 检查句柄是否已创建，如果已创建则检查是否需要Invoke
                if (this.IsHandleCreated && this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        UpdateDataGridView(displayItems);
                    }));
                }
                else
                {
                    // 句柄未创建或已在UI线程，直接调用
                    UpdateDataGridView(displayItems);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("刷新板块数据失败: {0}", ex.Message));
            }
        }
        
        /// <summary>
        /// 初始化DataGridView列（提取为独立方法，便于重用）
        /// </summary>
        private void InitializeDataGridViewColumns()
        {
            if (dgvStocks == null)
                return;
            
            // 如果列已存在，先清除
            if (dgvStocks.Columns.Count > 0)
            {
                dgvStocks.Columns.Clear();
            }
            
            // 确保列标题可见和高度（在添加列之前设置）
            // 按照正确的顺序设置：先高度，后样式，最后模式
            dgvStocks.ColumnHeadersVisible = true;
            dgvStocks.ColumnHeadersHeight = 28;
            dgvStocks.EnableHeadersVisualStyles = false;
            dgvStocks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // 第一列：序号（自动编号）
            DataGridViewTextBoxColumn colIndex = new DataGridViewTextBoxColumn
            {
                Name = "Index",
                HeaderText = "序号",
                Width = 40,  // 从45减小到40，节省空间
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                MinimumWidth = 35  // 从40减小到35
            };
            colIndex.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colIndex.DefaultCellStyle.ForeColor = Color.FromArgb(180, 180, 180);  // 灰色序号
            colIndex.DefaultCellStyle.Font = new Font("Microsoft YaHei", 8);
            // 设置列标题样式（创建新样式对象）
            DataGridViewCellStyle indexHeaderStyle = new DataGridViewCellStyle();
            indexHeaderStyle.BackColor = Color.FromArgb(50, 50, 50);
            indexHeaderStyle.ForeColor = Color.White;
            indexHeaderStyle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
            indexHeaderStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            indexHeaderStyle.SelectionBackColor = Color.FromArgb(50, 50, 50);
            indexHeaderStyle.SelectionForeColor = Color.White;
            colIndex.HeaderCell.Style = indexHeaderStyle;
            dgvStocks.Columns.Add(colIndex);
            
            // 第二列：名称（股票名称）
            // 由于股票名称已限制为4个字符以内，减小列宽以确保涨幅列可见
            DataGridViewTextBoxColumn colName = new DataGridViewTextBoxColumn
            {
                Name = "StockName",
                HeaderText = "名称",
                Width = 60,  // 从140减小到60，因为名称最多4个字符
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                MinimumWidth = 50  // 从100减小到50
            };
            colName.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            colName.DefaultCellStyle.Padding = new Padding(3, 0, 0, 0);  // 减小左边距，从5改为3，节省空间
            // 设置列标题样式（创建新样式对象）
            DataGridViewCellStyle nameHeaderStyle = new DataGridViewCellStyle();
            nameHeaderStyle.BackColor = Color.FromArgb(50, 50, 50);
            nameHeaderStyle.ForeColor = Color.White;
            nameHeaderStyle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
            nameHeaderStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nameHeaderStyle.SelectionBackColor = Color.FromArgb(50, 50, 50);
            nameHeaderStyle.SelectionForeColor = Color.White;
            colName.HeaderCell.Style = nameHeaderStyle;
            dgvStocks.Columns.Add(colName);
            
            // 第三列：涨幅（涨跌幅）
            DataGridViewTextBoxColumn colChange = new DataGridViewTextBoxColumn
            {
                Name = "ChangePercent",
                HeaderText = "涨幅",
                Width = 70,  // 从75稍微减小到70，确保在面板内可见
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                MinimumWidth = 60
            };
            colChange.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colChange.DefaultCellStyle.Padding = new Padding(0, 0, 8, 0);  // 右边距
            colChange.DefaultCellStyle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);  // 加粗显示
            // 注意：默认颜色设为红色，但实际会根据涨跌幅动态设置（正数红色，负数绿色，0白色）
            colChange.DefaultCellStyle.ForeColor = Color.Red;  // 默认用红色
            // 表头用红色（创建新样式对象）
            DataGridViewCellStyle changeHeaderStyle = new DataGridViewCellStyle();
            changeHeaderStyle.BackColor = Color.FromArgb(50, 50, 50);
            changeHeaderStyle.ForeColor = Color.Red;
            changeHeaderStyle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
            changeHeaderStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            changeHeaderStyle.SelectionBackColor = Color.FromArgb(50, 50, 50);
            changeHeaderStyle.SelectionForeColor = Color.Red;
            colChange.HeaderCell.Style = changeHeaderStyle;
            dgvStocks.Columns.Add(colChange);
            
            // 再次确保列标题可见和高度（添加列之后）
            // 按照正确的顺序设置：先高度，后样式，最后模式
            dgvStocks.ColumnHeadersVisible = true;
            dgvStocks.ColumnHeadersHeight = 28;
            dgvStocks.EnableHeadersVisualStyles = false;
            dgvStocks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        }
        
        /// <summary>
        /// 更新DataGridView数据（添加序号列）
        /// </summary>
        private void UpdateDataGridView(List<StockDisplayItem> displayItems)
        {
            try
            {
                if (dgvStocks == null)
                {
                    // 使用 StringBuilder 优化字符串拼接
                    StringBuilder sb = new StringBuilder("板块[");
                    sb.Append(boardName);
                    sb.Append("] DataGridView未初始化，无法更新数据");
                    Logger.Instance.Warning(sb.ToString());
                    return;
                }
                
                // 根据数据量决定是否使用虚拟模式（超过100条时启用）
                bool shouldUseVirtualMode = displayItems != null && displayItems.Count > 100;
                if (shouldUseVirtualMode != useVirtualMode)
                {
                    useVirtualMode = shouldUseVirtualMode;
                    dgvStocks.VirtualMode = useVirtualMode;
                    
                    if (useVirtualMode)
                    {
                        // 设置虚拟模式事件处理
                        dgvStocks.CellValueNeeded += DgvStocks_CellValueNeeded;
                        dgvStocks.CellFormatting += DgvStocks_CellFormatting;
                        virtualModeData = new List<StockDisplayItem>(displayItems);
                        dgvStocks.RowCount = virtualModeData.Count;
                    }
                    else
                    {
                        // 取消虚拟模式事件处理
                        dgvStocks.CellValueNeeded -= DgvStocks_CellValueNeeded;
                        dgvStocks.CellFormatting -= DgvStocks_CellFormatting;
                        virtualModeData.Clear();
                    }
                }
                
                // 如果使用虚拟模式，只更新数据源，不手动添加行
                if (useVirtualMode)
                {
                    // 先返回旧对象到对象池
                    foreach (var oldItem in virtualModeData)
                    {
                        if (oldItem != null)
                        {
                            StockDisplayItemPool.Return(oldItem);
                        }
                    }
                    
                    // 更新虚拟模式数据源
                    virtualModeData = new List<StockDisplayItem>(displayItems);
                    dgvStocks.RowCount = virtualModeData.Count;
                    dgvStocks.Invalidate();
                    return;
                }
                
                // 减少日志输出（仅在调试模式下记录）
                // Logger.Instance.Debug(string.Format("板块[{0}]开始更新DataGridView: 待显示记录数={1}", boardName, displayItems.Count));
                
                // 保存当前选中的股票代码，以便刷新后恢复选中状态
                string selectedStockCode = null;
                if (dgvStocks.SelectedRows.Count > 0)
                {
                    var selectedRow = dgvStocks.SelectedRows[0];
                    if (selectedRow.Tag is StockDisplayItem selectedItem)
                    {
                        selectedStockCode = selectedItem.StockCode;
                        // Logger.Instance.Debug(string.Format("板块[{0}]保存选中状态: 股票代码={1}", boardName, selectedStockCode));
                    }
                }
                
                // 暂停布局更新，避免闪烁和残影
                dgvStocks.SuspendLayout();
                
                try
                {
                    // 关键：在更新数据之前，确保列标题可见和高度设置正确
                    // 按照正确的顺序设置：先高度，后样式，最后模式
                    dgvStocks.ColumnHeadersVisible = true;
                    dgvStocks.ColumnHeadersHeight = 28;  // 确保列标题高度不为0（这是关键！）
                    dgvStocks.EnableHeadersVisualStyles = false;  // 禁用系统样式，使用自定义样式
                    dgvStocks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;  // 禁止调整大小
                    
                    // 确保列存在，如果不存在则初始化
                    if (dgvStocks.Columns.Count == 0)
                    {
                        StringBuilder sb = new StringBuilder("板块[");
                        sb.Append(boardName);
                        sb.Append("] DataGridView列不存在，初始化列");
                        Logger.Instance.Warning(sb.ToString());
                        InitializeDataGridViewColumns();
                    }
                    
                    // 检查列是否存在，如果不存在则重新初始化列（在清空行之前检查）
                    if (dgvStocks.Columns["Index"] == null || 
                        dgvStocks.Columns["StockName"] == null || 
                        dgvStocks.Columns["ChangePercent"] == null)
                    {
                        StringBuilder sb = new StringBuilder("板块[");
                        sb.Append(boardName);
                        sb.Append("] DataGridView列未初始化，重新初始化列");
                        Logger.Instance.Warning(sb.ToString());
                        InitializeDataGridViewColumns();
                    }
                    
                    // 先清空数据源（但不清除列，保留列标题）
                    // 注意：不要使用 DataSource = null，这会清除列
                    // 只清空行，保留列定义
                    dgvStocks.Rows.Clear();
                    
                    // 强制清除背景，避免残影
                    dgvStocks.Invalidate(true);
                    dgvStocks.Update();  // 立即更新，清除旧内容
                
                // 再次确保列标题显示（清空后可能被重置）
                // 按照正确的顺序设置：先高度，后样式，最后模式
                dgvStocks.ColumnHeadersVisible = true;
                dgvStocks.ColumnHeadersHeight = 28;  // 确保列标题高度不为0（关键！）
                dgvStocks.EnableHeadersVisualStyles = false;  // 禁用系统样式，使用自定义样式
                dgvStocks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;  // 禁止调整大小

                // 强制设置列标题样式（确保每列的标题都正确显示）
                // 使用创建新样式对象的方式，确保样式被应用
                foreach (DataGridViewColumn col in dgvStocks.Columns)
                {
                    if (col != null && col.HeaderCell != null)
                    {
                        // 创建新的样式对象
                        DataGridViewCellStyle headerStyle = new DataGridViewCellStyle();
                        headerStyle.BackColor = Color.FromArgb(50, 50, 50);
                        headerStyle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
                        headerStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        headerStyle.SelectionBackColor = Color.FromArgb(50, 50, 50);
                        
                        // 除了涨幅列用红色，其他列用白色
                        if (col.Name == "ChangePercent")
                        {
                            headerStyle.ForeColor = Color.Red;
                            headerStyle.SelectionForeColor = Color.Red;
                        }
                        else
                        {
                            headerStyle.ForeColor = Color.White;
                            headerStyle.SelectionForeColor = Color.White;
                        }
                        
                        // 应用样式
                        col.HeaderCell.Style = headerStyle;
                    }
                }
                
                    // 手动添加行，包含序号
                    Logger.Instance.Debug(string.Format("板块[{0}]准备添加 {1} 行数据到DataGridView", boardName, displayItems.Count));
                    if (displayItems != null && displayItems.Count > 0)
                {
                    Logger.Instance.Info(string.Format("板块[{0}]开始添加 {1} 行数据到DataGridView", boardName, displayItems.Count));
                    for (int i = 0; i < displayItems.Count; i++)
                    {
                        try
                        {
                            var item = displayItems[i];
                            if (item == null)
                                continue;
                                
                            int rowIndex = dgvStocks.Rows.Add();
                            DataGridViewRow row = dgvStocks.Rows[rowIndex];
                            
                            // 设置序号（从1开始）
                            if (row.Cells["Index"] != null)
                            {
                                row.Cells["Index"].Value = (i + 1).ToString();
                            }
                            
                            // 设置名称（如果名称为空，显示代码的前4个字符）
                            if (row.Cells["StockName"] != null)
                            {
                                string displayName = item.StockName;
                                if (string.IsNullOrEmpty(displayName))
                                {
                                    // 如果名称为空，显示代码的前4个字符（限制在4个字以内）
                                    if (!string.IsNullOrEmpty(item.StockCode))
                                    {
                                        displayName = item.StockCode.Length > 4 ? item.StockCode.Substring(0, 4) : item.StockCode;
                                    }
                                    else
                                    {
                                        displayName = "";
                                    }
                                }
                                else if (displayName.Length > 4)
                                {
                                    // 如果名称本身超过4个字符，也限制为4个字符
                                    displayName = displayName.Substring(0, 4);
                                }
                                row.Cells["StockName"].Value = displayName;
                            }
                            
                            // 设置涨幅（带%符号）- 使用 StringBuilder 优化
                            if (row.Cells["ChangePercent"] != null)
                            {
                                StringBuilder percentSb = new StringBuilder();
                                percentSb.Append(item.ChangePercent.ToString("F2"));
                                percentSb.Append("%");
                                row.Cells["ChangePercent"].Value = percentSb.ToString();
                                
                                // 根据涨跌幅设置颜色：正数为红色，负数为绿色，0为白色
                                // 注意：必须创建新的样式对象，不能直接修改，否则可能不生效
                                DataGridViewCellStyle cellStyle = new DataGridViewCellStyle();
                                cellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                                cellStyle.Padding = new Padding(0, 0, 8, 0);
                                cellStyle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
                                cellStyle.BackColor = Color.Transparent;
                                cellStyle.SelectionBackColor = Color.FromArgb(60, 60, 60);
                                
                                // 根据涨跌幅设置颜色（使用小的阈值避免浮点数精度问题）
                                float changePercent = item.ChangePercent;
                                if (changePercent > 0.0001f)
                                {
                                    // 上涨用红色
                                    cellStyle.ForeColor = Color.Red;
                                    cellStyle.SelectionForeColor = Color.Red;
                                    Logger.Instance.Debug(string.Format("股票 {0} 涨幅 {1:F2}% 设置为红色", item.StockCode, changePercent));
                                }
                                else if (changePercent < -0.0001f)
                                {
                                    // 下跌用绿色
                                    cellStyle.ForeColor = Color.Green;
                                    cellStyle.SelectionForeColor = Color.Green;
                                    Logger.Instance.Debug(string.Format("股票 {0} 涨幅 {1:F2}% 设置为绿色", item.StockCode, changePercent));
                                }
                                else
                                {
                                    // 平盘用白色
                                    cellStyle.ForeColor = Color.White;
                                    cellStyle.SelectionForeColor = Color.White;
                                    Logger.Instance.Debug(string.Format("股票 {0} 涨幅 {1:F2}% 设置为白色", item.StockCode, changePercent));
                                }
                                
                                // 应用样式（使用赋值方式，确保样式被应用）
                                row.Cells["ChangePercent"].Style = cellStyle;
                                
                                // 再次强制设置颜色（双重保险，确保颜色被应用）
                                row.Cells["ChangePercent"].Style.ForeColor = cellStyle.ForeColor;
                                row.Cells["ChangePercent"].Style.SelectionForeColor = cellStyle.SelectionForeColor;
                            }
                            
                            // 存储原始数据用于删除操作
                            row.Tag = item;
                        }
                        catch (Exception ex)
                        {
                            StringBuilder sb = new StringBuilder("添加行数据失败 (索引 ");
                            sb.Append(i);
                            sb.Append("): ");
                            sb.Append(ex.Message);
                            Logger.Instance.Error(sb.ToString());
                        }
                    }
                    
                    // 更新完成后，将对象返回到对象池（但保留引用，因为 Tag 中还在使用）
                    // 注意：这里不能立即返回，因为 row.Tag 还在使用这些对象
                    // 对象会在下次更新时被重用，或者在新对象创建时被回收
                }
                else
                {
                    Logger.Instance.Debug("displayItems为null或空，不添加数据行");
                }
                
                // 无论是否有数据，都强制刷新显示列标题
                // 重要：必须在设置样式之前确保列标题可见
                dgvStocks.ColumnHeadersVisible = true;
                dgvStocks.ColumnHeadersHeight = 28;  // 确保列标题高度不为0（关键！）
                dgvStocks.EnableHeadersVisualStyles = false;  // 禁用系统样式，使用自定义样式
                
                // 确保列标题样式正确（必须在设置列标题可见之后）
                dgvStocks.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(50, 50, 50);
                dgvStocks.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                dgvStocks.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
                dgvStocks.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                
                // 确保每列的标题样式都正确设置（逐个设置，确保生效）
                foreach (DataGridViewColumn col in dgvStocks.Columns)
                {
                    if (col != null && col.HeaderCell != null)
                    {
                        // 创建新的样式对象，确保样式被应用
                        DataGridViewCellStyle headerStyle = new DataGridViewCellStyle();
                        headerStyle.BackColor = Color.FromArgb(50, 50, 50);
                        headerStyle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
                        headerStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        headerStyle.SelectionBackColor = Color.FromArgb(50, 50, 50);
                        
                        // 除了涨幅列用红色，其他列用白色
                        if (col.Name == "ChangePercent")
                        {
                            headerStyle.ForeColor = Color.Red;
                            headerStyle.SelectionForeColor = Color.Red;
                        }
                        else
                        {
                            headerStyle.ForeColor = Color.White;
                            headerStyle.SelectionForeColor = Color.White;
                        }
                        
                        // 应用样式
                        col.HeaderCell.Style = headerStyle;
                    }
                }
                
                    // 在添加完数据后，确保列标题可见
                    // 按照正确的顺序设置：先高度，后样式，最后模式
                    dgvStocks.ColumnHeadersVisible = true;
                    dgvStocks.ColumnHeadersHeight = 28;
                    dgvStocks.EnableHeadersVisualStyles = false;
                    dgvStocks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

                    // 恢复布局更新
                    dgvStocks.ResumeLayout(true);
                    
                    // 恢复选中状态（根据之前保存的股票代码）
                    if (!string.IsNullOrEmpty(selectedStockCode))
                    {
                        foreach (DataGridViewRow row in dgvStocks.Rows)
                        {
                            if (row.Tag is StockDisplayItem item && item.StockCode == selectedStockCode)
                            {
                                dgvStocks.ClearSelection();  // 先清除所有选中
                                row.Selected = true;  // 选中匹配的行
                                dgvStocks.CurrentCell = row.Cells[0];  // 设置当前单元格
                                Logger.Instance.Debug(string.Format("板块[{0}]恢复选中状态: 股票代码={1}, 行索引={2}", boardName, selectedStockCode, row.Index));
                                break;
                            }
                        }
                    }
                    
                    // 强制刷新整个控件，清除残影
                    dgvStocks.Invalidate(true);  // 刷新整个控件（包括列标题和数据行）
                    dgvStocks.Update();  // 立即更新，确保清除所有旧内容
                    
                    // 记录日志，便于调试
                    Logger.Instance.Debug(string.Format("DataGridView更新完成: 列数={0}, 行数={1}, 列标题可见={2}, 列标题高度={3}, 选中行数={4}", 
                        dgvStocks.Columns.Count, dgvStocks.Rows.Count, dgvStocks.ColumnHeadersVisible, dgvStocks.ColumnHeadersHeight, dgvStocks.SelectedRows.Count));
                }
                finally
                {
                    // 确保即使发生异常也恢复布局
                    if (dgvStocks != null)
                    {
                        dgvStocks.ResumeLayout(true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("更新DataGridView失败: {0}", ex.Message));
                Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
            }
        }
        
        private void TxtBoardName_TextChanged(object sender, EventArgs e)
        {
            // 实时更新名称（但不立即保存，等失去焦点时保存）
        }
        
        private void TxtBoardName_LostFocus(object sender, EventArgs e)
        {
            string newName = txtBoardName.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                txtBoardName.Text = boardName;  // 恢复原名称
                return;
            }
            
            // 检查名称是否重复（需要从父窗体获取所有板块名称）
            if (newName != boardName)
            {
                // 通过事件通知父窗体检查名称重复
                bool isNameValid = true;
                if (BoardNameValidating != null)
                {
                    var args = new BoardNameValidatingEventArgs(newName, this);
                    BoardNameValidating.Invoke(this, args);
                    isNameValid = args.IsValid;
                    if (!isNameValid)
                    {
                        // 名称重复，恢复原名称
                        txtBoardName.Text = boardName;
                        MessageBox.Show("板块名称 \"{newName}\" 已存在，请使用其他名称。", "名称重复", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                
                if (isNameValid)
                {
                    string oldName = boardName;
                    boardName = newName;
                    Logger.Instance.Info(string.Format("板块名称已更改: [{0}] -> [{1}]", oldName, newName));
                    OnBoardNameChanged(newName);  // 触发保存事件
                }
            }
        }
        
        private void BtnAdd_Click(object sender, EventArgs e)
        {
            // 使用简单的输入对话框
            Form inputForm = new Form
            {
                Text = "添加股票",
                Size = new Size(300, 120),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(40, 40, 40),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            
            Label lblPrompt = new Label
            {
                Text = "请输入股票代码或拼音首字母：",
                Location = new Point(10, 15),
                Size = new Size(260, 20),
                ForeColor = Color.White
            };
            inputForm.Controls.Add(lblPrompt);
            
            TextBox txtInput = new TextBox
            {
                Location = new Point(10, 40),
                Size = new Size(260, 20),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            inputForm.Controls.Add(txtInput);
            
            // 添加搜索结果列表
            ListBox listResults = new ListBox
            {
                Location = new Point(10, 65),
                Size = new Size(260, 100),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Visible = false
            };
            inputForm.Controls.Add(listResults);
            inputForm.Height = 120;  // 初始高度
            
            // 输入变化时搜索
            txtInput.TextChanged += (s, args) =>
            {
                try
                {
                    string input = txtInput.Text.Trim();
                    if (string.IsNullOrEmpty(input))
                    {
                        listResults.Visible = false;
                        inputForm.Height = 120;
                        return;
                    }
                    
                    // 检查dataManager是否可用
                    if (dataManager == null)
                    {
                        Logger.Instance.Warning("DataManager未初始化，无法搜索股票");
                        return;
                    }
                    
                    // 搜索股票
                    var results = dataManager.SearchStock(input);
                    
                    if (results != null && results.Count > 0)
                    {
                        listResults.Items.Clear();
                        foreach (var code in results.Take(10))  // 最多显示10个结果
                        {
                            try
                            {
                                string name = dataManager.GetStockName(code) ?? "";
                                listResults.Items.Add(string.Format("{0} - {1}", code, name));
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Error(string.Format("获取股票名称失败: {0}, 错误: {1}", code, ex.Message));
                                listResults.Items.Add(string.Format("{0} - (未知)", code));
                            }
                        }
                        listResults.Visible = true;
                        inputForm.Height = 180;  // 增加高度以显示列表
                    }
                    else
                    {
                        listResults.Visible = false;
                        inputForm.Height = 120;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("搜索股票时发生错误: {0}", ex.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                    listResults.Visible = false;
                    inputForm.Height = 120;
                }
            };
            
            // 双击列表项选择
            listResults.DoubleClick += (s, args) =>
            {
                try
                {
                    if (listResults.SelectedIndex >= 0 && listResults.SelectedItem != null)
                    {
                        string selected = listResults.SelectedItem.ToString();
                        if (!string.IsNullOrEmpty(selected) && selected.Contains("-"))
                        {
                            string code = selected.Split('-')[0].Trim();
                            if (!string.IsNullOrEmpty(code))
                            {
                                AddStock(code);
                                inputForm.DialogResult = DialogResult.OK;
                                inputForm.Close();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("双击选择股票时发生错误: {0}", ex.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                    MessageBox.Show(string.Format("选择股票时发生错误: {0}", ex.Message), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            Button btnOK = new Button
            {
                Text = "确定",
                Location = new Point(110, 170),
                Size = new Size(75, 25),
                DialogResult = DialogResult.OK
            };
            inputForm.Controls.Add(btnOK);
            
            inputForm.AcceptButton = btnOK;
            txtInput.Focus();
            
            if (inputForm.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                try
                {
                    string input = txtInput.Text.Trim();
                    if (!string.IsNullOrEmpty(input))
                    {
                        // 如果输入的是列表中的项，提取代码
                        if (listResults.SelectedIndex >= 0 && listResults.SelectedItem != null)
                        {
                            try
                            {
                                string selected = listResults.SelectedItem.ToString();
                                if (!string.IsNullOrEmpty(selected) && selected.Contains("-"))
                                {
                                    input = selected.Split('-')[0].Trim();
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Error(string.Format("解析选中项失败: {0}", ex.Message));
                            }
                        }
                        
                        // 检查dataManager是否可用
                        if (dataManager == null)
                        {
                            Logger.Instance.Error("DataManager未初始化，无法添加股票");
                            MessageBox.Show("数据管理器未初始化，无法添加股票", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        
                        // 搜索匹配的股票代码
                        var results = dataManager.SearchStock(input);
                        if (results != null && results.Count > 0)
                        {
                            // 使用第一个匹配结果
                            AddStock(results[0]);
                        }
                        else
                        {
                            // 如果没有搜索结果，尝试直接添加（可能是新代码）
                            AddStock(input);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(string.Format("添加股票时发生错误: {0}", ex.Message));
                    Logger.Instance.Error(string.Format("异常堆栈: {0}", ex.StackTrace));
                    MessageBox.Show(string.Format("添加股票时发生错误: {0}", ex.Message), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            
            inputForm.Dispose();
        }
        
        private void BtnDelete_Click(object sender, EventArgs e)
        {
            // 临时暂停刷新定时器，避免在删除过程中数据刷新导致选中状态丢失
            bool timerWasRunning = false;
            if (refreshTimer != null && refreshTimer.Enabled)
            {
                timerWasRunning = true;
                refreshTimer.Stop();
                Logger.Instance.Debug(string.Format("板块[{0}]删除操作：暂停刷新定时器", boardName));
            }
            
            try
            {
                if (dgvStocks.SelectedRows.Count > 0)
                {
                    var selectedRow = dgvStocks.SelectedRows[0];
                    // 从Tag中获取原始数据
                    StockDisplayItem item = selectedRow.Tag as StockDisplayItem;
                    if (item != null)
                    {
                        // 确认删除对话框
                        DialogResult result = MessageBox.Show(
                            string.Format("确定要删除股票 [{0}] 吗？", item.StockName ?? item.StockCode),
                            "确认删除",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                        
                        if (result == DialogResult.Yes)
                        {
                            RemoveStock(item.StockCode);
                            Logger.Instance.Info(string.Format("板块[{0}]用户确认删除股票: {1}", boardName, item.StockCode));
                        }
                    }
                    else
                    {
                        MessageBox.Show("无法获取选中行的股票信息", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("请先选择要删除的股票", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                // 恢复刷新定时器
                if (timerWasRunning && refreshTimer != null)
                {
                    refreshTimer.Start();
                    Logger.Instance.Debug(string.Format("板块[{0}]删除操作完成：恢复刷新定时器", boardName));
                }
            }
        }

        /// <summary>
        /// 删除板块按钮点击事件
        /// </summary>
        private void BtnDeleteBoard_Click(object sender, EventArgs e)
        {
            // 弹出确认对话框
            DialogResult result = MessageBox.Show(
                string.Format("确定要删除板块 [{0}] 吗？", boardName),
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                // 触发删除板块请求事件
                DeleteBoardRequested.Invoke(this, EventArgs.Empty);
            }
        }

        protected virtual void OnBoardNameChanged(string newName)
        {
            // 如果正在加载配置，不触发保存事件（避免覆盖配置文件）
            if (isLoading)
            {
                Logger.Instance.Debug(string.Format("板块名称改变（加载模式）: [{0}]，跳过保存事件触发", newName));
                return;
            }
            
            Logger.Instance.Info(string.Format("板块名称已改变: [{0}]，触发保存事件", newName));
            BoardNameChanged.Invoke(this, new BoardNameChangedEventArgs(newName));        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (refreshTimer != null)
                {
                    refreshTimer.Stop();
                    refreshTimer.Dispose();
                    refreshTimer = null;
                }
                
                // 取消ControlAdded事件订阅（防止内存泄漏）
                if (controlAddedHandler != null)
                {
                    this.ControlAdded -= controlAddedHandler;
                    controlAddedHandler = null;
                }
                
                // 清理 DataGridView 中使用的对象，返回到对象池
                if (dgvStocks != null && !dgvStocks.IsDisposed)
                {
                    // 取消虚拟模式事件
                    if (useVirtualMode)
                    {
                        dgvStocks.CellValueNeeded -= DgvStocks_CellValueNeeded;
                        dgvStocks.CellFormatting -= DgvStocks_CellFormatting;
                    }
                    
                    // 返回普通模式下的对象
                    foreach (DataGridViewRow row in dgvStocks.Rows)
                    {
                        if (row.Tag is StockDisplayItem item)
                        {
                            StockDisplayItemPool.Return(item);
                            row.Tag = null;
                        }
                    }
                    
                    // 返回虚拟模式下的对象
                    if (virtualModeData != null)
                    {
                        foreach (var item in virtualModeData)
                        {
                            if (item != null)
                            {
                                StockDisplayItemPool.Return(item);
                            }
                        }
                        virtualModeData.Clear();
                    }
                }
                
                // 取消Resize事件订阅
                this.Resize -= StockBoardPanel_Resize;
            }
            base.Dispose(disposing);
        }
        
        /// <summary>
        /// 虚拟模式：CellValueNeeded 事件处理（仅在虚拟模式下使用）
        /// </summary>
        private void DgvStocks_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (!useVirtualMode || virtualModeData == null || e.RowIndex < 0 || e.RowIndex >= virtualModeData.Count)
                return;
                
            if (dgvStocks == null || e.ColumnIndex < 0 || e.ColumnIndex >= dgvStocks.Columns.Count)
                return;
                
            var item = virtualModeData[e.RowIndex];
            if (item == null)
                return;
                
            // 通过 ColumnIndex 获取列名
            string columnName = dgvStocks.Columns[e.ColumnIndex].Name;
                
            try
            {
                switch (columnName)
                {
                    case "Index":
                        e.Value = (e.RowIndex + 1).ToString();
                        break;
                    case "StockName":
                        string displayName = item.StockName;
                        if (string.IsNullOrEmpty(displayName))
                        {
                            if (!string.IsNullOrEmpty(item.StockCode))
                            {
                                displayName = item.StockCode.Length > 4 ? item.StockCode.Substring(0, 4) : item.StockCode;
                            }
                            else
                            {
                                displayName = "";
                            }
                        }
                        else if (displayName.Length > 4)
                        {
                            displayName = displayName.Substring(0, 4);
                        }
                        e.Value = displayName;
                        break;
                    case "ChangePercent":
                        StringBuilder percentSb = new StringBuilder();
                        percentSb.Append(item.ChangePercent.ToString("F2"));
                        percentSb.Append("%");
                        e.Value = percentSb.ToString();
                        break;
                }
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder("虚拟模式获取单元格值失败: ");
                sb.Append(ex.Message);
                Logger.Instance.Error(sb.ToString());
            }
        }
        
        /// <summary>
        /// 虚拟模式：CellFormatting 事件处理（设置单元格样式，特别是涨幅列的颜色）
        /// </summary>
        private void DgvStocks_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (!useVirtualMode || virtualModeData == null || e.RowIndex < 0 || e.RowIndex >= virtualModeData.Count)
                return;
                
            if (dgvStocks == null || e.ColumnIndex < 0 || e.ColumnIndex >= dgvStocks.Columns.Count)
                return;
                
            // 通过 ColumnIndex 获取列名
            string columnName = dgvStocks.Columns[e.ColumnIndex].Name;
                
            if (columnName == "ChangePercent")
            {
                var item = virtualModeData[e.RowIndex];
                if (item != null)
                {
                    float changePercent = item.ChangePercent;
                    if (changePercent > 0.0001f)
                    {
                        e.CellStyle.ForeColor = Color.Red;
                        e.CellStyle.SelectionForeColor = Color.Red;
                    }
                    else if (changePercent < -0.0001f)
                    {
                        e.CellStyle.ForeColor = Color.Green;
                        e.CellStyle.SelectionForeColor = Color.Green;
                    }
                    else
                    {
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.SelectionForeColor = Color.White;
                    }
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    e.CellStyle.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
                }
            }
        }
    }
    
    /// <summary>
    /// 股票显示项（用于板块面板）
    /// </summary>
    public class StockDisplayItem
    {
        private string _stockCode;
        public string StockCode { get { return _stockCode; } set { _stockCode = value; } }

        private string _stockName;
        public string StockName { get { return _stockName; } set { _stockName = value; } }

        private float _newPrice;
        public float NewPrice { get { return _newPrice; } set { _newPrice = value; } }

        private float _changePercent;
        public float ChangePercent { get { return _changePercent; } set { _changePercent = value; } }
        
        /// <summary>
        /// 重置对象以便重用
        /// </summary>
        public void Reset()
        {
            _stockCode = null;
            _stockName = null;
            _newPrice = 0;
            _changePercent = 0;
        }
    }
    
    /// <summary>
    /// StockDisplayItem 对象池（减少GC压力，提高性能）
    /// </summary>
    public static class StockDisplayItemPool
    {
        private static readonly Queue<StockDisplayItem> pool = new Queue<StockDisplayItem>();
        private static readonly object poolLock = new object();
        private const int MaxPoolSize = 100;  // 最大池大小
        
        /// <summary>
        /// 从对象池获取一个对象
        /// </summary>
        public static StockDisplayItem Get()
        {
            lock (poolLock)
            {
                if (pool.Count > 0)
                {
                    return pool.Dequeue();
                }
            }
            return new StockDisplayItem();
        }
        
        /// <summary>
        /// 将对象返回到对象池
        /// </summary>
        public static void Return(StockDisplayItem item)
        {
            if (item == null) return;
            
            item.Reset();
            
            lock (poolLock)
            {
                if (pool.Count < MaxPoolSize)
                {
                    pool.Enqueue(item);
                }
            }
        }
        
        /// <summary>
        /// 清理对象池
        /// </summary>
        public static void Clear()
        {
            lock (poolLock)
            {
                pool.Clear();
            }
        }
    }
}

