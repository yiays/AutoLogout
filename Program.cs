using System.Reflection;
using Avalonia;

namespace AutoLogout;

internal class Program
{
    [STAThread]
    public static void Main(string[] args){
      /*// Ensure the base directory is correct
      Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
      AppDomain.CurrentDomain.SetData("PROBING_PRIVATE_PATHS", "Libraries");

      // Help the runtime find library files that have been moved to .\Libraries\
      AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
      {
          var assemblyName = new AssemblyName(args.Name).Name + ".dll";
          var probePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libraries", assemblyName);
          return File.Exists(probePath) ? Assembly.LoadFrom(probePath) : null;
      };
      */
      BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}