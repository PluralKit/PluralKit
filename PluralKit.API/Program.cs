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
        await CreateHostBuilder(args).Build().RunAsync();
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