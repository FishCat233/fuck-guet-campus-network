using System;
using System.Linq;
using ManagedNativeWifi;

namespace GUETCampusNetAutoLogin
{
    /// <summary>
    /// WiFi 管理类，用于获取当前连接的 WiFi SSID
    /// 使用 ManagedNativeWifi NuGet 包封装 Native Wifi API
    /// </summary>
    internal static class WiFiManager
    {
        /// <summary>
        /// 获取当前连接的 WiFi SSID
        /// </summary>
        /// <returns>SSID 字符串，如果未连接则返回 null</returns>
        public static string GetCurrentSSID()
        {
            try
            {
                // 使用 ManagedNativeWifi 获取已连接网络的 SSID
                var connectedSsids = NativeWifi.EnumerateConnectedNetworkSsids();
                
                string ssid = connectedSsids.FirstOrDefault()?.ToString();
                
                if (!string.IsNullOrEmpty(ssid))
                {
                    return ssid;
                }
                
                Console.WriteLine("[INFO] 未检测到已连接的 WiFi 网络");
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine("[ERROR] wlanapi.dll 未找到，请确保系统支持 WLAN API");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 获取 SSID 时发生异常: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 获取所有可用 WiFi 网络的 SSID（用于调试）
        /// </summary>
        /// <returns>可用网络 SSID 列表</returns>
        public static string[] GetAvailableSSIDs()
        {
            try
            {
                return NativeWifi.EnumerateAvailableNetworkSsids()
                    .Select(x => x.ToString())
                    .ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 获取可用网络失败: {ex.Message}");
                return new string[0];
            }
        }
    }
}
