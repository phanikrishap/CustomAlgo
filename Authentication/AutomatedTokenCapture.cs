using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Text;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using log4net;

namespace CustomAlgo.Authentication
{
    /// <summary>
    /// Automated HTTP-based token capture for Zerodha OAuth without WebView2 dependencies
    /// Provides a complete WebView2-free solution for console applications
    /// </summary>
    public class AutomatedTokenCapture : IDisposable
    {
        private readonly string _apiKey;
        private readonly string _apiSecret;
        private readonly string _userId;
        private readonly string _password;
        private readonly string _totpSecret;
        private readonly int _localPort;
        private readonly string _redirectUrl;
        private HttpListener _httpListener;
        private string _capturedRequestToken;
        private readonly ManualResetEventSlim _tokenCaptured = new ManualResetEventSlim(false);
        private readonly CookieContainer _cookieContainer;
        private readonly ILog _logger;
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MaxTimeout = TimeSpan.FromMinutes(2);

        public AutomatedTokenCapture(string apiKey, string apiSecret, string userId, string password, string totpSecret, int localPort = 8001, string redirectUrl = null)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _apiSecret = apiSecret ?? throw new ArgumentNullException(nameof(apiSecret));
            _userId = userId ?? throw new ArgumentNullException(nameof(userId));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            _totpSecret = totpSecret ?? throw new ArgumentNullException(nameof(totpSecret));
            _localPort = localPort;
            _redirectUrl = redirectUrl ?? $"http://127.0.0.1:{localPort}/callback";
            _cookieContainer = new CookieContainer();
            _logger = LogManager.GetLogger(typeof(AutomatedTokenCapture));
        }

        /// <summary>
        /// Captures an access token automatically using HTTP requests and session management
        /// </summary>
        /// <returns>Valid access token ready for Kite Connect API usage</returns>
        public async Task<string> CaptureAccessTokenAsync()
        {
            try
            {
                // Ensure we start fresh
                var zerodhaUri = new Uri("https://kite.zerodha.com");
                var cookies = _cookieContainer.GetCookies(zerodhaUri);
                foreach (Cookie cookie in cookies)
                {
                    cookie.Expired = true;
                }
                
                // Only reset if not disposed
                if (!_tokenCaptured.IsSet)
                {
                    try
                    {
                        _tokenCaptured.Reset();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Ignore disposal errors during reset
                    }
                }
                _capturedRequestToken = null;

                // Start local server to capture OAuth redirect
                StartLocalServer();
                
                // Perform automated HTTP-based login with timeout
                var loginTask = PerformAutomatedLoginAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
                
                var completedTask = await Task.WhenAny(loginTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    _logger.Error("❌ Login process timed out after 2 minutes");
                    throw new TimeoutException("Login process timed out after 2 minutes");
                }

                var requestToken = await loginTask;
                
                if (!string.IsNullOrEmpty(requestToken))
                {
                    // Convert request token to access token
                    var accessToken = await GenerateAccessTokenAsync(requestToken);
                    return !string.IsNullOrEmpty(accessToken) ? accessToken : requestToken;
                }

                // Fallback to redirect capture if HTTP automation didn't complete
                var tokenCaptured = await Task.Run(() => _tokenCaptured.Wait(TimeSpan.FromMinutes(2)));
                if (!tokenCaptured)
                {
                    throw new TimeoutException("Timeout waiting for request token capture");
                }

                // Convert captured request token to access token
                var finalAccessToken = await GenerateAccessTokenAsync(_capturedRequestToken);
                return !string.IsNullOrEmpty(finalAccessToken) ? finalAccessToken : _capturedRequestToken;
            }
            finally
            {
                StopLocalServer();
                
                // Safe reset with disposal check
                try
                {
                    if (!_tokenCaptured.IsSet)
                    {
                        _tokenCaptured.Reset();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Ignore disposal errors during cleanup
                }
                
                _capturedRequestToken = null;
                var zerodhaUri = new Uri("https://kite.zerodha.com");
                var cookies = _cookieContainer.GetCookies(zerodhaUri);
                foreach (Cookie cookie in cookies)
                {
                    cookie.Expired = true;
                }
            }
        }

        private HttpClient CreateConfiguredHttpClient(bool allowAutoRedirect = true)
        {
            _logger.Info($"🔧 Creating HTTP client (AutoRedirect: {allowAutoRedirect})");
            
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                UseCookies = true,
                AllowAutoRedirect = allowAutoRedirect,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler)
            {
                Timeout = DefaultTimeout
            };

            // Set realistic headers
            client.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            
            _logger.Info("✅ HTTP client created with headers configured");
            return client;
        }

        private async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout, string operationName)
        {
            _logger.Info($"⏳ Starting operation '{operationName}' with {timeout.TotalSeconds}s timeout");
            using var cts = new CancellationTokenSource();
            var timeoutTask = Task.Delay(timeout, cts.Token);
            
            try
            {
                _logger.Info($"⌛ Waiting for operation '{operationName}' to complete...");
                var completedTask = await Task.WhenAny(task, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    _logger.Error($"⏰ Operation '{operationName}' timed out after {timeout.TotalSeconds} seconds");
                    throw new TimeoutException($"Operation '{operationName}' timed out after {timeout.TotalSeconds} seconds");
                }
                
                cts.Cancel(); // Cancel the timeout task
                _logger.Info($"✅ Operation '{operationName}' completed successfully");
                return await task; // Unwrap the result or propagate the exception
            }
            catch (Exception ex) when (!(ex is TimeoutException))
            {
                _logger.Error($"❌ Operation '{operationName}' failed with error: {ex.GetType().Name} - {ex.Message}");
                throw new Exception($"Operation '{operationName}' failed: {ex.Message}", ex);
            }
        }

        private async Task<string> PerformAutomatedLoginAsync()
        {
            try
            {
                _logger.Info("🔄 Starting automated HTTP-based login process...");
                _logger.Info($"📍 Local server port: {_localPort}");
                
                using var httpClient = CreateConfiguredHttpClient();
                
                _logger.Info("🌐 Step 1: Getting initial login page...");
                
                // Step 1: Get initial login page with timeout
                var loginUrl = $"https://kite.trade/connect/login?api_key={_apiKey}&v=3&redirect_uri={Uri.EscapeDataString(_redirectUrl)}";
                _logger.Info($"🔗 Login URL: {loginUrl}");
                
                try
                {
                    _logger.Info("📡 Sending GET request to login page...");
                    var loginPageResponse = await WithTimeout(
                        httpClient.GetAsync(loginUrl),
                        DefaultTimeout,
                        "Get login page"
                    );
                    
                    _logger.Info($"📥 Login page response received: {loginPageResponse.StatusCode}");
                    _logger.Info($"📍 Response URL: {loginPageResponse.RequestMessage.RequestUri}");
                    
                    var loginPageContent = await WithTimeout(
                        loginPageResponse.Content.ReadAsStringAsync(),
                        DefaultTimeout,
                        "Read login page content"
                    );
                    
                    _logger.Info($"📄 Login page content length: {loginPageContent?.Length ?? 0} chars");
                    
                    if (!loginPageResponse.IsSuccessStatusCode)
                    {
                        _logger.Error($"❌ Login page request failed: {loginPageResponse.StatusCode}");
                        _logger.Error($"❌ Response content: {loginPageContent}");
                        throw new HttpRequestException($"Login page request failed with status {loginPageResponse.StatusCode}");
                    }
                    
                    // Extract hidden fields and form action
                    var hiddenFields = ExtractHiddenFields(loginPageContent);
                    var formAction = ExtractFormAction(loginPageContent);
                    
                    _logger.Info($"🔍 Found {hiddenFields.Count} hidden fields");
                    foreach (var field in hiddenFields)
                    {
                        _logger.Info($"  - {field.Key}: {(field.Key.ToLowerInvariant().Contains("password") ? "***" : field.Value)}");
                    }
                    _logger.Info($"📝 Form action: {formAction ?? "default"}");
                    
                    // Step 2: Submit username and password
                    _logger.Info("🔐 Step 2: Submitting credentials...");
                    
                    var loginPayload = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("user_id", _userId),
                        new KeyValuePair<string, string>("password", _password)
                    };
                    
                    // Add hidden fields
                    foreach (var field in hiddenFields)
                    {
                        loginPayload.Add(new KeyValuePair<string, string>(field.Key, field.Value));
                    }

                    var loginPostData = new FormUrlEncodedContent(loginPayload);
                    var loginSubmitUrl = string.IsNullOrEmpty(formAction) ? 
                        "https://kite.zerodha.com/api/login" : formAction;
                    
                    var loginResponse = await WithTimeout(
                        httpClient.PostAsync(loginSubmitUrl, loginPostData),
                        DefaultTimeout,
                        "Submit login credentials"
                    );
                    
                    var loginResponseContent = await WithTimeout(
                        loginResponse.Content.ReadAsStringAsync(),
                        DefaultTimeout,
                        "Read login response"
                    );
                    
                    _logger.Info($"✅ Login response: {loginResponse.StatusCode}");
                    
                    // Check if we need TOTP
                    if (loginResponseContent.Contains("twofa") || loginResponseContent.Contains("request_id"))
                    {
                        _logger.Info("🔢 Step 3: TOTP required, extracting request_id...");
                        
                        // Extract request_id from response
                        var requestId = ExtractRequestId(loginResponseContent);
                        if (string.IsNullOrEmpty(requestId))
                        {
                            _logger.Error("❌ Could not extract request_id from login response");
                            throw new InvalidOperationException("Could not extract request_id from login response");
                        }
                        
                        _logger.Info($"✅ Request ID extracted: {requestId}");
                        _logger.Info("🔢 Generating TOTP code...");

                        // Generate and submit TOTP
                        var totpCode = GenerateTotp();
                        _logger.Info($"✅ TOTP generated: {totpCode}");
                        _logger.Info("📤 Submitting TOTP...");
                        
                        var totpPayload = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("user_id", _userId),
                            new KeyValuePair<string, string>("request_id", requestId),
                            new KeyValuePair<string, string>("twofa_value", totpCode),
                            new KeyValuePair<string, string>("twofa_type", "totp"),
                            new KeyValuePair<string, string>("skip_session", "true")
                        });

                        var totpResponse = await WithTimeout(
                            httpClient.PostAsync("https://kite.zerodha.com/api/twofa", totpPayload),
                            DefaultTimeout,
                            "Submit TOTP"
                        );
                        
                        var totpResponseContent = await WithTimeout(
                            totpResponse.Content.ReadAsStringAsync(),
                            DefaultTimeout,
                            "Read TOTP response"
                        );
                        
                        _logger.Info($"✅ TOTP response: {totpResponse.StatusCode}");
                        
                        if (totpResponse.IsSuccessStatusCode)
                        {
                            _logger.Info("🔐 TOTP successful, completing OAuth flow programmatically...");
                            return await CompleteOAuthFlowProgrammatically(_redirectUrl);
                        }
                        else
                        {
                            _logger.Error($"❌ TOTP submission failed: {totpResponseContent}");
                        }
                    }
                    else
                    {
                        _logger.Info("✅ No TOTP required, proceeding with OAuth flow...");
                        return await CompleteOAuthFlowProgrammatically(_redirectUrl);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"❌ Failed during login page fetch: {ex.GetType().Name} - {ex.Message}");
                    _logger.Error($"Stack trace: {ex.StackTrace}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"❌ ERROR in PerformAutomatedLoginAsync: {ex.GetType().Name}: {ex.Message}");
                _logger.Error($"Stack trace: {ex.StackTrace}");
                
                // Fallback: open browser for manual completion
                var fallbackUrl = $"https://kite.trade/connect/login?api_key={_apiKey}&v=3&redirect_uri={Uri.EscapeDataString(_redirectUrl)}";
                try
                {
                    _logger.Info("🌐 Falling back to browser-based capture...");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fallbackUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception browserEx)
                {
                    _logger.Error($"❌ Browser fallback also failed: {browserEx.Message}");
                    _logger.Error($"Stack trace: {browserEx.StackTrace}");
                }
            }

            return null;
        }

        private async Task<string> CompleteOAuthFlowProgrammatically(string redirectUrl)
        {
            try
            {
                _logger.Info("🔗 Navigating to OAuth URL with authenticated session...");
                
                using var redirectClient = CreateConfiguredHttpClient(allowAutoRedirect: false);
                
                var oauthUrl = $"https://kite.trade/connect/login?api_key={_apiKey}&v=3&redirect_uri={Uri.EscapeDataString(redirectUrl)}";
                var oauthResponse = await WithTimeout(
                    redirectClient.GetAsync(oauthUrl),
                    DefaultTimeout,
                    "Initial OAuth request"
                );
                
                _logger.Info($"📍 OAuth response status: {oauthResponse.StatusCode}");
                
                // Follow redirect chain manually to capture token
                var currentResponse = oauthResponse;
                var redirectCount = 0;
                var maxRedirects = 10;
                
                while (IsRedirectStatus(currentResponse.StatusCode) && redirectCount < maxRedirects)
                {
                    redirectCount++;
                    var location = currentResponse.Headers.Location;
                    if (location == null) break;

                    _logger.Info($"📍 Following redirect {redirectCount}: {MaskUrl(location.ToString())}");
                    
                    currentResponse = await WithTimeout(
                        redirectClient.GetAsync(location),
                        DefaultTimeout,
                        $"Follow redirect {redirectCount}"
                    );
                    
                    _logger.Info($"📍 Redirect {redirectCount} status: {currentResponse.StatusCode}");
                    
                    // Check if we've reached the callback URL with the token
                    if (currentResponse.RequestMessage.RequestUri.ToString().Contains("request_token="))
                    {
                        var finalUrl = currentResponse.RequestMessage.RequestUri.ToString();
                        _logger.Info($"📍 Final URL after redirects: {MaskUrl(finalUrl)}");
                        
                        var token = ExtractTokenFromUrl(finalUrl);
                        if (!string.IsNullOrEmpty(token))
                        {
                            _logger.Info("✅ Successfully captured token from final URL!");
                            return token;
                        }
                    }
                    
                    // Check the response content for any token information
                    var finalResponseContent = await WithTimeout(
                        currentResponse.Content.ReadAsStringAsync(),
                        DefaultTimeout,
                        "Read redirect response content"
                    );
                    
                    var tokenFromContent = ExtractTokenFromResponse(finalResponseContent);
                    if (!string.IsNullOrEmpty(tokenFromContent))
                    {
                        _logger.Info("✅ Found request token in final response content!");
                        return tokenFromContent;
                    }
                }
                
                if (redirectCount >= maxRedirects)
                {
                    _logger.Warn("⚠️ Maximum redirect count reached without finding token");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"⚠️ Programmatic redirect failed: {ex.Message}", ex);
                _logger.Info("🔄 Will fall back to local server capture...");
            }

            return null;
        }

        public string GenerateTotp()
        {
            try
            {
                var secretBytes = Base32Decode(_totpSecret);
                var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var timeStep = unixTime / 30;
                
                var timeBytes = BitConverter.GetBytes(timeStep);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(timeBytes);

                using (var hmac = new HMACSHA1(secretBytes))
                {
                    var hash = hmac.ComputeHash(timeBytes);
                    var offset = hash[hash.Length - 1] & 0xF;
                    var code = (hash[offset] & 0x7F) << 24 |
                              (hash[offset + 1] & 0xFF) << 16 |
                              (hash[offset + 2] & 0xFF) << 8 |
                              (hash[offset + 3] & 0xFF);
                    
                    return (code % 1000000).ToString("D6");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate TOTP: {ex.Message}", ex);
            }
        }

        private async Task<string> GenerateAccessTokenAsync(string requestToken)
        {
            try
            {
                // Create checksum: SHA-256 of (api_key + request_token + api_secret)
                var checksumInput = _apiKey + requestToken + _apiSecret;
                var checksum = ComputeSha256Hash(checksumInput);

                using var httpClient = CreateConfiguredHttpClient();

                // Prepare payload for access token generation
                var tokenPayload = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("api_key", _apiKey),
                    new KeyValuePair<string, string>("request_token", requestToken),
                    new KeyValuePair<string, string>("checksum", checksum)
                });

                _logger.Info("🔄 Exchanging request token for access token...");

                // POST to session/token endpoint with timeout
                var sessionTokenUrl = "https://api.kite.trade/session/token";
                var sessionResponse = await WithTimeout(
                    httpClient.PostAsync(sessionTokenUrl, tokenPayload),
                    DefaultTimeout,
                    "Exchange token"
                );
                
                var sessionResponseContent = await WithTimeout(
                    sessionResponse.Content.ReadAsStringAsync(),
                    DefaultTimeout,
                    "Read token response"
                );
                
                if (sessionResponse.IsSuccessStatusCode)
                {
                    var accessToken = ExtractAccessTokenFromResponse(sessionResponseContent);
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        _logger.Info("✅ Successfully exchanged request token for access token!");
                        return accessToken;
                    }
                    else
                    {
                        _logger.Error("❌ Could not extract access token from successful response");
                    }
                }
                else
                {
                    _logger.Error($"❌ Token exchange failed: {sessionResponse.StatusCode} - {sessionResponseContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"❌ Failed to generate access token: {ex.Message}", ex);
            }

            return null;
        }

        #region Helper Methods

        private Dictionary<string, string> ExtractHiddenFields(string html)
        {
            var hiddenFields = new Dictionary<string, string>();
            var inputMatches = Regex.Matches(html, @"<input[^>]*type=[""']hidden[""'][^>]*>", RegexOptions.IgnoreCase);
            
            foreach (Match match in inputMatches)
            {
                var nameMatch = Regex.Match(match.Value, @"name=[""']([^""']*)[""']", RegexOptions.IgnoreCase);
                var valueMatch = Regex.Match(match.Value, @"value=[""']([^""']*)[""']", RegexOptions.IgnoreCase);
                
                if (nameMatch.Success && valueMatch.Success)
                {
                    hiddenFields[nameMatch.Groups[1].Value] = valueMatch.Groups[1].Value;
                }
            }

            return hiddenFields;
        }

        private string ExtractFormAction(string html)
        {
            var actionMatch = Regex.Match(html, @"<form[^>]*action=[""']([^""']*)[""']", RegexOptions.IgnoreCase);
            return actionMatch.Success ? actionMatch.Groups[1].Value : "";
        }

        private string ExtractRequestId(string response)
        {
            var jsonMatch = Regex.Match(response, @"""request_id""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (jsonMatch.Success)
                return jsonMatch.Groups[1].Value;

            var fieldMatch = Regex.Match(response, @"request_id[""']\s*value=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (fieldMatch.Success)
                return fieldMatch.Groups[1].Value;

            return "";
        }

        private string ExtractTokenFromUrl(string url)
        {
            var tokenMatch = Regex.Match(url, @"[?&]request_token=([^&]+)", RegexOptions.IgnoreCase);
            return tokenMatch.Success ? Uri.UnescapeDataString(tokenMatch.Groups[1].Value) : "";
        }

        private string ExtractAccessTokenFromResponse(string response)
        {
            var tokenMatch = Regex.Match(response, @"""access_token""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            return tokenMatch.Success ? tokenMatch.Groups[1].Value : "";
        }

        private string ExtractTokenFromResponse(string response)
        {
            // Try to extract request_token from JSON response
            var tokenMatch = Regex.Match(response, @"""request_token""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (tokenMatch.Success)
                return tokenMatch.Groups[1].Value;

            // Try to extract from URL in response content
            var urlMatch = Regex.Match(response, @"request_token=([^&\s""']+)", RegexOptions.IgnoreCase);
            return urlMatch.Success ? Uri.UnescapeDataString(urlMatch.Groups[1].Value) : "";
        }

        private string MaskUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "";
            
            // Mask the request_token parameter value for logging security
            return Regex.Replace(url, @"(request_token=)[^&\s""']+", "$1***", RegexOptions.IgnoreCase);
        }

        private bool IsRedirectStatus(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.MovedPermanently ||
                   statusCode == HttpStatusCode.Found ||
                   statusCode == HttpStatusCode.SeeOther ||
                   statusCode == HttpStatusCode.TemporaryRedirect ||
                   ((int)statusCode >= 300 && (int)statusCode < 400);
        }

        private byte[] Base32Decode(string base32)
        {
            const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            base32 = base32.Replace(" ", "").ToUpper();
            
            var result = new List<byte>();
            var buffer = 0;
            var bufferLength = 0;

            foreach (char c in base32)
            {
                var value = base32Chars.IndexOf(c);
                if (value < 0) continue;

                buffer = (buffer << 5) | value;
                bufferLength += 5;

                if (bufferLength >= 8)
                {
                    result.Add((byte)(buffer >> (bufferLength - 8)));
                    bufferLength -= 8;
                }
            }

            return result.ToArray();
        }

        private string ComputeSha256Hash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = sha256.ComputeHash(bytes);
                
                var sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        #endregion

        #region Local Server Management

        private void StartLocalServer()
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://127.0.0.1:{_localPort}/");
            _httpListener.Start();

            Task.Run(async () =>
            {
                while (_httpListener.IsListening)
                {
                    try
                    {
                        var context = await _httpListener.GetContextAsync();
                        await HandleRedirectRequest(context);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        // Log errors if needed
                    }
                }
            });
        }

        private async Task HandleRedirectRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var query = request.Url.Query;
            if (query.Contains("request_token="))
            {
                var requestToken = ExtractTokenFromUrl(request.Url.ToString());
                if (!string.IsNullOrEmpty(requestToken))
                {
                    _capturedRequestToken = requestToken;
                    try
                    {
                        _tokenCaptured.Set();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Ignore if already disposed
                    }

                    var responseString = @"
                    <html>
                    <head><title>✅ Success!</title></head>
                    <body style='font-family: Arial, sans-serif; text-align: center; padding: 50px;'>
                        <h1 style='color: green;'>✅ Token Captured!</h1>
                        <p>Automated login successful!</p>
                        <script>setTimeout(() => window.close(), 2000);</script>
                    </body>
                    </html>";

                    var buffer = Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html";
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
            }

            response.OutputStream.Close();
        }

        private void StopLocalServer()
        {
            try
            {
                _httpListener?.Stop();
                _httpListener?.Close();
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }

        #endregion

        public void Dispose()
        {
            StopLocalServer();
        }
    }
} 