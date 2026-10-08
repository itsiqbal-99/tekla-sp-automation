using System;
using System.Configuration;
using System.Threading.Tasks;

namespace SinglePartAutoFix.Wpf.Authentication
{
    internal sealed class DemoLoginService : ILoginService
    {
        public DemoLoginService()
        {
            Username = ReadSetting("DummyAuth.Username", "engineer");
            Password = ReadSetting("DummyAuth.Password", "demo123");
            DisplayName = ReadSetting("DummyAuth.DisplayName", "Demo Engineer");
        }

        public string Username { get; }
        public string Password { get; }
        public string DisplayName { get; }

        public Task<LoginResult> SignInAsync(LoginRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrEmpty(request.Password))
            {
                return Task.FromResult(LoginResult.Failure("Enter your username and password."));
            }

            bool credentialsMatch =
                string.Equals(request.Username.Trim(), Username, StringComparison.Ordinal) &&
                string.Equals(request.Password, Password, StringComparison.Ordinal);

            return Task.FromResult(credentialsMatch
                ? LoginResult.Success(DisplayName)
                : LoginResult.Failure("The username or password is incorrect."));
        }

        private static string ReadSetting(string key, string defaultValue)
        {
            string configuredValue = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(configuredValue) ? defaultValue : configuredValue;
        }
    }
}
