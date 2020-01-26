using Autofac.Extensions.DependencyInjection;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace PluralKit.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            InitUtils.Init();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureWebHostDefaults(whb => whb

                    .UseConfiguration(InitUtils.BuildConfiguration(args).Build())
                    .ConfigureKestrel(opts => { opts.ListenAnyIP(5000); })
                    .UseStartup<Startup>());
    }
}