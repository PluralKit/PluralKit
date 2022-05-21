using App.Metrics;
using App.Metrics.Gauge;
using App.Metrics.Meter;
using App.Metrics.Timer;

namespace PluralKit.Core;

public static class CoreMetrics
{
    public static GaugeOptions SystemCount => new()
    {
        Name = "Systems",
        MeasurementUnit = Unit.Items
    };

    public static GaugeOptions MemberCount => new()
    {
        Name = "Members",
        MeasurementUnit = Unit.Items
    };

    public static GaugeOptions MessageCount => new()
    {
        Name = "Messages",
        MeasurementUnit = Unit.Items
    };

    public static GaugeOptions SwitchCount => new()
    {
        Name = "Switches",
        MeasurementUnit = Unit.Items
    };

    public static GaugeOptions GroupCount => new()
    {
        Name = "Groups",
        MeasurementUnit = Unit.Items
    };

    public static GaugeOptions ProcessPhysicalMemory => new()
    {
        Name = "Process Physical Memory",
        MeasurementUnit = Unit.Bytes,
        Context = "Process"
    };

    public static GaugeOptions ProcessVirtualMemory => new()
    {
        Name = "Process Virtual Memory",
        MeasurementUnit = Unit.Bytes,
        Context = "Process"
    };

    public static GaugeOptions ProcessPrivateMemory => new()
    {
        Name = "Process Private Memory",
        MeasurementUnit = Unit.Bytes,
        Context = "Process"
    };

    public static GaugeOptions ProcessThreads => new()
    {
        Name = "Process Thread Count",
        MeasurementUnit = Unit.Threads,
        Context = "Process"
    };

    public static GaugeOptions ProcessHandles => new()
    {
        Name = "Process Handle Count",
        MeasurementUnit = Unit.Items,
        Context = "Process"
    };

    public static GaugeOptions CpuUsage => new()
    {
        Name = "CPU Usage",
        MeasurementUnit = Unit.Percent,
        Context = "Process"
    };

    public static MeterOptions DatabaseRequests => new()
    {
        Name = "Database Requests",
        MeasurementUnit = Unit.Requests,
        Context = "Database",
        RateUnit = TimeUnit.Seconds
    };

    public static TimerOptions DatabaseQuery => new()
    {
        Name = "Database Query",
        MeasurementUnit = Unit.Requests,
        DurationUnit = TimeUnit.Seconds,
        RateUnit = TimeUnit.Seconds,
        Context = "Database"
    };

    public static GaugeOptions DatabaseConnections => new()
    {
        Name = "Database Connections",
        MeasurementUnit = Unit.Connections,
        Context = "Database"
    };
    public static GaugeOptions DatabaseConnectionsByCluster => new()
    {
        Name = "Database Connections by Cluster",
        MeasurementUnit = Unit.Connections,
        Context = "Database"
    };
}

public record ClusterMetricInfo
{
    public int GuildCount;
    public int ChannelCount;
    public int DatabaseConnectionCount;
    public int WebhookCacheSize;
}