using Avalonia.Interactivity;
using Avalonia.Controls;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace AutoLogout;

public partial class FirstTimeSetup : Window
{
    private readonly List<ConditionalValue<TabItem>> nextStep;

    public string Version { get {
            var result = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "1.0.0";
            if(result.IndexOf('+')>0)
                result = result[..result.IndexOf('+')];
            return "v"+result;
        } }
    
    private bool setupComplete = false;
    public FirstTimeSetup()
    {
        InitializeComponent();
        DataContext = this;

        // Create criteria for the order in which tabs are unlocked
        nextStep = [
            new ConditionalValue<TabItem>(() => State.Current.Store.hashedPassword == "", TabPassword),
            new ConditionalValue<TabItem>(() => !OS.Current.AutoStart, TabAutoStart),
            new ConditionalValue<TabItem>(() => State.Current.RemainingTime is null, TabLimits),
            new ConditionalValue<TabItem>(() => !State.Current.Store.Online, TabSync)
        ];
    }

    private void RevealNextTab()
    {
        /// Unhides a tab and switches to it
        
        var resolved = false;
        foreach (var step in nextStep)
        {
            if(!step.Cleared && step.Condition())
            {
                step.Cleared = true;
                var tab = step.Value;
                tab.Styles.Clear();
                tab.InvalidateVisual();
                tabControl.SelectedItem = tab;

                resolved = true;
                break;
            }
        }

        // If all setup conditions are set/cleared, show the done tab
        if(!resolved) {
            setupComplete = true;
            tabControl.SelectedItem = TabDone;
        }
    }

    private void AboutButton_Click(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow();
        aboutWindow.ShowDialog(this);
    }
    private async void UnsupervisedButton_Click(object? sender, RoutedEventArgs e)
    {
        var alert = new AlertDialog(
            "If you want this account to remain unsupervised, nothing more needs to be done here. Open AutoLogout in an account you want to supervise to continue.",
            "AutoLogout"
        );
        await alert.ShowDialog(this);
        State.Current.Intent = UserIntent.Exit;
        Close();
    }
    private void NextButton_Click(object? sender, RoutedEventArgs e)
    {
        RevealNextTab();
    }
    private async void ChangePasswordButton_Click(object? sender, RoutedEventArgs e)
    {
        var controlPanel = new ControlPanel();
        var result = await controlPanel.ChangePassword(this);
        if(result)
            RevealNextTab();
    }
    private async void AutoStartButton_Click(object? sender, RoutedEventArgs e)
    {
        var controlPanel = new ControlPanel();
        var result = await controlPanel.SetAutoStart(true, this);
        if(result)
            RevealNextTab();
    }
    private async void LimitsButton_Click(object? sender, RoutedEventArgs e)
    {
        var controlPanel = new ControlPanel("TabLimits");
        await controlPanel.ShowDialog(this);
        RevealNextTab();
    }
    private async void SyncButton_Click(object? sender, RoutedEventArgs e)
    {
        var controlPanel = new ControlPanel("TabSync");
        await controlPanel.ShowDialog(this);
        RevealNextTab();
    }
    private void FinishButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if(State.Current.Intent == UserIntent.Exit)
        {
            // No further action needed, MainWindow will cascade
        }
        else if (!setupComplete)
        {
            e.Cancel = true;
            var confirm = new ConfirmDialog("You haven't completed setup, AutoLogout will not run on this account. Continue?", "AutoLogout");
            await confirm.ShowDialog(this);
            if (confirm.Result ?? false)
            {
                // AutoLogout will close, ensure the parent password isn't set as this is the setup flag
                State.Current.Store.hashedPassword = "";
                await OS.Current.SaveState();
                State.Current.Intent = UserIntent.Exit;
                Close();
            }
        }
        else
        {
            // Exit setup mode and return control to MainWindow
            State.Current.Intent = UserIntent.None;
        }
    }
}