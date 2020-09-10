using Serilog.Events;

namespace PluralKit.Core
{
    public class CoreConfig
    {
        public string Database { get; set; }
        public string SentryUrl { get; set; }
        public string InfluxUrl { get; set; }
        public string InfluxDb { get; set; }
        public string LogDir { get; set; }
        public string ElasticUrl { get; set; }

        public LogEventLevel ConsoleLogLevel { get; set; } = LogEventLevel.Debug;
        public LogEventLevel FileLogLevel { get; set; } = LogEventLevel.Information;
    }
}