using Avalonia.Controls;

namespace AutoLogout;

public partial class FirstTimeSetup : Window
{
    public FirstTimeSetup()
    {
        InitializeComponent();

        var ContinueButton = this.FindControl<Button>("ContinueButton");
        var CancelButton = this.FindControl<Button>("CancelButton");

        ContinueButton.Click += ContinueButton_Click;
        CancelButton.Click += CancelButton_Click;
    }

    private void ContinueButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Common.RelaunchAsAdmin("--register");
        Close();
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Common.Relaunch("--skipsetup");
    }
}