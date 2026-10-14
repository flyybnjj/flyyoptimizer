using System.Windows;

namespace FlyyOptimizer.App;

public partial class OptimizationConfirmation : Window
{
    public OptimizationConfirmation() => InitializeComponent();
    private void Accept(object sender, RoutedEventArgs args) => DialogResult = true;
    internal static bool Confirm()
    {
        var dialog = new OptimizationConfirmation { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true;
    }
}
