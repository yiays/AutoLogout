using Avalonia.Controls;
using System.Reflection;

namespace AutoLogout;

public partial class AboutWindow : Window
{
    public string Version { get => "v"+Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0"; }
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = this;
    }
}