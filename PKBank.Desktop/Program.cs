using System;
using System.Diagnostics;
using Avalonia;

namespace PKBank.Desktop;

public static class Program
{
    public static AppBuilder BuildAvaloniaApp()
    {
        if (Environment.GetEnvironmentVariable("PKHEX_AVALONIA_LOG") == "1")
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));

        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
}
