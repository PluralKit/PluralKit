using App.Metrics;
using App.Metrics.Gauge;
using App.Metrics.Meter;
using App.Metrics.Timer;

namespace PluralKit.Bot;

public static class BotMetrics
{
    public static MeterOptions MessagesReceived => new()
    {
        Name = "Messages processed",
        MeasurementUnit = Unit.Events,
        RateUnit = TimeUnit.Seconds,
        Context = "Bot"
    };

    public static MeterOptions MessagesProxied => new()
    {
        Name = "Messages proxied",
        MeasurementUnit = Unit.Events,
        RateUnit = TimeUnit.Seconds,
        Context = "Bot"
    };

    public static MeterOptions CommandsRun => new()
    {
        Name = "Commands run",
        MeasurementUnit = Unit.Commands,
        RateUnit = TimeUnit.Seconds,
        Context = "Bot"
    };

    public static TimerOptions CommandTime => new()
    {
        Name = "Command run time",
        MeasurementUnit = Unit.Commands,
        RateUnit = TimeUnit.Seconds,
        DurationUnit = TimeUnit.Seconds,
        Context = "Bot"
    };

    public static GaugeOptions MembersTotal => new()
    {
        Name = "Members total",
        MeasurementUnit = Unit.None,
        Context = "Bot"
    };

    public static GaugeOptions MembersOnline => new()
    {
        Name = "Members online",
        MeasurementUnit = Unit.None,
        Context = "Bot"
    };

    public static GaugeOptions Guilds => new()
    {
        Name = "Guilds",
        MeasurementUnit = Unit.None,
        Context = "Bot"
    };
    public static GaugeOptions Channels => new()
    {
        Name = "Channels",
        MeasurementUnit = Unit.None,
        Context = "Bot"
    };

    public static GaugeOptions ShardLatency => new()
    {
        Name = "Shard Latency",
        Context = "Bot"
    };

    public static GaugeOptions ShardsConnected => new()
    {
        Name = "Shards Connected",
        Context = "Bot",
        MeasurementUnit = Unit.Connections
    };

    public static MeterOptions WebhookCacheMisses => new()
    {
        Name = "Webhook cache misses",
        Context = "Bot",
        MeasurementUnit = Unit.Calls
    };

    public static GaugeOptions WebhookCacheSize => new()
    {
        Name = "Webhook Cache Size",
        Context = "Bot",
        MeasurementUnit = Unit.Items
    };

    public static TimerOptions WebhookResponseTime => new()
    {
        Name = "Webhook Response Time",
        Context = "Bot",
        RateUnit = TimeUnit.Seconds,
        MeasurementUnit = Unit.Requests,
        DurationUnit = TimeUnit.Seconds
    };

    public static TimerOptions MessageContextQueryTime => new()
    {
        Name = "Message context query duration",
        Context = "Bot",
        RateUnit = TimeUnit.Seconds,
        DurationUnit = TimeUnit.Seconds,
        MeasurementUnit = Unit.Calls
    };

    public static TimerOptions ProxyMembersQueryTime => new()
    {
        Name = "Proxy member query duration",
        Context = "Bot",
        RateUnit = TimeUnit.Seconds,
        DurationUnit = TimeUnit.Seconds,
        MeasurementUnit = Unit.Calls
    };

    public static TimerOptions DiscordApiRequests => new()
    {
        Name = "Discord API requests",
        MeasurementUnit = Unit.Requests,
        DurationUnit = TimeUnit.Milliseconds,
        Context = "Bot"
    };

    public static MeterOptions BotErrors => new()
    {
        Name = "Bot errors",
        MeasurementUnit = Unit.Errors,
        RateUnit = TimeUnit.Seconds,
        Context = "Bot"
    };

    public static MeterOptions ErrorMessagesSent => new()
    {
        Name = "Error messages sent",
        MeasurementUnit = Unit.Errors,
        RateUnit = TimeUnit.Seconds,
        Context = "Bot"
    };

    public static TimerOptions EventsHandled => new()
    {
        Name = "Events handled",
        MeasurementUnit = Unit.Errors,
        RateUnit = TimeUnit.Seconds,
        DurationUnit = TimeUnit.Seconds,
        Context = "Bot"
    };
}