using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Logging;

namespace PKBank.Desktop;

public static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        // Surface binding/layout warnings on stderr when diagnosing issues.
        if (Environment.GetEnvironmentVariable("PKHEX_AVALONIA_LOG") == "1")
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));

        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(LogEventLevel.Warning);
    }

    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
}
