﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;

namespace GUETCampusNetAutoLogin
{
    internal static class Program
    {
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
        }

        private static void OnNetworkAddressChanged(object sender, EventArgs e)
        {
            Console.WriteLine("[INFO] Network address changed.");

            Thread.Sleep(2000);
            TryAutoLogin();
        }

        /// <summary>
        /// 获取当前连接的 WiFi SSID
        /// </summary>
        private static string GetCurrentSSID()
        {
            try
            {
                // 使用 WMI 查询当前连接的 WiFi 网络
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT * FROM MSNDis_80211_ServiceSetIdentifier"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        byte[] ssidBytes = obj["Ndis80211SsId"] as byte[];
                        if (ssidBytes != null)
                        {
                            string ssid = System.Text.Encoding.UTF8.GetString(ssidBytes).TrimEnd('\0');
                            return ssid;
                        }
                    }
                }

                // 备用方法：通过 NetworkInterface 获取
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        // 尝试通过 WMI 获取该接口的 SSID
                        string ssid = GetSSIDFromInterface(ni.Description);
                        if (!string.IsNullOrEmpty(ssid))
                        {
                            return ssid;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 获取 SSID 失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 通过接口描述获取 SSID
        /// </summary>
        private static string GetSSIDFromInterface(string interfaceDescription)
        {
            try
            {
                string query = $"SELECT * FROM MSNDis_80211_ServiceSetIdentifier WHERE InstanceName LIKE '%{interfaceDescription.Replace("'", "''")}%'";
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        byte[] ssidBytes = obj["Ndis80211SsId"] as byte[];
                        if (ssidBytes != null)
                        {
                            return System.Text.Encoding.UTF8.GetString(ssidBytes).TrimEnd('\0');
                        }
                    }
                }
            }
            catch { }
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
