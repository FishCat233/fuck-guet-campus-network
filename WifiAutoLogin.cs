using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string LOGIN_PAGE_URL = "http://10.32.254.3/";
        private const string LOGIN_API_URL = "http://10.32.254.3/drcom/login";
        private const string CHECK_URL = "http://10.32.254.3/drcom/chkstatus";
        private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(10);

        public WifiAutoLogin()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = REQUEST_TIMEOUT;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        /// <summary>
        /// 执行登录操作
        /// </summary>
        public LoginResult Login(string username, string password)
        {
            try
            {
                // 先检查是否已经在线
                if (IsAlreadyOnline())
                {
                    return LoginResult.SuccessResult("您已经在线");
                }

                // 获取登录页面中的必要参数（如 challenge, mac 等）
                var loginParams = GetLoginParameters();
                if (loginParams == null)
                {
                    return LoginResult.FailureResult("无法获取登录参数，请检查网络连接");
                }

                // 构建登录请求
                var content = BuildLoginContent(username, password, loginParams);

                // 发送登录请求
                var response = _httpClient.PostAsync(LOGIN_API_URL, content).Result;
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
        /// 检查是否已在线
        /// </summary>
        private bool IsAlreadyOnline()
        {
            try
            {
                var response = _httpClient.GetAsync(CHECK_URL).Result;
                if (response.IsSuccessStatusCode)
                {
                    var content = response.Content.ReadAsStringAsync().Result;
                    // 根据实际响应判断，通常包含用户信息的响应表示已在线
                    return content.Contains("uid") || content.Contains("username");
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 从登录页面获取必要参数
        /// </summary>
        private Dictionary<string, string> GetLoginParameters()
        {
            try
            {
                var response = _httpClient.GetAsync(LOGIN_PAGE_URL).Result;
                var content = response.Content.ReadAsStringAsync().Result;

                var parameters = new Dictionary<string, string>();

                // 提取 challenge 参数
                var challengeMatch = Regex.Match(content, @"challenge""?\s*[:=]\s*[""']?([^""'\s,;]+)");
                if (challengeMatch.Success)
                {
                    parameters["challenge"] = challengeMatch.Groups[1].Value;
                }

                // 提取 mac 参数
                var macMatch = Regex.Match(content, @"mac""?\s*[:=]\s*[""']?([^""'\s,;]+)");
                if (macMatch.Success)
                {
                    parameters["mac"] = macMatch.Groups[1].Value;
                }

                // 提取其他可能需要的参数
                var acidMatch = Regex.Match(content, @"acid""?\s*[:=]\s*[""']?([^""'\s,;]+)");
                if (acidMatch.Success)
                {
                    parameters["acid"] = acidMatch.Groups[1].Value;
                }

                return parameters.Count > 0 ? parameters : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 构建登录请求内容
        /// </summary>
        private FormUrlEncodedContent BuildLoginContent(string username, string password, Dictionary<string, string> parameters)
        {
            var formData = new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = password,
                ["domain"] = "",  // 根据实际需要填写
                ["enablemacauth"] = "0"
            };

            // 添加从页面获取的参数
            foreach (var param in parameters)
            {
                if (!formData.ContainsKey(param.Key))
                {
                    formData[param.Key] = param.Value;
                }
            }

            return new FormUrlEncodedContent(formData);
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

            // 成功响应通常包含 "success" 或 "ok"
            if (response.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0 ||
                response.IndexOf("ok", StringComparison.OrdinalIgnoreCase) >= 0 ||
                response.IndexOf("登录成功", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return LoginResult.SuccessResult();
            }

            // 解析错误信息
            var errorMatch = Regex.Match(response, @"""error""?\s*[:=]\s*[""']([^""']+)""");
            if (errorMatch.Success)
            {
                return LoginResult.FailureResult($"登录失败: {errorMatch.Groups[1].Value}");
            }

            var msgMatch = Regex.Match(response, @"""message""?\s*[:=]\s*[""']([^""']+)""");
            if (msgMatch.Success)
            {
                return LoginResult.FailureResult(msgMatch.Groups[1].Value);
            }

            // 如果响应中包含已在线的提示
            if (response.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0 ||
                response.IndexOf("online", StringComparison.OrdinalIgnoreCase) >= 0 ||
                response.IndexOf("已在线", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return LoginResult.SuccessResult("您已经在线");
            }

            // 默认返回原始响应
            return LoginResult.FailureResult($"未知响应: {response.Substring(0, Math.Min(100, response.Length))}");
        }
    }
}
