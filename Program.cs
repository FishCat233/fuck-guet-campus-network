﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Threading;
using ManagedNativeWifi;

namespace GUETCampusNetAutoLogin
{
    internal static class Program
    {
        // 登录防抖控制字段
        private static readonly object _loginLock = new object();
        private static DateTime _lastLoginAttempt = DateTime.MinValue;
        private static readonly TimeSpan _loginCooldown = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            Console.WriteLine("[INFO] Network address changed event registered.");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());

            // 程序退出前取消订阅网络事件，释放资源
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            Console.WriteLine("[INFO] Network address changed event unregistered.");
        }

        private static void OnNetworkAddressChanged(object sender, EventArgs e)
        {
            Console.WriteLine("[INFO] Network address changed.");

            Thread.Sleep(2000);

            // 检查是否处于冷却期，防止短时间内重复登录
            lock (_loginLock)
            {
                var timeSinceLastLogin = DateTime.Now - _lastLoginAttempt;
                if (timeSinceLastLogin < _loginCooldown)
                {
                    Console.WriteLine($"[INFO] 登录冷却中，跳过本次登录请求 (剩余 {(_loginCooldown - timeSinceLastLogin).TotalSeconds:F0} 秒)");
                    return;
                }
                _lastLoginAttempt = DateTime.Now;
            }

            TryAutoLogin();
        }

        /// <summary>
        /// 获取当前连接的 WiFi SSID
        /// </summary>
        private static string GetCurrentSSID()
        {
            try
            {
                // 使用 Native Wifi API 获取当前连接的 WiFi SSID
                string ssid = WiFiManager.GetCurrentSSID();
                if (!string.IsNullOrEmpty(ssid))
                {
                    return ssid;
                }

                Console.WriteLine("[WARN] 无法通过 Native Wifi API 获取 SSID");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 获取 SSID 失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 尝试自动登录
        /// </summary>
        private static void TryAutoLogin()
        {
            string currentSSID = GetCurrentSSID();
            Console.WriteLine($"[INFO] 当前 SSID: {currentSSID ?? "未获取到"}");

            if (string.IsNullOrEmpty(currentSSID))
            {
                Console.WriteLine("[WARN] 无法获取当前 WiFi SSID，跳过自动登录");
                return;
            }

            // 检查是否为校园网 SSID
            if (currentSSID != "GUET-WiFi")
            {
                Console.WriteLine($"[INFO] 当前网络 '{currentSSID}' 不是校园网，跳过登录");
                return;
            }

            Console.WriteLine($"[INFO] 检测到校园网 '{currentSSID}'，尝试自动登录...");

            // 获取保存的账号密码
            string username = Properties.Settings.Default.Username;
            string password = Properties.Settings.Default.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("[WARN] 未配置账号密码，无法自动登录");
                ShowNotification("自动登录失败", "请先配置校园网账号密码", ToolTipIcon.Warning);
                return;
            }

            // 执行登录
            var loginService = new WifiAutoLogin();
            var result = loginService.Login(username, password);

            if (result.Success)
            {
                Console.WriteLine("[INFO] 登录成功");
                ShowNotification("自动登录成功", "已成功连接到校园网", ToolTipIcon.Info);
            }
            else
            {
                Console.WriteLine($"[ERROR] 登录失败: {result.Message}");
                ShowNotification("自动登录失败", result.Message, ToolTipIcon.Error);
            }
        }

        /// <summary>
        /// 显示托盘通知
        /// </summary>
        private static void ShowNotification(string title, string message, ToolTipIcon icon)
        {
            // 通过主窗体的 NotifyIcon 显示通知
            MainForm.Instance?.ShowNotification(title, message, icon);
        }
    }
}
