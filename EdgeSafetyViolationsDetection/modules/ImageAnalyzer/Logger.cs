namespace ImageAnalyzer
{
    using System;
    using System.Linq;
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;

    public class ConsoleLoggerConfiguration
    {
        public int EventId { get; set; } = 0;
        public LogLevel LogLevel { get; set; } = LogLevel.Information;
        public ConsoleColor Color { get; } = ConsoleColor.Yellow;
        public class LogLevelColor
        {
            public LogLevel LogLevel { get; set; }
            public ConsoleColor Color { get; set; }

            public LogLevelColor(LogLevel logLevel, ConsoleColor color)
            {
                this.LogLevel = logLevel;
                this.Color = color;
            }
        }

        private static LogLevelColor[] _consoleColorTable = new LogLevelColor[]
        {
                new LogLevelColor(LogLevel.Trace, ConsoleColor.Gray),
                new LogLevelColor(LogLevel.Debug, ConsoleColor.Cyan),
                new LogLevelColor(LogLevel.Information, ConsoleColor.Green),
                new LogLevelColor(LogLevel.Warning, ConsoleColor.Yellow),
                new LogLevelColor(LogLevel.Error, ConsoleColor.Red),
                new LogLevelColor(LogLevel.Critical, ConsoleColor.DarkRed),
        };

        public ConsoleLoggerConfiguration(int eventId = 0, LogLevel logLevel = LogLevel.Information)
        {
            this.EventId = eventId;
            this.LogLevel = logLevel;
        }

        public ConsoleColor GetConsoleColor(LogLevel logLevel)
        {
            return _consoleColorTable.Where(x => x.LogLevel == logLevel).First().Color;
        }
    }

    public class ConsoleLogger : ILogger
    {
        private readonly string _name;
        private readonly ConsoleLoggerConfiguration _config;

        public ConsoleLogger(string name, ConsoleLoggerConfiguration config)
        {
            _name = name;
            _config = config;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _config.LogLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (_config.EventId == 0 || _config.EventId == eventId.Id)
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = _config.GetConsoleColor(logLevel);
                Console.WriteLine($"{logLevel.ToString()} - {formatter(state, exception)}");
                Console.ForegroundColor = color;
            }
        }
    }

    public class ConsoleLoggerProvider : ILoggerProvider
    {
        private readonly ConsoleLoggerConfiguration _config;
        private readonly ConcurrentDictionary<string, ConsoleLogger> _loggers = new ConcurrentDictionary<string, ConsoleLogger>();

        public ConsoleLoggerProvider(ConsoleLoggerConfiguration config)
        {
            _config = config;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new ConsoleLogger(name, _config));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }

    public static class ConsoleLoggerExtensions
    {
        public static ILoggerFactory AddConsoleLogger(this ILoggerFactory loggerFactory, ConsoleLoggerConfiguration config)
        {
            loggerFactory.AddProvider(new ConsoleLoggerProvider(config));
            return loggerFactory;
        }
        public static ILoggerFactory AddConsoleLogger(this ILoggerFactory loggerFactory)
        {
            var config = new ConsoleLoggerConfiguration();
            return loggerFactory.AddConsoleLogger(config);
        }
        public static ILoggerFactory AddConsoleLogger(this ILoggerFactory loggerFactory, Action<ConsoleLoggerConfiguration> configure)
        {
            var config = new ConsoleLoggerConfiguration();
            configure(config);
            return loggerFactory.AddConsoleLogger(config);
        }
    }
}