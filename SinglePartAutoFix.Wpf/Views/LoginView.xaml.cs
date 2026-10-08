using SinglePartAutoFix.Wpf.Authentication;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SinglePartAutoFix.Wpf.Views
{
    internal sealed class SignInSubmittedEventArgs : EventArgs
    {
        public SignInSubmittedEventArgs(LoginRequest login)
        {
            Login = login;
        }

        public LoginRequest Login { get; }
    }

    public partial class LoginView : UserControl
    {
        private bool _syncingPassword;

        public LoginView(string modelName, string demoUsername, string demoPassword)
        {
            InitializeComponent();
            ModelNameText.Text = string.IsNullOrWhiteSpace(modelName) ? "Active model" : modelName;
            DemoCredentialsText.Text = $"Username: {demoUsername}   |   Password: {demoPassword}";
            Loaded += (sender, args) => UsernameTextBox.Focus();
        }

        internal event EventHandler<SignInSubmittedEventArgs> SignInSubmitted;

        internal void SetBusy(bool isBusy)
        {
            UsernameTextBox.IsEnabled = !isBusy;
            PasswordInput.IsEnabled = !isBusy;
            VisiblePasswordInput.IsEnabled = !isBusy;
            ShowPasswordCheckBox.IsEnabled = !isBusy;
            SignInButton.IsEnabled = !isBusy;
            SignInButton.Content = isBusy ? "Signing In..." : "Sign In";
            Cursor = isBusy ? Cursors.Wait : null;
        }

        internal void ShowLoginError(string message)
        {
            ErrorText.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;
        }

        internal void ClearPassword()
        {
            PasswordInput.Password = string.Empty;
            VisiblePasswordInput.Text = string.Empty;
            PasswordInput.Focus();
        }

        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            SubmitSignIn();
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            SubmitSignIn();
        }

        private void SubmitSignIn()
        {
            ErrorBorder.Visibility = Visibility.Collapsed;

            var login = new LoginRequest
            {
                Username = UsernameTextBox.Text.Trim(),
                Password = GetEnteredPassword()
            };

            if (string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrEmpty(login.Password))
            {
                ShowLoginError("Enter your username and password.");
                return;
            }

            SignInSubmitted?.Invoke(this, new SignInSubmittedEventArgs(login));
        }

        private string GetEnteredPassword()
        {
            return ShowPasswordCheckBox.IsChecked == true
                ? VisiblePasswordInput.Text
                : PasswordInput.Password;
        }

        private void ShowPasswordCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            bool showPassword = ShowPasswordCheckBox.IsChecked == true;

            if (showPassword)
            {
                VisiblePasswordInput.Text = PasswordInput.Password;
                PasswordInput.Visibility = Visibility.Collapsed;
                VisiblePasswordInput.Visibility = Visibility.Visible;
                VisiblePasswordInput.Focus();
                VisiblePasswordInput.CaretIndex = VisiblePasswordInput.Text.Length;
                return;
            }

            PasswordInput.Password = VisiblePasswordInput.Text;
            VisiblePasswordInput.Visibility = Visibility.Collapsed;
            PasswordInput.Visibility = Visibility.Visible;
            PasswordInput.Focus();
        }

        private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_syncingPassword)
            {
                return;
            }

            _syncingPassword = true;
            VisiblePasswordInput.Text = PasswordInput.Password;
            _syncingPassword = false;
        }

        private void VisiblePasswordInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingPassword)
            {
                return;
            }

            _syncingPassword = true;
            PasswordInput.Password = VisiblePasswordInput.Text;
            _syncingPassword = false;
        }
    }
}
