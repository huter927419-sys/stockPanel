using System;
using System.Drawing;
using System.Windows.Forms;

namespace HaiLiDrvDemo
{
    /// <summary>
    /// 主题管理器 - 负责管理应用程序的主题颜色
    /// </summary>
    public class ThemeManager
    {
        /// <summary>
        /// 主题类型枚举
        /// </summary>
        public enum ThemeType
        {
            Dark,   // 深色主题
            Light   // 浅色主题
        }

        /// <summary>
        /// 主题配色方案
        /// </summary>
        public class ThemeColors
        {
            private Color _backgroundPrimary;
            public Color BackgroundPrimary { get { return _backgroundPrimary; } set { _backgroundPrimary = value; } }

            private Color _backgroundSecondary;
            public Color BackgroundSecondary { get { return _backgroundSecondary; } set { _backgroundSecondary = value; } }

            private Color _backgroundTertiary;
            public Color BackgroundTertiary { get { return _backgroundTertiary; } set { _backgroundTertiary = value; } }

            private Color _foregroundPrimary;
            public Color ForegroundPrimary { get { return _foregroundPrimary; } set { _foregroundPrimary = value; } }

            private Color _foregroundSecondary;
            public Color ForegroundSecondary { get { return _foregroundSecondary; } set { _foregroundSecondary = value; } }

            private Color _foregroundDisabled;
            public Color ForegroundDisabled { get { return _foregroundDisabled; } set { _foregroundDisabled = value; } }

            private Color _borderColor;
            public Color BorderColor { get { return _borderColor; } set { _borderColor = value; } }

            private Color _buttonBackground;
            public Color ButtonBackground { get { return _buttonBackground; } set { _buttonBackground = value; } }

            private Color _buttonHover;
            public Color ButtonHover { get { return _buttonHover; } set { _buttonHover = value; } }

            private Color _buttonPressed;
            public Color ButtonPressed { get { return _buttonPressed; } set { _buttonPressed = value; } }

            private Color _menuBackground;
            public Color MenuBackground { get { return _menuBackground; } set { _menuBackground = value; } }

            private Color _menuForeground;
            public Color MenuForeground { get { return _menuForeground; } set { _menuForeground = value; } }

            private Color _gridBackground;
            public Color GridBackground { get { return _gridBackground; } set { _gridBackground = value; } }

            private Color _gridHeaderBackground;
            public Color GridHeaderBackground { get { return _gridHeaderBackground; } set { _gridHeaderBackground = value; } }

            private Color _gridAlternateRow;
            public Color GridAlternateRow { get { return _gridAlternateRow; } set { _gridAlternateRow = value; } }

            private Color _gridSelectionBackground;
            public Color GridSelectionBackground { get { return _gridSelectionBackground; } set { _gridSelectionBackground = value; } }

            private Color _positiveColor;
            public Color PositiveColor { get { return _positiveColor; } set { _positiveColor = value; } }

            private Color _negativeColor;
            public Color NegativeColor { get { return _negativeColor; } set { _negativeColor = value; } }

            private Color _neutralColor;
            public Color NeutralColor { get { return _neutralColor; } set { _neutralColor = value; } }
        }

        private static ThemeType currentTheme = ThemeType.Dark;

        /// <summary>
        /// 深色主题配色
        /// </summary>
        public static ThemeColors DarkTheme = CreateDarkTheme();

        private static ThemeColors CreateDarkTheme()
        {
            ThemeColors theme = new ThemeColors();
            theme.BackgroundPrimary = Color.FromArgb(30, 30, 30);
            theme.BackgroundSecondary = Color.FromArgb(40, 40, 40);
            theme.BackgroundTertiary = Color.FromArgb(50, 50, 50);

            theme.ForegroundPrimary = Color.White;
            theme.ForegroundSecondary = Color.FromArgb(200, 200, 200);
            theme.ForegroundDisabled = Color.FromArgb(120, 120, 120);

            theme.BorderColor = Color.FromArgb(80, 80, 80);

            theme.ButtonBackground = Color.FromArgb(70, 70, 70);
            theme.ButtonHover = Color.FromArgb(85, 85, 85);
            theme.ButtonPressed = Color.FromArgb(55, 55, 55);

            theme.MenuBackground = Color.FromArgb(40, 40, 40);
            theme.MenuForeground = Color.White;

            theme.GridBackground = Color.FromArgb(40, 40, 40);
            theme.GridHeaderBackground = Color.FromArgb(50, 50, 50);
            theme.GridAlternateRow = Color.FromArgb(45, 45, 45);
            theme.GridSelectionBackground = Color.FromArgb(60, 60, 60);

            theme.PositiveColor = Color.FromArgb(255, 80, 80);   // 红色（涨）
            theme.NegativeColor = Color.FromArgb(80, 255, 80);   // 绿色（跌）
            theme.NeutralColor = Color.FromArgb(180, 180, 180);  // 灰色

            return theme;
        }

        /// <summary>
        /// 浅色主题配色
        /// </summary>
        public static ThemeColors LightTheme = CreateLightTheme();

        private static ThemeColors CreateLightTheme()
        {
            ThemeColors theme = new ThemeColors();
            theme.BackgroundPrimary = Color.FromArgb(245, 245, 245);
            theme.BackgroundSecondary = Color.FromArgb(255, 255, 255);
            theme.BackgroundTertiary = Color.FromArgb(235, 235, 235);
            theme.ForegroundPrimary = Color.FromArgb(30, 30, 30);
            theme.ForegroundSecondary = Color.FromArgb(80, 80, 80);
            theme.ForegroundDisabled = Color.FromArgb(160, 160, 160);
            theme.BorderColor = Color.FromArgb(200, 200, 200);
            theme.ButtonBackground = Color.FromArgb(230, 230, 230);
            theme.ButtonHover = Color.FromArgb(210, 210, 210);
            theme.ButtonPressed = Color.FromArgb(190, 190, 190);
            theme.MenuBackground = Color.FromArgb(250, 250, 250);
            theme.MenuForeground = Color.FromArgb(30, 30, 30);
            theme.GridBackground = Color.White;
            theme.GridHeaderBackground = Color.FromArgb(240, 240, 240);
            theme.GridAlternateRow = Color.FromArgb(248, 248, 248);
            theme.GridSelectionBackground = Color.FromArgb(200, 220, 240);
            theme.PositiveColor = Color.FromArgb(220, 20, 60);
            theme.NegativeColor = Color.FromArgb(34, 139, 34);
            theme.NeutralColor = Color.FromArgb(100, 100, 100);
            return theme;
        }

        /// <summary>
        /// 获取当前主题
        /// </summary>
        public static ThemeType CurrentTheme
        {
            get { return currentTheme; }
            set { currentTheme = value; }
        }

        /// <summary>
        /// 获取当前主题颜色配置
        /// </summary>
        public static ThemeColors CurrentColors
        {
            get
            {
                return currentTheme == ThemeType.Dark ? DarkTheme : LightTheme;
            }
        }

        /// <summary>
        /// 切换主题
        /// </summary>
        public static void SwitchTheme(ThemeType theme)
        {
            currentTheme = theme;
        }

        /// <summary>
        /// 应用主题到表单
        /// </summary>
        public static void ApplyTheme(Form form)
        {
            ThemeColors colors = CurrentColors;

            // 应用到表单本身
            form.BackColor = colors.BackgroundPrimary;
            form.ForeColor = colors.ForegroundPrimary;

            // 递归应用到所有控件
            ApplyThemeToControl(form, colors);
        }

        /// <summary>
        /// 递归应用主题到控件
        /// </summary>
        private static void ApplyThemeToControl(Control control, ThemeColors colors)
        {
            // MenuStrip
            MenuStrip menuStrip = control as MenuStrip;
            if (menuStrip != null)
            {
                menuStrip.BackColor = colors.MenuBackground;
                menuStrip.ForeColor = colors.MenuForeground;
                foreach (ToolStripItem item in menuStrip.Items)
                {
                    ApplyThemeToToolStripItem(item, colors);
                }
            }
            // Panel
            Panel panel = control as Panel;
            if (panel != null)
            {
                panel.BackColor = colors.BackgroundPrimary;
                panel.ForeColor = colors.ForegroundPrimary;
            }
            // SplitContainer
            SplitContainer splitContainer = control as SplitContainer;
            if (splitContainer != null)
            {
                splitContainer.BackColor = colors.BackgroundPrimary;
                splitContainer.Panel1.BackColor = colors.BackgroundPrimary;
                splitContainer.Panel2.BackColor = colors.BackgroundPrimary;
            }
            // Button
            Button button = control as Button;
            if (button != null)
            {
                button.BackColor = colors.ButtonBackground;
                button.ForeColor = colors.ForegroundPrimary;
                button.FlatAppearance.BorderColor = colors.BorderColor;
                button.FlatAppearance.MouseOverBackColor = colors.ButtonHover;
                button.FlatAppearance.MouseDownBackColor = colors.ButtonPressed;
            }
            // TextBox
            TextBox textBox = control as TextBox;
            if (textBox != null)
            {
                textBox.BackColor = colors.BackgroundSecondary;
                textBox.ForeColor = colors.ForegroundPrimary;
            }
            // RichTextBox
            RichTextBox richTextBox = control as RichTextBox;
            if (richTextBox != null)
            {
                richTextBox.BackColor = colors.BackgroundSecondary;
                richTextBox.ForeColor = colors.ForegroundPrimary;
            }
            // DataGridView
            DataGridView dataGridView = control as DataGridView;
            if (dataGridView != null)
            {
                dataGridView.BackgroundColor = colors.GridBackground;
                dataGridView.ForeColor = colors.ForegroundPrimary;
                dataGridView.GridColor = colors.BorderColor;
                dataGridView.DefaultCellStyle.BackColor = colors.GridBackground;
                dataGridView.DefaultCellStyle.ForeColor = colors.ForegroundPrimary;
                dataGridView.DefaultCellStyle.SelectionBackColor = colors.GridSelectionBackground;
                dataGridView.DefaultCellStyle.SelectionForeColor = colors.ForegroundPrimary;
                dataGridView.AlternatingRowsDefaultCellStyle.BackColor = colors.GridAlternateRow;
                dataGridView.ColumnHeadersDefaultCellStyle.BackColor = colors.GridHeaderBackground;
                dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = colors.ForegroundPrimary;
                dataGridView.EnableHeadersVisualStyles = false;
            }
            // Label
            Label label = control as Label;
            if (label != null)
            {
                label.ForeColor = colors.ForegroundPrimary;
            }

            // 递归处理子控件
            foreach (Control childControl in control.Controls)
            {
                ApplyThemeToControl(childControl, colors);
            }
        }

        /// <summary>
        /// 应用主题到ToolStripItem
        /// </summary>
        private static void ApplyThemeToToolStripItem(ToolStripItem item, ThemeColors colors)
        {
            item.BackColor = colors.MenuBackground;
            item.ForeColor = colors.MenuForeground;

            ToolStripMenuItem menuItem = item as ToolStripMenuItem;
            if (menuItem != null)
            {
                foreach (ToolStripItem subItem in menuItem.DropDownItems)
                {
                    ApplyThemeToToolStripItem(subItem, colors);
                }

                if (menuItem.DropDown != null)
                {
                    menuItem.DropDown.BackColor = colors.MenuBackground;
                    menuItem.DropDown.ForeColor = colors.MenuForeground;
                }
            }
        }
    }
}
