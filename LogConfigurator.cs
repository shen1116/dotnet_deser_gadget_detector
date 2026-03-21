using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace Project
{
    internal class LogConfigurator
    {
        private static readonly string LogDirectory = "C:\\Users\\Administrator\\Desktop\\TestCode\\LOGS";

        private static readonly LogEventLevel[] Levels =
        [
            LogEventLevel.Debug,
            LogEventLevel.Information,
            LogEventLevel.Warning,
            LogEventLevel.Error,
            LogEventLevel.Fatal
        ];

        public static void Configure()
        {
            var config = new LoggerConfiguration()
                .MinimumLevel.Debug();

            foreach (var level in Levels)
            {
                config = config.WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level == level)
                    .WriteTo.Async(a => a.File(
                        path: Path.Combine(LogDirectory, $"{level.ToString().ToLower()}-log-.txt"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14,
                        fileSizeLimitBytes: 50_000_000,
                        rollOnFileSizeLimit: true,
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                    )));
            }

            Log.Logger = config.CreateLogger();
        }
    }

}
