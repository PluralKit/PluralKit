using System;

using App.Metrics;

using Autofac;
using Autofac.Extensions.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;

namespace PluralKit.Core
{
    public class DataStoreModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<DbConnectionCountHolder>().SingleInstance();
            builder.RegisterType<DbConnectionFactory>().AsSelf().SingleInstance();
            builder.RegisterType<PostgresDataStore>().AsSelf().As<IDataStore>();
            builder.RegisterType<SchemaService>().AsSelf();
            
            builder.Populate(new ServiceCollection().AddMemoryCache());
            builder.RegisterType<ProxyCache>().AsSelf().SingleInstance();
        }
    }

    public class ConfigModule<T>: Module where T: new()
    {
        private string _submodule;

        public ConfigModule(string submodule = null)
        {
            _submodule = submodule;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // We're assuming IConfiguration is already available somehow - it comes from various places (auto-injected in ASP, etc)

            // Register the CoreConfig and where to find it
            builder.Register(c => c.Resolve<IConfiguration>().GetSection("PluralKit").Get<CoreConfig>() ?? new CoreConfig()).SingleInstance();

            // Register the submodule config (BotConfig, etc) if specified
            if (_submodule != null)
                builder.Register(c => c.Resolve<IConfiguration>().GetSection("PluralKit").GetSection(_submodule).Get<T>() ?? new T()).SingleInstance();
        }
    }

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

    public class LoggingModule: Module
    {
        private readonly string _component;

        public LoggingModule(string component)
        {
            _component = component;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => InitLogger(c.Resolve<CoreConfig>())).AsSelf().SingleInstance();
        }

        private ILogger InitLogger(CoreConfig config)
        {
            return new LoggerConfiguration()
                .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
                .MinimumLevel.Debug()
                .WriteTo.Async(a =>
                    a.File(
                        new RenderedCompactJsonFormatter(),
                        (config.LogDir ?? "logs") + $"/pluralkit.{_component}.log",
                        rollingInterval: RollingInterval.Day,
                        flushToDiskInterval: TimeSpan.FromSeconds(10),
                        restrictedToMinimumLevel: LogEventLevel.Information,
                        buffered: true))
                .WriteTo.Async(a => 
                    a.Console(theme: AnsiConsoleTheme.Code, outputTemplate:"[{Timestamp:HH:mm:ss}] {Level:u3} {Message:lj}{NewLine}{Exception}"))
                .CreateLogger();
        }
    }
}