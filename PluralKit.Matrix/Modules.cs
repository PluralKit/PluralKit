using Autofac;

using NodaTime;

namespace PluralKit.Matrix;

public class MatrixModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Database
        builder.RegisterType<MatrixMigrator>().AsSelf().SingleInstance();
        builder.RegisterType<MatrixRepository>().AsSelf().SingleInstance();

        // API
        builder.RegisterType<MatrixApiClient>().AsSelf().SingleInstance();

        // Services
        builder.RegisterType<VirtualUserCacheService>().AsSelf().SingleInstance();
        builder.RegisterType<VirtualUserService>().AsSelf().SingleInstance();
        builder.RegisterType<MatrixEventHandler>().AsSelf().SingleInstance();
        builder.RegisterType<MatrixProxyService>().AsSelf().SingleInstance();
        builder.RegisterType<MatrixCommandHandler>().AsSelf().SingleInstance();

        // Proxy
        builder.RegisterType<ProxyMatcher>().AsSelf().SingleInstance();
        builder.RegisterType<ProxyTagParser>().AsSelf().SingleInstance();

        // Utils
        builder.Register(_ => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        }).AsSelf().SingleInstance();
        builder.RegisterInstance(SystemClock.Instance).As<NodaTime.IClock>();
    }
}
