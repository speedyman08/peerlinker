using Serilog;
using Serilog.Sinks.SpectreConsole;

namespace libpeerlinker.Utility;

public static class Logger
{
    public static ILogger Instance { get; } = new LoggerConfiguration()
        .WriteTo.SpectreConsole()
        .CreateLogger();
}