using App.Metrics;
using App.Metrics.Gauge;

namespace PluralKit.Core
{
    public static class CoreMetrics
    {
        public static GaugeOptions SystemCount => new GaugeOptions { Name = "Systems", MeasurementUnit = Unit.Items};
        public static GaugeOptions MemberCount => new GaugeOptions { Name = "Members", MeasurementUnit = Unit.Items };
        public static GaugeOptions MessageCount => new GaugeOptions { Name = "Messages", MeasurementUnit = Unit.Items };
        public static GaugeOptions SwitchCount => new GaugeOptions { Name = "Switches", MeasurementUnit = Unit.Items };
    }
}