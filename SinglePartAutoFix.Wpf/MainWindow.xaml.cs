using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Wpf.ViewModels;
using System.Windows;

namespace SinglePartAutoFix.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var viewModel = e.NewValue as MainViewModel;
            if (viewModel == null) return;
            viewModel.ShowMessage = (title, message) => MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            viewModel.ConfirmBatch = ConfirmBatch;
        }

        private bool ConfirmBatch(BatchPlan plan)
        {
            string message = $"Generate {plan.Candidates.Count} Single Part Drawings?\n\n" +
                             $"Selected ready candidates: {plan.SelectedReadyCount}\n" +
                             $"Batch limit: {plan.BatchLimit}\n" +
                             $"Drawings to create: {plan.Candidates.Count}";
            return MessageBox.Show(this, message, "Confirm Controlled Batch", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
        }
    }
}
