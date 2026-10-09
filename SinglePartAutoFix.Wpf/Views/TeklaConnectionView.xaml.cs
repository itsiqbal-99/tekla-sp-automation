using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SinglePartAutoFix.Wpf.Views
{
    public partial class TeklaConnectionView : UserControl
    {
        public TeklaConnectionView()
        {
            InitializeComponent();
            ShowChecking();
        }

        public event EventHandler RetryClicked;
        public event EventHandler CloseClicked;

        public void ShowChecking()
        {
            StatusIconText.Text = "…";
            StatusIconBorder.Background = new SolidColorBrush(Color.FromRgb(219, 234, 254));
            StatusIconText.Foreground = (Brush)FindResource("PrimaryBrush");
            StatusTitleText.Text = "Checking connection";
            StatusMessageText.Text = "Looking for an active Tekla Structures model.";
            CheckingProgressBar.Visibility = Visibility.Visible;
            CheckingHintText.Visibility = Visibility.Visible;
            InstructionsPanel.Visibility = Visibility.Collapsed;
            RetryButton.Visibility = Visibility.Collapsed;
            CloseButton.IsEnabled = false;
        }

        public void ShowConnectionError(string message)
        {
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
