using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SinglePartAutoFix.Wpf.Views
{
    public partial class TeklaConnectionView : UserControl
    {
        private readonly DispatcherTimer _checkingAnimationTimer;
        private int _checkingDotCount;

        public TeklaConnectionView()
        {
            InitializeComponent();
            _checkingAnimationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _checkingAnimationTimer.Tick += CheckingAnimationTimer_Tick;
            Unloaded += (sender, args) => _checkingAnimationTimer.Stop();
            ShowChecking();
        }

        public event EventHandler RetryClicked;
        public event EventHandler CloseClicked;

        public void ShowChecking()
        {
            _checkingDotCount = 1;
            StatusIconText.Text = ".";
            StatusIconBorder.Background = new SolidColorBrush(Color.FromRgb(219, 234, 254));
            StatusIconText.Foreground = (Brush)FindResource("PrimaryBrush");
            StatusTitleText.Text = "Checking connection";
            StatusMessageText.Text = "Looking for an active Tekla Structures model.";
            CheckingProgressBar.Visibility = Visibility.Visible;
            CheckingHintText.Visibility = Visibility.Visible;
            InstructionsPanel.Visibility = Visibility.Collapsed;
            RetryButton.Visibility = Visibility.Collapsed;
            CloseButton.IsEnabled = false;
            _checkingAnimationTimer.Stop();
            _checkingAnimationTimer.Start();
        }

        public void ShowConnectionError(string message)
        {
            _checkingAnimationTimer.Stop();
            StatusIconText.Text = "!";
            StatusIconBorder.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
            StatusIconText.Foreground = (Brush)FindResource("WarningBrush");
            StatusTitleText.Text = "Tekla is not connected";
            StatusMessageText.Text = string.IsNullOrWhiteSpace(message)
                ? "Open Tekla Structures and load a model before continuing."
                : message;
            CheckingProgressBar.Visibility = Visibility.Collapsed;
            CheckingHintText.Visibility = Visibility.Collapsed;
            InstructionsPanel.Visibility = Visibility.Visible;
            RetryButton.Visibility = Visibility.Visible;
            CloseButton.IsEnabled = true;
        }

        private void CheckingAnimationTimer_Tick(object sender, EventArgs e)
        {
            _checkingDotCount = _checkingDotCount % 3 + 1;
            StatusIconText.Text = new string('.', _checkingDotCount);
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            RetryClicked?.Invoke(this, EventArgs.Empty);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
