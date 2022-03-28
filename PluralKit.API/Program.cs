using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters.Prometheus;

using Autofac.Extensions.DependencyInjection;

using PluralKit.Core;

using Serilog;

namespace PluralKit.API;

public class Program
{
    public static IMetricsRoot _metrics { get; set; }

    public static async Task Main(string[] args)
    {
        _metrics = AppMetrics.CreateDefaultBuilder()
            .OutputMetrics.AsPrometheusPlainText()
            .OutputMetrics.AsPrometheusProtobuf()
            .Build();

        InitUtils.InitStatic();
        await BuildInfoService.LoadVersion();
        var host = CreateHostBuilder(args).Build();
        var config = host.Services.GetRequiredService<CoreConfig>();
        await host.Services.GetRequiredService<RedisService>().InitAsync(config);
        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureMetrics(_metrics)
            .UseMetricsWebTracking()
            .UseMetricsEndpoints()
            .UseMetrics(
            options =>
            {
                options.EndpointOptions = endpointsOptions =>
                {
                    endpointsOptions.MetricsTextEndpointOutputFormatter = _metrics.OutputMetricsFormatters.OfType<MetricsPrometheusTextOutputFormatter>().First();
                    endpointsOptions.MetricsEndpointOutputFormatter = _metrics.OutputMetricsFormatters.OfType<MetricsPrometheusProtobufOutputFormatter>().First();
                };
            })
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .UseSerilog()
            .ConfigureWebHostDefaults(whb => whb
                .UseConfiguration(InitUtils.BuildConfiguration(args).Build())
                .ConfigureKestrel(opts =>
                {
                    opts.ListenAnyIP(opts.ApplicationServices.GetRequiredService<ApiConfig>().Port);
                })
                .UseStartup<Startup>());
}