using Serilog;
using Serilog.Events;
using Serilog.Sinks.Spectre;

namespace libpeerlinker.Utility;

public static class Logger
{
    public static ILogger Instance { get; } = new LoggerConfiguration()
        #if DEBUG
        .MinimumLevel.Verbose()
        #endif
        .WriteTo.Spectre()
        .CreateLogger();
}
