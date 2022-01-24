using System.Globalization;

using Autofac;

using Microsoft.Extensions.Logging;

using NodaTime;

using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Elasticsearch;
using Serilog.Sinks.SystemConsole.Themes;

using ILogger = Serilog.ILogger;

namespace PluralKit.Core;

public class LoggingModule: Module
{
    private readonly string _component;
    private readonly Action<LoggerConfiguration> _fn;

    public LoggingModule(string component, Action<LoggerConfiguration> fn = null, LoggerConfiguration cfg = null)
    {
        _component = component;
        _fn = fn ?? (_ => { });
        _cfg = cfg ?? new LoggerConfiguration();
    }

    private LoggerConfiguration _cfg { get; }

    protected override void Load(ContainerBuilder builder)
    {
        builder
            .Register(c => InitLogger(c.Resolve<CoreConfig>()))
            .AsSelf()
            .SingleInstance()
            // AutoActivate ensures logging is enabled as early as possible in the API startup flow
            // since we set the Log.Logger global >.>
            .AutoActivate();

        builder.Register(c => new LoggerFactory().AddSerilog(c.Resolve<ILogger>()))
            .As<ILoggerFactory>()
            .SingleInstance();
    }

    private ILogger InitLogger(CoreConfig config)
    {
        var consoleTemplate = "[{Timestamp:HH:mm:ss.fff}] {Level:u3} {Message:lj}{NewLine}{Exception}";
        var outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.ffffff}] {Level:u3} {Message:lj}{NewLine}{Exception}";

        var logCfg = _cfg
            .Enrich.FromLogContext()
            .Enrich.WithProperty("GitCommitHash", BuildInfoService.FullVersion)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
            .Enrich.WithProperty("Component", _component)
            .MinimumLevel.Is(config.ConsoleLogLevel)

            // Don't want App.Metrics/D#+ spam
            .MinimumLevel.Override("App.Metrics", LogEventLevel.Information)

            // nor ASP.NET spam
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)

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
                    retainedFileCountLimit: 10,
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: null,
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
                MinimumLogEventLevel = config.ElasticLogLevel,
                IndexFormat = "pluralkit-logs-{0:yyyy.MM.dd}",
                CustomFormatter = new ScalarFormatting.Elasticsearch()
            };

            logCfg.WriteTo.Elasticsearch(elasticConfig);
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