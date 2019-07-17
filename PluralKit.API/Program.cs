using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace PluralKit.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            InitUtils.Init();
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseConfiguration(InitUtils.BuildConfiguration(args).Build())
                .ConfigureKestrel(opts => { opts.ListenAnyIP(5000);})
                .UseStartup<Startup>();
    }
}