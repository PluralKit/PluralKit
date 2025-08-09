using System.Globalization;

using Autofac;

using AppFact.SerilogOpenSearchSink;

using Microsoft.Extensions.Logging;

using NodaTime;

using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
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
        // todo: this is messy and not really used anywhere...?
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
        var logCfg = _cfg
            .Enrich.FromLogContext()
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
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
                a.Console(
                    new CustomJsonFormatter(_component),
                    config.ConsoleLogLevel));

        if (config.ElasticUrl != null)
        {
            logCfg.WriteTo.OpenSearch(
                uri: config.ElasticUrl,
                index: "dotnet-logs",
                basicAuthUser: "unused",
                basicAuthPassword: "unused"
            );
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