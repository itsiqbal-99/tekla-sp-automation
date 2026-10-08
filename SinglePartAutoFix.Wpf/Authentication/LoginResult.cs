namespace SinglePartAutoFix.Wpf.Authentication
{
    internal sealed class LoginResult
    {
        private LoginResult(bool succeeded, string displayName, string errorMessage)
        {
            Succeeded = succeeded;
            DisplayName = displayName;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded { get; }
        public string DisplayName { get; }
        public string ErrorMessage { get; }

        public static LoginResult Success(string displayName)
        {
            return new LoginResult(true, displayName, string.Empty);
        }

        public static LoginResult Failure(string message)
        {
            return new LoginResult(false, string.Empty, message);
        }
    }
}
