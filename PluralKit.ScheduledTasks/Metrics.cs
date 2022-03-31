using App.Metrics;
using App.Metrics.Gauge;

public static class Metrics
{
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

    public static GaugeOptions WebhookCacheSize => new()
    {
        Name = "Webhook Cache Size",
        Context = "Bot",
        MeasurementUnit = Unit.Items
    };
}