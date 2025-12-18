using System;
using System.Drawing;
using System.Windows.Forms;

namespace HaiLiDrvDemo
{
    /// <summary>
    /// 筛选条件设置对话框
    /// </summary>
    public class FilterSettingsDialog : Form
    {
        private CheckBox chkEnableChangePercent;
        private NumericUpDown nudMinChangePercent;
        private Label lblChangePercent;

        private CheckBox chkEnableIntradayChange;
        private NumericUpDown nudMinIntradayChange;
        private Label lblIntradayChange;

        private Button btnOK;
        private Button btnCancel;
        private Button btnReset;

        private FilterSettings filterSettings;
        public FilterSettings FilterSettings
        {
            get { return filterSettings; }
            private set { filterSettings = value; }
        }

        public FilterSettingsDialog(FilterSettings settings)
        {
            FilterSettings = settings.Clone();
            InitializeComponents();
            LoadSettings();
        }

        private void InitializeComponents()
        {
            this.Text = "数据筛选设置";
            this.Size = new Size(450, 250);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(45, 45, 45);
            this.ForeColor = Color.White;

            // 涨幅筛选
            chkEnableChangePercent = new CheckBox();
            chkEnableChangePercent.Text = "启用涨幅筛选";
            chkEnableChangePercent.Location = new Point(20, 20);
            chkEnableChangePercent.Size = new Size(150, 24);
            chkEnableChangePercent.ForeColor = Color.White;
            chkEnableChangePercent.Checked = FilterSettings.EnableChangePercentFilter;
            chkEnableChangePercent.CheckedChanged += ChkEnableChangePercent_CheckedChanged;

            lblChangePercent = new Label();
            lblChangePercent.Text = "最小涨幅（%）：";
            lblChangePercent.Location = new Point(40, 50);
            lblChangePercent.Size = new Size(120, 24);
            lblChangePercent.TextAlign = ContentAlignment.MiddleLeft;
            lblChangePercent.ForeColor = Color.White;

            nudMinChangePercent = new NumericUpDown();
            nudMinChangePercent.Location = new Point(160, 50);
            nudMinChangePercent.Size = new Size(100, 24);
            nudMinChangePercent.Minimum = -100;
            nudMinChangePercent.Maximum = 1000;
            nudMinChangePercent.DecimalPlaces = 2;
            nudMinChangePercent.Increment = 0.5m;
            nudMinChangePercent.Value = FilterSettings.MinChangePercent;
            nudMinChangePercent.BackColor = Color.FromArgb(60, 60, 60);
            nudMinChangePercent.ForeColor = Color.White;

            // 日内涨幅筛选
            chkEnableIntradayChange = new CheckBox();
            chkEnableIntradayChange.Text = "启用日内涨幅筛选（现价相对开盘价）";
            chkEnableIntradayChange.Location = new Point(20, 90);
            chkEnableIntradayChange.Size = new Size(300, 24);
            chkEnableIntradayChange.ForeColor = Color.White;
            chkEnableIntradayChange.Checked = FilterSettings.EnableIntradayChangeFilter;
            chkEnableIntradayChange.CheckedChanged += ChkEnableIntradayChange_CheckedChanged;

            lblIntradayChange = new Label();
            lblIntradayChange.Text = "最小日内涨幅（%）：";
            lblIntradayChange.Location = new Point(40, 120);
            lblIntradayChange.Size = new Size(140, 24);
            lblIntradayChange.TextAlign = ContentAlignment.MiddleLeft;
            lblIntradayChange.ForeColor = Color.White;

            nudMinIntradayChange = new NumericUpDown();
            nudMinIntradayChange.Location = new Point(180, 120);
            nudMinIntradayChange.Size = new Size(100, 24);
            nudMinIntradayChange.Minimum = -100;
            nudMinIntradayChange.Maximum = 1000;
            nudMinIntradayChange.DecimalPlaces = 2;
            nudMinIntradayChange.Increment = 0.5m;
            nudMinIntradayChange.Value = FilterSettings.MinIntradayChangePercent;
            nudMinIntradayChange.BackColor = Color.FromArgb(60, 60, 60);
            nudMinIntradayChange.ForeColor = Color.White;

            // 按钮
            btnOK = new Button();
            btnOK.Text = "确定";
            btnOK.Location = new Point(150, 170);
            btnOK.Size = new Size(80, 30);
            btnOK.BackColor = Color.FromArgb(0, 120, 215);
            btnOK.ForeColor = Color.White;
            btnOK.FlatStyle = FlatStyle.Flat;
            btnOK.DialogResult = DialogResult.OK;
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Location = new Point(240, 170);
            btnCancel.Size = new Size(80, 30);
            btnCancel.BackColor = Color.FromArgb(80, 80, 80);
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.DialogResult = DialogResult.Cancel;

            btnReset = new Button();
            btnReset.Text = "重置";
            btnReset.Location = new Point(330, 170);
            btnReset.Size = new Size(80, 30);
            btnReset.BackColor = Color.FromArgb(80, 80, 80);
            btnReset.ForeColor = Color.White;
            btnReset.FlatStyle = FlatStyle.Flat;
            btnReset.Click += BtnReset_Click;

            // 添加控件
            this.Controls.Add(chkEnableChangePercent);
            this.Controls.Add(lblChangePercent);
            this.Controls.Add(nudMinChangePercent);
            this.Controls.Add(chkEnableIntradayChange);
            this.Controls.Add(lblIntradayChange);
            this.Controls.Add(nudMinIntradayChange);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
            this.Controls.Add(btnReset);

            UpdateControlStates();
        }

        private void LoadSettings()
        {
            chkEnableChangePercent.Checked = FilterSettings.EnableChangePercentFilter;
            nudMinChangePercent.Value = FilterSettings.MinChangePercent;
            chkEnableIntradayChange.Checked = FilterSettings.EnableIntradayChangeFilter;
            nudMinIntradayChange.Value = FilterSettings.MinIntradayChangePercent;
        }

        private void ChkEnableChangePercent_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControlStates();
        }

        private void ChkEnableIntradayChange_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControlStates();
        }

        private void UpdateControlStates()
        {
            lblChangePercent.Enabled = chkEnableChangePercent.Checked;
            nudMinChangePercent.Enabled = chkEnableChangePercent.Checked;

            lblIntradayChange.Enabled = chkEnableIntradayChange.Checked;
            nudMinIntradayChange.Enabled = chkEnableIntradayChange.Checked;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            // 保存设置
            FilterSettings.EnableChangePercentFilter = chkEnableChangePercent.Checked;
            FilterSettings.MinChangePercent = nudMinChangePercent.Value;
            FilterSettings.EnableIntradayChangeFilter = chkEnableIntradayChange.Checked;
            FilterSettings.MinIntradayChangePercent = nudMinIntradayChange.Value;
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            // 重置为默认值
            chkEnableChangePercent.Checked = false;
            nudMinChangePercent.Value = 3.0m;
            chkEnableIntradayChange.Checked = false;
            nudMinIntradayChange.Value = 5.0m;
        }
    }
}
