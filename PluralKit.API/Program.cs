using Autofac.Extensions.DependencyInjection;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using PluralKit.Core;

using Serilog;

namespace PluralKit.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            InitUtils.InitStatic();
            CreateHostBuilder(args).Build().Run();
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
}