using Avalonia.Controls;
using System.Reflection;

namespace AutoLogout;

public partial class AboutWindow : Window
{
    public string Version { get {
            var result = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "1.0.0";
            if(result.IndexOf('+')>0)
                result = result[..result.IndexOf('+')];
            return "v"+result;
        } }
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = this;
    }
}