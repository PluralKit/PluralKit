using App.Metrics;
using App.Metrics.Gauge;
using App.Metrics.Meter;

namespace PluralKit.Bot
{
    public static class BotMetrics
    {
        public static MeterOptions MessagesReceived => new MeterOptions {Name = "Messages processed", MeasurementUnit = Unit.Events, RateUnit = TimeUnit.Seconds, Context = "Bot"};
        public static MeterOptions MessagesProxied => new MeterOptions {Name = "Messages proxied", MeasurementUnit = Unit.Events, RateUnit = TimeUnit.Seconds, Context = "Bot"};
        public static MeterOptions CommandsRun => new MeterOptions {Name = "Commands run", MeasurementUnit = Unit.Commands, RateUnit = TimeUnit.Seconds, Context = "Bot"};
        public static GaugeOptions MembersOnline => new GaugeOptions {Name = "Members online", MeasurementUnit = Unit.None, Context = "Bot"};
        
        public static GaugeOptions DatabasePoolSize => new GaugeOptions { Name = "Database pool size", Context = "Database" };
    }
}