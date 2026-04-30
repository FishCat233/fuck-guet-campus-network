﻿using System;
using System.ComponentModel;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GUETCampusNetAutoLogin
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// 单例实例，供 Program 类调用
        /// </summary>
        public static MainForm Instance { get; private set; }

        public MainForm()
        {
            InitializeComponent();
            Instance = this;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // 加载保存的配置
            LoadSettings();

            // 根据设置决定是否最小化到托盘
            if (Properties.Settings.Default.StartMinimized)
            {
                WindowState = FormWindowState.Minimized;
                Hide();
            }

            UpdateStatus("程序已启动");
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 点击关闭按钮时最小化到托盘而不是退出
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                WindowState = FormWindowState.Minimized;
                Hide();
                ShowNotification("程序已最小化", "双击托盘图标显示主窗口", ToolTipIcon.Info);
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        }

        #region 配置加载与保存

        /// <summary>
        /// 加载保存的设置
        /// </summary>
        private void LoadSettings()
        {
            textBoxUsername.Text = Properties.Settings.Default.Username;
            textBoxPassword.Text = Properties.Settings.Default.Password;
            checkBoxAutoStart.Checked = Properties.Settings.Default.AutoStart;
            checkBoxStartMinimized.Checked = Properties.Settings.Default.StartMinimized;
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        private void SaveSettings()
        {
            Properties.Settings.Default.Username = textBoxUsername.Text.Trim();
            Properties.Settings.Default.Password = textBoxPassword.Text;
            Properties.Settings.Default.AutoStart = checkBoxAutoStart.Checked;
            Properties.Settings.Default.StartMinimized = checkBoxStartMinimized.Checked;
            Properties.Settings.Default.Save();

            // 设置开机自启动
            SetAutoStart(checkBoxAutoStart.Checked);
        }

        /// <summary>
        /// 设置开机自启动
        /// </summary>
        private void SetAutoStart(bool enable)
        {
            try
            {
                string appName = "GUETCampusNetAutoLogin";
                string appPath = Application.ExecutablePath;

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (enable)
                    {
                        key.SetValue(appName, appPath);
                    }
                    else
                    {
                        if (key.GetValue(appName) != null)
                        {
                            key.DeleteValue(appName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置开机自启动失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 保存配置按钮点击
        /// </summary>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxUsername.Text))
            {
                MessageBox.Show("请输入账号", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBoxUsername.Focus();
                return;
            }

            if (string.IsNullOrEmpty(textBoxPassword.Text))
            {
                MessageBox.Show("请输入密码", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBoxPassword.Focus();
                return;
            }

            SaveSettings();
            UpdateStatus("配置已保存");
            MessageBox.Show("配置保存成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 测试登录按钮点击
        /// </summary>
        private void buttonTestLogin_Click(object sender, EventArgs e)
        {
            string username = textBoxUsername.Text.Trim();
            string password = textBoxPassword.Text;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("请先输入账号和密码", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            UpdateStatus("正在测试登录...");
            buttonTestLogin.Enabled = false;

            // 在后台线程执行登录测试
            var worker = new BackgroundWorker();
            worker.DoWork += (s, args) =>
            {
                var loginService = new WifiAutoLogin();
                args.Result = loginService.Login(username, password);
            };
            worker.RunWorkerCompleted += (s, args) =>
            {
                buttonTestLogin.Enabled = true;

                if (args.Error != null)
                {
                    UpdateStatus("登录测试出错");
                    MessageBox.Show($"测试登录时发生错误: {args.Error.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var result = args.Result as LoginResult;
                if (result.Success)
                {
                    UpdateStatus("登录测试成功");
                    MessageBox.Show(result.Message, "登录成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    UpdateStatus("登录测试失败");
                    MessageBox.Show(result.Message, "登录失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            worker.RunWorkerAsync();
        }

        /// <summary>
        /// 显示/隐藏密码
        /// </summary>
        private void checkBoxShowPassword_CheckedChanged(object sender, EventArgs e)
        {
            textBoxPassword.PasswordChar = checkBoxShowPassword.Checked ? '\0' : '*';
        }

        /// <summary>
        /// 托盘图标双击
        /// </summary>
        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ShowMainWindow();
        }

        /// <summary>
        /// 右键菜单 - 显示主窗口
        /// </summary>
        private void 显示主窗口ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowMainWindow();
        }

        /// <summary>
        /// 右键菜单 - 手动登录
        /// </summary>
        private void 手动登录ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string username = Properties.Settings.Default.Username;
            string password = Properties.Settings.Default.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowNotification("手动登录失败", "请先配置账号密码", ToolTipIcon.Warning);
                ShowMainWindow();
                return;
            }

            UpdateStatus("正在手动登录...");

            var worker = new BackgroundWorker();
            worker.DoWork += (s, args) =>
            {
                var loginService = new WifiAutoLogin();
                args.Result = loginService.Login(username, password);
            };
            worker.RunWorkerCompleted += (s, args) =>
            {
                if (args.Error != null)
                {
                    UpdateStatus("手动登录出错");
                    ShowNotification("登录错误", args.Error.Message, ToolTipIcon.Error);
                    return;
                }

                var result = args.Result as LoginResult;
                if (result.Success)
                {
                    UpdateStatus("手动登录成功");
                    ShowNotification("登录成功", result.Message, ToolTipIcon.Info);
                }
                else
                {
                    UpdateStatus("手动登录失败");
                    ShowNotification("登录失败", result.Message, ToolTipIcon.Error);
                }
            };
            worker.RunWorkerAsync();
        }

        /// <summary>
        /// 右键菜单 - 退出
        /// </summary>
        private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 真正退出程序
            notifyIcon1.Visible = false;
            Application.Exit();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 显示主窗口
        /// </summary>
        private void ShowMainWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        /// <summary>
        /// 更新状态栏文本
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateStatus), message);
                return;
            }
            toolStripStatusLabel1.Text = message;
        }

        /// <summary>
        /// 显示托盘通知
        /// </summary>
        public void ShowNotification(string title, string message, ToolTipIcon icon)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, string, ToolTipIcon>(ShowNotification), title, message, icon);
                return;
            }
            notifyIcon1.ShowBalloonTip(3000, title, message, icon);
        }

        #endregion
    }
}
