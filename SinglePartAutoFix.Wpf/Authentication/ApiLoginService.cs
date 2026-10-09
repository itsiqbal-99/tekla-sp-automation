using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SinglePartAutoFix.Wpf.Authentication
{
    internal sealed class ApiLoginService : ILoginService, IDisposable
    {
        private readonly HttpClient _client;
        private readonly string _loginPath;
        public bool IsAuthenticated { get; private set; }

        public ApiLoginService()
        {
            string baseUrl = ConfigurationManager.AppSettings["Auth.Api.BaseUrl"];
            _loginPath = ConfigurationManager.AppSettings["Auth.Api.LoginPath"] ?? "api/auth/login";

            Uri baseUri;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri) ||
                !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigurationErrorsException(
                    "Auth.Api.BaseUrl must be configured with an HTTPS URL.");
            }

            int timeoutSeconds;
            if (!int.TryParse(ConfigurationManager.AppSettings["Auth.Api.TimeoutSeconds"], out timeoutSeconds))
            {
                timeoutSeconds = 15;
            }

            _client = new HttpClient
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromSeconds(Math.Max(3, timeoutSeconds))
            };
        }

        public async Task<LoginResult> SignInAsync(LoginRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrEmpty(request.Password))
            {
                return LoginResult.Failure("Enter your username and password.");
            }

            try
            {
                string json = JsonConvert.SerializeObject(new
                {
                    username = request.Username.Trim(),
                    password = request.Password
                });

                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var response = await _client.PostAsync(_loginPath, content))
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    JObject payload = TryParseObject(responseJson);

                    if (!response.IsSuccessStatusCode)
                    {
                        IsAuthenticated = false;
                        return LoginResult.Failure(
                            ReadString(payload, "errorMessage", "message") ??
                            "Sign in was rejected by the company authentication service.");
                    }

                    string displayName = ReadString(payload, "displayName", "name", "username");
                    IsAuthenticated = true;
                    return LoginResult.Success(
                        string.IsNullOrWhiteSpace(displayName) ? request.Username.Trim() : displayName);
                }
            }
            catch (TaskCanceledException)
            {
                IsAuthenticated = false;
                return LoginResult.Failure(
                    "The authentication service did not respond in time. Please try again.");
            }
            catch (HttpRequestException)
            {
                IsAuthenticated = false;
                return LoginResult.Failure(
                    "The authentication service is unavailable. Check the network connection and try again.");
            }
            catch (Exception)
            {
                IsAuthenticated = false;
                return LoginResult.Failure(
                    "Sign in could not be completed. Contact support if the problem continues.");
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private static JObject TryParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JObject.Parse(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string ReadString(JObject payload, params string[] names)
        {
            if (payload == null)
            {
                return null;
            }

            foreach (string name in names)
            {
                JToken value = payload.GetValue(name, StringComparison.OrdinalIgnoreCase);
                if (value != null && value.Type != JTokenType.Null)
                {
                    return value.ToString();
                }
            }

            return null;
        }
    }
}
