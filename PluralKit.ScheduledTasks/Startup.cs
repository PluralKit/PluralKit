using System.Threading.Tasks;

using Autofac;

using Microsoft.Extensions.Configuration;

using PluralKit.Core;

namespace PluralKit.ScheduledTasks;

internal class Startup
{
    private static async Task Main(string[] args)
    {
        // Load configuration and run global init stuff
        var config = InitUtils.BuildConfiguration(args).Build();
        InitUtils.InitStatic();

        var services = BuildContainer(config);
        services.Resolve<TaskHandler>().Run();

        await Task.Delay(-1);
    }

    private static IContainer BuildContainer(IConfiguration config)
    {
        var builder = new ContainerBuilder();

        builder.RegisterInstance(config);
        builder.RegisterModule(new ConfigModule<CoreConfig>());
        builder.RegisterModule(new LoggingModule("ScheduledTasks"));
        builder.RegisterModule(new MetricsModule());
        builder.RegisterModule<DataStoreModule>();
        builder.RegisterType<TaskHandler>().AsSelf().SingleInstance();

        return builder.Build();
    }
}