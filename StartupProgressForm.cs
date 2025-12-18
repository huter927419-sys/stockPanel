using System;
using System.Drawing;
using System.Windows.Forms;

namespace HaiLiDrvDemo
{
    /// <summary>
    /// 启动进度窗口
    /// </summary>
    public class StartupProgressForm : Form
    {
        private ProgressBar progressBar;
        private Label lblStatus;
        private Label lblProgress;
        
        public StartupProgressForm()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "系统启动中...";
            this.Size = new Size(500, 180);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false;
            this.BackColor = Color.FromArgb(40, 40, 40);
            this.ForeColor = Color.White;
            
            // 状态标签
            lblStatus = new Label();
            lblStatus.Text = "正在初始化系统...";
            lblStatus.Location = new Point(20, 20);
            lblStatus.Size = new Size(460, 25);
            lblStatus.ForeColor = Color.White;
            lblStatus.Font = new Font("Microsoft YaHei", 10, FontStyle.Bold);
            lblStatus.BackColor = Color.Transparent;
            this.Controls.Add(lblStatus);

            // 进度条
            progressBar = new ProgressBar();
            progressBar.Location = new Point(20, 60);
            progressBar.Size = new Size(460, 30);
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            this.Controls.Add(progressBar);

            // 进度百分比标签
            lblProgress = new Label();
            lblProgress.Text = "0%";
            lblProgress.Location = new Point(20, 100);
            lblProgress.Size = new Size(460, 20);
            lblProgress.ForeColor = Color.FromArgb(180, 180, 180);
            lblProgress.Font = new Font("Microsoft YaHei", 9);
            lblProgress.TextAlign = ContentAlignment.MiddleCenter;
            lblProgress.BackColor = Color.Transparent;
            this.Controls.Add(lblProgress);

            // 提示标签
            Label lblTip = new Label();
            lblTip.Text = "请稍候，系统正在启动并加载数据...";
            lblTip.Location = new Point(20, 130);
            lblTip.Size = new Size(460, 20);
            lblTip.ForeColor = Color.FromArgb(150, 150, 150);
            lblTip.Font = new Font("Microsoft YaHei", 8);
            lblTip.TextAlign = ContentAlignment.MiddleCenter;
            lblTip.BackColor = Color.Transparent;
            this.Controls.Add(lblTip);
        }
        
        /// <summary>
        /// 更新进度
        /// </summary>
        public void UpdateProgress(int value, string status)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<int, string>(UpdateProgress), value, status);
                return;
            }
            
            progressBar.Value = Math.Max(0, Math.Min(100, value));
            lblProgress.Text = string.Format("{0}%", progressBar.Value);
            lblStatus.Text = status;
            
            // 强制刷新
            this.Refresh();
            Application.DoEvents();
        }
        
        /// <summary>
        /// 设置进度（0-100）
        /// </summary>
        public void SetProgress(int value)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<int>(SetProgress), value);
                return;
            }
            
            progressBar.Value = Math.Max(0, Math.Min(100, value));
            lblProgress.Text = string.Format("{0}%", progressBar.Value);
            this.Refresh();
            Application.DoEvents();
        }
        
        /// <summary>
        /// 设置状态文本
        /// </summary>
        public void SetStatus(string status)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(SetStatus), status);
                return;
            }
            
            lblStatus.Text = status;
            this.Refresh();
            Application.DoEvents();
        }
    }
}
