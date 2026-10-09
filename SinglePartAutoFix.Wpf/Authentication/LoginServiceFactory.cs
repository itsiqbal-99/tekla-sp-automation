using System;
using System.Configuration;

namespace SinglePartAutoFix.Wpf.Authentication
{
    internal static class LoginServiceFactory
    {
        public static ILoginService Create()
        {
            string mode = ConfigurationManager.AppSettings["Auth.Mode"] ?? "Api";
            if (string.Equals(mode, "Api", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return new ApiLoginService();
                }
                catch (ConfigurationErrorsException)
                {
                    return new UnavailableLoginService(
                        "Production authentication is not configured correctly. Contact support.");
                }
            }

#if DEBUG
            if (string.Equals(mode, "Demo", StringComparison.OrdinalIgnoreCase))
            {
                return new DemoLoginService();
            }
#endif

            return new UnavailableLoginService(
                "Production authentication is not configured. Contact support.");
        }

        private sealed class UnavailableLoginService : ILoginService
        {
            private readonly string _message;

            public UnavailableLoginService(string message)
            {
                _message = message;
            }

            public bool IsAuthenticated => false;

            public System.Threading.Tasks.Task<LoginResult> SignInAsync(LoginRequest request)
            {
                return System.Threading.Tasks.Task.FromResult(LoginResult.Failure(_message));
            }
        }
    }
}
