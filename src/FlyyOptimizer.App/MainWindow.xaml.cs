using System.Windows;
using System.ComponentModel;

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
