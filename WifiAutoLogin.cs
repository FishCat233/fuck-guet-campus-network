using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GUETCampusNetAutoLogin
{
    /// <summary>
    /// 登录结果
    /// </summary>
    public class LoginResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 结果消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static LoginResult SuccessResult(string message = "登录成功")
        {
            return new LoginResult { Success = true, Message = message };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static LoginResult FailureResult(string message)
        {
            return new LoginResult { Success = false, Message = message };
        }
    }

    /// <summary>
    /// 校园网自动登录服务
    /// </summary>
    internal class WifiAutoLogin
    {
        private readonly HttpClient _httpClient;

        // 校园网登录相关配置
        private const string LOGIN_API_URL = "http://10.0.1.5:801/eportal/portal/login";
        private const string WLAN_USER_MAC = "000000000000";
        private const string WLAN_AC_NAME = "HJ-BRAS-ME60-01";
        private const string REFERER = "https://10.0.1.5:801/";
        private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(10);

        public WifiAutoLogin()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = REQUEST_TIMEOUT;
            _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Referer", REFERER);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36 Edg/147.0.0.0");
        }

        /// <summary>
        /// 执行登录操作
        /// </summary>
        public LoginResult Login(string username, string password)
        {
            try
            {
                // 构建登录 URL
                var loginUrl = BuildLoginUrl(username, password);

                // 发送 GET 登录请求
                var response = _httpClient.GetAsync(loginUrl).Result;
                var responseContent = response.Content.ReadAsStringAsync().Result;

                // 解析登录结果
                return ParseLoginResponse(responseContent);
            }
            catch (TaskCanceledException)
            {
                return LoginResult.FailureResult("登录请求超时，请检查网络");
            }
            catch (HttpRequestException ex)
            {
                return LoginResult.FailureResult($"网络请求失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                return LoginResult.FailureResult($"登录异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 将密码进行 Base64 编码
        /// </summary>
        private string EncodePassword(string password)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
        }

        /// <summary>
        /// 构建登录 URL
        /// </summary>
        private string BuildLoginUrl(string username, string password)
        {
            var encodedPassword = EncodePassword(password);
            var userAccount = $",0,{username}";

            return $"{LOGIN_API_URL}?user_account={Uri.EscapeDataString(userAccount)}" +
                   $"&user_password={Uri.EscapeDataString(encodedPassword)}" +
                   $"&wlan_user_mac={WLAN_USER_MAC}" +
                   $"&wlan_ac_name={Uri.EscapeDataString(WLAN_AC_NAME)}";
        }

        /// <summary>
        /// 解析登录响应
        /// </summary>
        private LoginResult ParseLoginResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return LoginResult.FailureResult("服务器返回空响应");
            }

            // 解析 JSONP 格式: jsonpReturn({...})
            var jsonpMatch = Regex.Match(response, @"jsonpReturn\((.+?)\);?$");
            if (!jsonpMatch.Success)
            {
                return LoginResult.FailureResult($"无法解析响应: {response.Substring(0, Math.Min(100, response.Length))}");
            }

            var jsonContent = jsonpMatch.Groups[1].Value;

            // 提取 result 字段
            var resultMatch = Regex.Match(jsonContent, @"""result""?\s*:\s*(\d+)");
            var msgMatch = Regex.Match(jsonContent, @"""msg""?\s*:\s*""([^""]*)""");

            if (!resultMatch.Success)
            {
                return LoginResult.FailureResult($"无法解析结果: {response.Substring(0, Math.Min(100, response.Length))}");
            }

            int result = int.Parse(resultMatch.Groups[1].Value);
            string message = msgMatch.Success ? msgMatch.Groups[1].Value : "未知响应";

            // result=1 表示成功
            if (result == 1)
            {
                return LoginResult.SuccessResult(message);
            }

            // result=0 且包含"已经在线"表示已在线
            if (result == 0 && message.Contains("已经在线"))
            {
                return LoginResult.SuccessResult(message);
            }

            return LoginResult.FailureResult(message);
        }
    }
}
