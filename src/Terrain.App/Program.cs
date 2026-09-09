using Avalonia;

namespace Terrain.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => AppBuilder.Configure<TerrainApplication>()
        .UsePlatformDetect()
        .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software] })
        .LogToTrace().StartWithClassicDesktopLifetime(args);
}
