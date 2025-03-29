using Autofac.Extensions.DependencyInjection;

using PluralKit.Core;

using Serilog;

namespace PluralKit.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        InitUtils.InitStatic();
        await BuildInfoService.LoadVersion();
        var host = CreateHostBuilder(args).Build();
        var config = host.Services.GetRequiredService<CoreConfig>();

        // Initialize Sentry SDK, and make sure it gets dropped at the end
        using var _ = SentrySdk.Init(opts =>
        {
            opts.Dsn = config.SentryUrl ?? "";
            opts.Release = BuildInfoService.FullVersion;
            opts.AutoSessionTracking = true;
            //                opts.DisableTaskUnobservedTaskExceptionCapture();
        });

        await host.Services.GetRequiredService<RedisService>().InitAsync(config);
        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
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