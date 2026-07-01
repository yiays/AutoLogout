using Avalonia;

namespace AutoLogout {
    public class Prompt
    {
        public required string text;
        public required string caption;
        public bool sensitive = false;
        public string? Show()
        {
            var prompt = new PromptDialog(text, caption, sensitive);
            var mainWindow = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow is not null)
                prompt.ShowDialog(mainWindow);
            return prompt.Result;
        }
    }
}
