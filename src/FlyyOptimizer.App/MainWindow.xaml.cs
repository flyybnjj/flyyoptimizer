using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FlyyOptimizer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel model;
    private bool closingRequested;
    private bool readyToClose;
    public MainWindow(MainViewModel model)
    {
        this.model = model;
        InitializeComponent();
        DataContext = model;
        Closing += HandleClosing;
        model.SectionNavigationRequested += NavigateToSection;
        Closed += (_, _) => model.SectionNavigationRequested -= NavigateToSection;
    }

    private void NavigateToSection(string name)
    {
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (closingRequested || FindName(name) is not FrameworkElement section) return;
            UpdateLayout();
            if (!section.IsVisible) return;
            section.BringIntoView();
            if (IsActive && section is Panel panel && panel.Children.OfType<Control>().FirstOrDefault(c => c.Focusable && c.IsEnabled) is { } control)
                control.Focus();
        }));
    }

    private async void HandleClosing(object? sender, CancelEventArgs args)
    {
        if (readyToClose) return;
        args.Cancel = true;
        if (closingRequested) return;
        closingRequested = true;
        IsEnabled = false;
        await model.PrepareToCloseAsync();
        readyToClose = true;
        // Even an idle close must run after the current Closing event has returned.
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }
}
