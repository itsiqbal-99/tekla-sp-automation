using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Application.Services;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.Infrastructure.Tekla;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace SinglePartAutoFix.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly TeklaModelSession _teklaSession;
        private readonly TeklaPartReader _partReader;
        private readonly DrawingCandidateBuilder _candidateBuilder;
        private List<DrawingCandidate> _drawingCandidates;
        public MainWindow()
        {
            InitializeComponent();
            _teklaSession = new TeklaModelSession();
            _partReader = new TeklaPartReader(_teklaSession);
            _candidateBuilder = new DrawingCandidateBuilder();
            _drawingCandidates = new List<DrawingCandidate>();

            //ShowTeklaRuntimeInfo();
            UpdateTeklaConnectionStatus();

        }

        private void UpdateTeklaConnectionStatus()
        {
            try
            {
                bool isConnected = _teklaSession.IsConnected();
                if (isConnected)
                {
                    TeklaStatusText.Text = "Tekla Connected";
                    TeklaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                } else
                {
                    TeklaStatusText.Text = "Tekla Not Connected";
                    TeklaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(156,163,175));

                    MessageBox.Show(
                        "TeklaModelSession was created successfully, but GetConnectionStatus() returned false", "Tekla Connection Diagnostic", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            } catch (System.Exception ex)
            {
                TeklaStatusText.Text = $"Error: {ex.Message}";
                TeklaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(156,163,175));

                MessageBox.Show(
                       ex.ToString(),"Tekla Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }

        private void SelectPartsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(!_teklaSession.IsConnected())
                {
                    SelectionTitleText.Text = "Tekla is Not Connected";
                    SelectionDetailText.Text = "Open Tekla Structures and load a model first.";
                    UpdateTeklaConnectionStatus();
                    return;
                }

                var query = new PartQuery
                {
                    SelectionMode = PartSelectionMode.Selected
                };

                var parts = _partReader.GetParts(query);
                _drawingCandidates = _candidateBuilder.Build(parts);
                CandidateDataGrid.ItemsSource = _drawingCandidates;

                if (parts.Count == 0)
                {
                    SelectionTitleText.Text = "No Parts Selected";
                    SelectionDetailText.Text = "Please select parts in Tekla Structures and try again.";
                    return;
                }

                SelectionTitleText.Text = $"{_drawingCandidates.Count} drawing candidate(s)";

                SelectionDetailText.Text = $"Selected physical parts: {parts.Count}";

            } catch (System.Exception ex)
            {
                SelectionTitleText.Text = "Unable to read selected parts";
                SelectionDetailText.Text = ex.Message;
            }
        }

        private void ShowTeklaRuntimeInfo()
        {
            var teklaAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Tekla.Structures.Model");

            string assemblyInfo = teklaAssembly == null
            ? "Tekla.Structures.Model is not loaded"
            : $"{teklaAssembly.FullName}\n\nLoaded From:\n{teklaAssembly.Location}";

            MessageBox.Show(
                $"Process 64-bit: {Environment.Is64BitProcess}\n\n" +
                $"Tekla Assembly:\n{assemblyInfo}",
                "Tekla Runtime Diagnostic",
                MessageBoxButton.OK,
                MessageBoxImage.Information);


        }
    }
}
