using SinglePartAutoFix.Application.Services;
using SinglePartAutoFix.Infrastructure.Logging;
using SinglePartAutoFix.Infrastructure.Tekla;
using SinglePartAutoFix.Wpf.ViewModels;
using System.Windows;

namespace SinglePartAutoFix.Wpf
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var session = new TeklaModelSession();
            var checker = new TeklaDrawingChecker(session);

            var service = new SinglePartAutomationService(
                session,
                new TeklaPartReader(session),
                new DrawingProcessor(
                    checker,
                    new TeklaDrawingCreator(session)),
                new SimpleFileLogger());

            var window = new MainWindow
            {
                DataContext = new MainViewModel(service)
            };

            window.Show();
        }
    }
}