using App.Metrics;

using Autofac;

namespace PluralKit.Core;

public class MetricsModule: Module
{
    private readonly string _onlyContext;

    public MetricsModule(string onlyContext = null)
    {
        _onlyContext = onlyContext;
    }

    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(c => InitMetrics(c.Resolve<CoreConfig>()))
            .AsSelf().As<IMetrics>().SingleInstance();
    }

    private IMetricsRoot InitMetrics(CoreConfig config)
    {
        var builder = AppMetrics.CreateDefaultBuilder();
        if (config.InfluxUrl != null && config.InfluxDb != null)
            builder.Report.ToInfluxDb(config.InfluxUrl, config.InfluxDb);
        if (_onlyContext != null)
            builder.Filter.ByIncludingOnlyContext(_onlyContext);
        return builder.Build();
    }
}