namespace AutoLogout;
using System.Reflection;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Help the runtime find library files that have been moved to .\Libraries\
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var assemblyName = new AssemblyName(args.Name).Name + ".dll";
            var probePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libraries", assemblyName);
            return File.Exists(probePath) ? Assembly.LoadFrom(probePath) : null;
        };

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new CountdownTimer());
    }    
}