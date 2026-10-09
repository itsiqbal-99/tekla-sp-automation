using SinglePartAutoFix.Infrastructure.Tekla;
using SinglePartAutoFix.Wpf.Authentication;
using SinglePartAutoFix.Wpf.Views;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace SinglePartAutoFix.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly ILoginService _loginService;
        private readonly string _demoUsername;
        private readonly string _demoPassword;
        private readonly TeklaConnectionView _connectionView;

        private TeklaModelSession _teklaSession;
        private bool _isCheckingTekla;
        private bool _isSignedIn;
        private string _displayName;
        private string _modelName;
        private string _modelPath;

        public MainWindow()
        {
            InitializeComponent();

            _loginService = LoginServiceFactory.Create();
            var demoLoginService = _loginService as DemoLoginService;
            _demoUsername = demoLoginService?.Username ?? string.Empty;
            _demoPassword = demoLoginService?.Password ?? string.Empty;

            _teklaSession = new TeklaModelSession();
            _connectionView = new TeklaConnectionView();
            _connectionView.RetryClicked += async (sender, args) => await CheckTeklaAsync(createNewSession: true);
            _connectionView.CloseClicked += (sender, args) => Close();

            Loaded += MainWindow_Loaded;
            Closed += (sender, args) => (_loginService as IDisposable)?.Dispose();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;
            await CheckTeklaAsync(createNewSession: false);
        }

        private async Task CheckTeklaAsync(bool createNewSession)
        {
            if (_isCheckingTekla)
            {
                return;
            }

            _isCheckingTekla = true;
            MainContent.Content = _connectionView;
            _connectionView.ShowChecking();

            try
            {
                // A fresh Tekla Model object is required when Tekla was opened
                // after this application. Reusing the failed object can keep a
                // stale connection state.
                var sessionToCheck = createNewSession
                    ? new TeklaModelSession()
                    : _teklaSession;

                var checkTask = Task.Run(() => CheckSession(sessionToCheck));

                // Keep the animated connection screen visible long enough to be
                // readable without delaying the Tekla check itself.
                await Task.WhenAll(checkTask, Task.Delay(2500));
                var result = checkTask.Result;
                if (!result.IsConnected)
                {
                    ClearModelInformation();
                    _connectionView.ShowConnectionError(result.ErrorMessage);
                    return;
                }

                _teklaSession = sessionToCheck;
                _modelName = result.ModelName;
                _modelPath = result.ModelPath;

                if (_isSignedIn)
                {
                    ShowWorkspace();
                }
                else
                {
                    ShowLogin();
                }
            }
            catch
            {
                ClearModelInformation();
                _connectionView.ShowConnectionError(
                    "Tekla Structures could not be reached. Open Tekla, load a model, then try again.");
            }
            finally
            {
                _isCheckingTekla = false;
            }
        }

        private static TeklaCheckResult CheckSession(TeklaModelSession session)
        {
            try
            {
                if (session == null || !session.IsConnected())
                {
                    return TeklaCheckResult.Failed(
                        "Open Tekla Structures and load a model before continuing.");
                }

                string modelName = session.GetModelName();
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    return TeklaCheckResult.Failed(
                        "Tekla Structures is running, but no model is open.");
                }

                return TeklaCheckResult.Connected(modelName, session.GetModelPath());
            }
            catch
            {
                return TeklaCheckResult.Failed(
                    "Tekla Structures could not be reached. Open a model, then try again.");
            }
        }

        private void ShowLogin()
        {
            var loginView = new LoginView(
                _modelName,
                _demoUsername,
                _demoPassword,
                _loginService is DemoLoginService);
            loginView.SignInSubmitted += SignInSubmitted;
            MainContent.Content = loginView;
        }

        private async void SignInSubmitted(object sender, SignInSubmittedEventArgs e)
        {
            var loginView = sender as LoginView;
            if (loginView == null)
            {
                return;
            }

            loginView.SetBusy(true);

            try
            {
                var teklaCheck = await Task.Run(() => CheckSession(_teklaSession));
                if (!teklaCheck.IsConnected)
                {
                    ClearModelInformation();
                    await CheckTeklaAsync(createNewSession: true);
                    return;
                }

                var loginResult = await _loginService.SignInAsync(e.Login);
                if (!loginResult.Succeeded)
                {
                    loginView.ShowLoginError(loginResult.ErrorMessage);
                    loginView.ClearPassword();
                    return;
                }

                _isSignedIn = true;
                _displayName = loginResult.DisplayName;
                ShowWorkspace();
            }
            catch
            {
                if (ReferenceEquals(MainContent.Content, loginView))
                {
                    loginView.ShowLoginError("Sign in could not be completed. Please try again.");
                }
            }
            finally
            {
                if (ReferenceEquals(MainContent.Content, loginView))
                {
                    loginView.SetBusy(false);
                }
            }
        }

        private void ShowWorkspace()
        {
            var workspace = new DrawingWorkspaceView(
                _teklaSession,
                _displayName,
                _modelPath,
                () => _isSignedIn && _loginService.IsAuthenticated);
            workspace.LogoutClicked += LogoutClicked;
            workspace.TeklaConnectionLost += TeklaConnectionLost;
            MainContent.Content = workspace;
        }

        private async void LogoutClicked(object sender, EventArgs e)
        {
            _isSignedIn = false;
            _displayName = null;
            await CheckTeklaAsync(createNewSession: false);
        }

        private async void TeklaConnectionLost(object sender, EventArgs e)
        {
            await CheckTeklaAsync(createNewSession: true);
        }

        private void ClearModelInformation()
        {
            _modelName = null;
            _modelPath = null;
        }

        private sealed class TeklaCheckResult
        {
            private TeklaCheckResult(bool isConnected, string modelName, string modelPath, string errorMessage)
            {
                IsConnected = isConnected;
                ModelName = modelName;
                ModelPath = modelPath;
                ErrorMessage = errorMessage;
            }

            public bool IsConnected { get; }
            public string ModelName { get; }
            public string ModelPath { get; }
            public string ErrorMessage { get; }

            public static TeklaCheckResult Connected(string modelName, string modelPath)
            {
                return new TeklaCheckResult(true, modelName, modelPath ?? string.Empty, string.Empty);
            }

            public static TeklaCheckResult Failed(string message)
            {
                return new TeklaCheckResult(false, string.Empty, string.Empty, message);
            }
        }
    }
}
