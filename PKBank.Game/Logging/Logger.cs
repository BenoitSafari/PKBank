using Microsoft.Extensions.Logging;

namespace PKBank.Game.Logging;

public static class Logger
{
    private static readonly ILoggerFactory LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
    {
        builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddConsole();
    });

    private static readonly ILogger _logger = LoggerFactory.CreateLogger("Main");

    public static void Trace(string? str) => _logger.Log(LogLevel.Trace, str);
}
