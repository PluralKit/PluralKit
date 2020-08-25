using App.Metrics;
using App.Metrics.Gauge;
using App.Metrics.Meter;
using App.Metrics.Timer;

namespace PluralKit.Core
{
    public static class CoreMetrics
    {
        public static GaugeOptions SystemCount => new GaugeOptions { Name = "Systems", MeasurementUnit = Unit.Items };
        public static GaugeOptions MemberCount => new GaugeOptions { Name = "Members", MeasurementUnit = Unit.Items };
        public static GaugeOptions MessageCount => new GaugeOptions { Name = "Messages", MeasurementUnit = Unit.Items };
        public static GaugeOptions SwitchCount => new GaugeOptions { Name = "Switches", MeasurementUnit = Unit.Items };
        public static GaugeOptions GroupCount => new GaugeOptions { Name = "Groups", MeasurementUnit = Unit.Items };
        
        public static GaugeOptions ProcessPhysicalMemory => new GaugeOptions { Name = "Process Physical Memory", MeasurementUnit = Unit.Bytes, Context = "Process" };
        public static GaugeOptions ProcessVirtualMemory => new GaugeOptions { Name = "Process Virtual Memory", MeasurementUnit = Unit.Bytes, Context = "Process" };
        public static GaugeOptions ProcessPrivateMemory => new GaugeOptions { Name = "Process Private Memory", MeasurementUnit = Unit.Bytes, Context = "Process" };
        public static GaugeOptions ProcessThreads => new GaugeOptions { Name = "Process Thread Count", MeasurementUnit = Unit.Threads, Context = "Process" };
        public static GaugeOptions ProcessHandles => new GaugeOptions { Name = "Process Handle Count", MeasurementUnit = Unit.Items, Context = "Process" };
        public static GaugeOptions CpuUsage => new GaugeOptions { Name = "CPU Usage", MeasurementUnit = Unit.Percent, Context = "Process" };
        
        public static MeterOptions DatabaseRequests => new MeterOptions() { Name = "Database Requests", MeasurementUnit = Unit.Requests, Context = "Database", RateUnit = TimeUnit.Seconds};
        public static TimerOptions DatabaseQuery => new TimerOptions() { Name = "Database Query", MeasurementUnit = Unit.Requests, DurationUnit = TimeUnit.Seconds, RateUnit = TimeUnit.Seconds, Context = "Database" };
        public static GaugeOptions DatabaseConnections => new GaugeOptions() { Name = "Database Connections", MeasurementUnit = Unit.Connections, Context = "Database" };
    }
}