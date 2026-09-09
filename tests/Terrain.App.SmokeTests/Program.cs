using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Terrain.App.Views;

namespace Terrain.App.SmokeTests;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => AppBuilder.Configure<SmokeApplication>()
        .UsePlatformDetect()
        .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software] })
        .LogToTrace().StartWithClassicDesktopLifetime(args);
}

internal sealed class SmokeApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            window.Opened += async (_, _) =>
            {
                await window.Ready;
                var directory = new DirectoryInfo(Environment.CurrentDirectory);
                while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Terrain.slnx")))
                    directory = directory.Parent;
                if (directory is null)
                {
                    Console.Error.WriteLine("Run from the repository root.");
                    desktop.Shutdown(1);
                    return;
                }
                int exitCode = await new SmokeTestRunner().RunAsync(window, directory.FullName);
                desktop.Shutdown(exitCode);
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
