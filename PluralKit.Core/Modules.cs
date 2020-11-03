using System;
using System.Globalization;

using App.Metrics;

using Autofac;
using Autofac.Extensions.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Elasticsearch;
using Serilog.Sinks.SystemConsole.Themes;

namespace PluralKit.Core
{
    public class DataStoreModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<DbConnectionCountHolder>().SingleInstance();
            builder.RegisterType<Database>().As<IDatabase>().SingleInstance();
            builder.RegisterType<DatabaseMigrator>().As<DatabaseMigrator>().SingleInstance();
            builder.RegisterType<ModelRepository>().AsSelf().SingleInstance();
            
            builder.Populate(new ServiceCollection().AddMemoryCache());
        }
    }

    public class ConfigModule<T>: Module where T: new()
    {
        private readonly string _submodule;

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
        private readonly Action<LoggerConfiguration> _fn;

        public LoggingModule(string component, Action<LoggerConfiguration> fn = null)
        {
            _component = component;
            _fn = fn ?? (_ => { });
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder
                .Register(c => InitLogger(c.Resolve<CoreConfig>()))
                .AsSelf()
                .SingleInstance()
                // AutoActivate ensures logging is enabled as early as possible in the API startup flow
                // since we set the Log.Logger global >.>
                .AutoActivate();
        }

        private ILogger InitLogger(CoreConfig config)
        {
            var consoleTemplate = "[{Timestamp:HH:mm:ss.fff}] {Level:u3} {Message:lj}{NewLine}{Exception}";
            var outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.ffffff}] {Level:u3} {Message:lj}{NewLine}{Exception}";
            
            var logCfg = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
                .Enrich.WithProperty("Component", _component)
                .MinimumLevel.Is(config.ConsoleLogLevel)
                
                // Don't want App.Metrics spam
                .MinimumLevel.Override("App.Metrics", LogEventLevel.Information)
                
                // Actual formatting for these is handled in ScalarFormatting
                .Destructure.AsScalar<SystemId>()
                .Destructure.AsScalar<MemberId>()
                .Destructure.AsScalar<GroupId>()
                .Destructure.AsScalar<SwitchId>()
                .Destructure.ByTransforming<ProxyTag>(t => new { t.Prefix, t.Suffix })
                .Destructure.With<PatchObjectDestructuring>()
                
                .WriteTo.Async(a =>
                {
                    // Both the same output, except one is raw compact JSON and one is plain text.
                    // Output simultaneously. May remove the JSON formatter later, keeping it just in cast.
                    // Flush interval is 50ms (down from 10s) to make "tail -f" easier. May be too low?
                    a.File(
                        (config.LogDir ?? "logs") + $"/pluralkit.{_component}.log",
                        outputTemplate: outputTemplate,
                        rollingInterval: RollingInterval.Day,
                        flushToDiskInterval: TimeSpan.FromMilliseconds(50),
                        restrictedToMinimumLevel: config.FileLogLevel,
                        formatProvider: new UTCTimestampFormatProvider(),
                        buffered: true);

                    a.File(
                        new RenderedCompactJsonFormatter(new ScalarFormatting.JsonValue()),
                        (config.LogDir ?? "logs") + $"/pluralkit.{_component}.json",
                        rollingInterval: RollingInterval.Day,
                        flushToDiskInterval: TimeSpan.FromMilliseconds(50),
                        restrictedToMinimumLevel: config.FileLogLevel,
                        buffered: true);
                })
                .WriteTo.Async(a =>
                    a.Console(
                        theme: AnsiConsoleTheme.Code,
                        outputTemplate: consoleTemplate,
                        restrictedToMinimumLevel: config.ConsoleLogLevel));

            if (config.ElasticUrl != null)
            {
                var elasticConfig = new ElasticsearchSinkOptions(new Uri(config.ElasticUrl))
                {
                    AutoRegisterTemplate = true,
                    AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                    MinimumLogEventLevel = LogEventLevel.Verbose,
                    IndexFormat = "pluralkit-logs-{0:yyyy.MM.dd}",
                    CustomFormatter = new ScalarFormatting.Elasticsearch()
                };
               
                logCfg.WriteTo
                    .Conditional(e => e.Properties.ContainsKey("Elastic"), 
                        c => c.Elasticsearch(elasticConfig));
            }

            _fn.Invoke(logCfg);
            return Log.Logger = logCfg.CreateLogger();
        }
    }

    // Serilog why is this necessary for such a simple thing >.>
    public class UTCTimestampFormatProvider: IFormatProvider
    {
        public object GetFormat(Type formatType) => new UTCTimestampFormatter();
    }

    public class UTCTimestampFormatter: ICustomFormatter
    {
        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            // Convert offset to UTC and then print
            // FormatProvider defaults to locale-specific stuff so we force-default to invariant culture
            // If we pass the given formatProvider it'll conveniently ignore it, for some reason >.>
            if (arg is DateTimeOffset dto)
                return dto.ToUniversalTime().ToString(format, CultureInfo.InvariantCulture);
            if (arg is IFormattable f)
                return f.ToString(format, CultureInfo.InvariantCulture);
            return arg.ToString();
        }
    }
}