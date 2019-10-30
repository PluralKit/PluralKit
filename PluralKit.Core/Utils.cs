using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Timer;
using Dapper;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using NodaTime.Text;
using Npgsql;
using PluralKit.Core;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;
using Serilog.Sinks.SystemConsole.Themes;


namespace PluralKit
{
    public static class Utils
    {
        public static string GenerateToken()
        {
            var buf = new byte[48]; // Results in a 64-byte Base64 string (no padding)
            new RNGCryptoServiceProvider().GetBytes(buf);
            return Convert.ToBase64String(buf);
        }

        public static bool IsLongerThan(this string str, int length)
        {
            if (str != null) return str.Length > length;
            return false;
        }

        public static Duration? ParsePeriod(string str)
        {
            
            Duration d = Duration.Zero;
            
            foreach (Match match in Regex.Matches(str, "(\\d{1,6})(\\w)"))
            {
                var amount = int.Parse(match.Groups[1].Value);
                var type = match.Groups[2].Value;

                if (type == "w") d += Duration.FromDays(7) * amount;
                else if (type == "d") d += Duration.FromDays(1) * amount;
                else if (type == "h") d += Duration.FromHours(1) * amount;
                else if (type == "m") d += Duration.FromMinutes(1) * amount;
                else if (type == "s") d += Duration.FromSeconds(1) * amount;
                else return null;
            }

            if (d == Duration.Zero) return null;
            return d;
        }

        public static LocalDate? ParseDate(string str, bool allowNullYear = false)
        {
            // NodaTime can't parse constructs like "1st" and "2nd" so we quietly replace those away
            // Gotta make sure to do the regex otherwise we'll catch things like the "st" in "August" too
            str = Regex.Replace(str, "(\\d+)(st|nd|rd|th)", "$1");
            
            var patterns = new[]
            {
                "MMM d yyyy",   // Jan 1 2019
                "MMM d, yyyy",  // Jan 1, 2019
                "MMMM d yyyy",  // January 1 2019
                "MMMM d, yyyy", // January 1, 2019
                "yyyy-MM-dd",   // 2019-01-01
                "yyyy MM dd",   // 2019 01 01
                "yyyy/MM/dd"    // 2019/01/01
            }.ToList();

            if (allowNullYear) patterns.AddRange(new[]
            {
                "MMM d",        // Jan 1
                "MMMM d",       // January 1
                "MM-dd",        // 01-01
                "MM dd",        // 01 01
                "MM/dd"         // 01/01
            });

            // Giving a template value so year will be parsed as 0001 if not present
            // This means we can later disambiguate whether a null year was given
            // TODO: should we be using invariant culture here?
            foreach (var pattern in patterns.Select(p => LocalDatePattern.CreateWithInvariantCulture(p).WithTemplateValue(new LocalDate(0001, 1, 1))))
            {
                var result = pattern.Parse(str);
                if (result.Success) return result.Value;
            }

            return null;
        }

        public static ZonedDateTime? ParseDateTime(string str, bool nudgeToPast = false, DateTimeZone zone = null)
        {
            if (zone == null) zone = DateTimeZone.Utc;
            
            // Find the current timestamp in the given zone, find the (naive) midnight timestamp, then put that into the same zone (and make it naive again)
            // Should yield a <current *local @ zone* date> 12:00:00 AM.
            var now = SystemClock.Instance.GetCurrentInstant().InZone(zone).LocalDateTime;
            var midnight = now.Date.AtMidnight();
            
            // First we try to parse the string as a relative time using the period parser
            var relResult = ParsePeriod(str);
            if (relResult != null)
            {
                // if we can, we just subtract that amount from the 
                return now.InZoneLeniently(zone).Minus(relResult.Value);
            }

            var timePatterns = new[]
            {
                "H:mm",         // 4:30
                "HH:mm",        // 23:30
                "H:mm:ss",      // 4:30:29
                "HH:mm:ss",     // 23:30:29
                "h tt",         // 2 PM
                "htt",          // 2PM
                "h:mm tt",      // 4:30 PM
                "h:mmtt",       // 4:30PM
                "h:mm:ss tt",   // 4:30:29 PM
                "h:mm:sstt",    // 4:30:29PM
                "hh:mm tt",     // 11:30 PM
                "hh:mmtt",      // 11:30PM
                "hh:mm:ss tt",   // 11:30:29 PM
                "hh:mm:sstt"   // 11:30:29PM
            };

            var datePatterns = new[]
            {
                "MMM d yyyy",   // Jan 1 2019
                "MMM d, yyyy",  // Jan 1, 2019
                "MMMM d yyyy",  // January 1 2019
                "MMMM d, yyyy", // January 1, 2019
                "yyyy-MM-dd",   // 2019-01-01
                "yyyy MM dd",   // 2019 01 01
                "yyyy/MM/dd",   // 2019/01/01
                "MMM d",        // Jan 1
                "MMMM d",       // January 1
                "MM-dd",        // 01-01
                "MM dd",        // 01 01
                "MM/dd"         // 01-01
            };
            
            // First, we try all the timestamps that only have a time
            foreach (var timePattern in timePatterns)
            {
                var pat = LocalDateTimePattern.CreateWithInvariantCulture(timePattern).WithTemplateValue(midnight);
                var result = pat.Parse(str);
                if (result.Success)
                {
                    // If we have a successful match and we need a time in the past, we try to shove a future-time a date before
                    // Example: "4:30 pm" at 3:30 pm likely refers to 4:30 pm the previous day
                    var val = result.Value;
                    
                    // If we need to nudge, we just subtract a day. This only occurs when we're parsing specifically *just time*, so
                    // we know we won't nudge it by more than a day since we use today's midnight timestamp as a date template.
                    
                    // Since this is a naive datetime, this ensures we're actually moving by one calendar day even if 
                    // DST changes occur, since they'll be resolved later wrt. the right side of the boundary
                    if (val > now && nudgeToPast) val = val.PlusDays(-1);
                    return val.InZoneLeniently(zone);
                }
            }
            
            // Then we try specific date+time combinations, both date first and time first, with and without commas
            foreach (var timePattern in timePatterns)
            {
                foreach (var datePattern in datePatterns)
                {
                    foreach (var patternStr in new[]
                    {
                        $"{timePattern}, {datePattern}", $"{datePattern}, {timePattern}",
                        $"{timePattern} {datePattern}", $"{datePattern} {timePattern}"
                    })
                    {
                        var pattern = LocalDateTimePattern.CreateWithInvariantCulture(patternStr).WithTemplateValue(midnight);
                        var res = pattern.Parse(str);
                        if (res.Success) return res.Value.InZoneLeniently(zone);
                    }
                }
            }
            
            // Finally, just date patterns, still using midnight as the template
            foreach (var datePattern in datePatterns)
            {
                var pat = LocalDateTimePattern.CreateWithInvariantCulture(datePattern).WithTemplateValue(midnight);
                var res = pat.Parse(str);
                if (res.Success) return res.Value.InZoneLeniently(zone);
            }

            // Still haven't parsed something, we just give up lmao
            return null;
        }
        
        public static string ExtractCountryFlag(string flag)
        {
            if (flag.Length != 4) return null;
            try
            {
                var cp1 = char.ConvertToUtf32(flag, 0);
                var cp2 = char.ConvertToUtf32(flag, 2);
                if (cp1 < 0x1F1E6 || cp1 > 0x1F1FF) return null;
                if (cp2 < 0x1F1E6 || cp2 > 0x1F1FF) return null;
                return $"{(char) (cp1 - 0x1F1E6 + 'A')}{(char) (cp2 - 0x1F1E6 + 'A')}";
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
        
        public static IEnumerable<T> TakeWhileIncluding<T>(this IEnumerable<T> list, Func<T, bool> predicate)
        {
            // modified from https://stackoverflow.com/a/6817553
            foreach(var el in list)
            {
                yield return el;
                if (!predicate(el))
                    yield break;
            }
        }

        public static string NullIfEmpty(this string input)
        {
            return input.Trim().Length == 0 ? null : input;
        }
    }

    public static class Emojis {
        public static readonly string Warn = "\u26A0";
        public static readonly string Success = "\u2705";
        public static readonly string Error = "\u274C";
        public static readonly string Note = "\U0001f4dd";
        public static readonly string ThumbsUp = "\U0001f44d";
        public static readonly string RedQuestion = "\u2753";
    }

    public static class Formats
    {
        public static IPattern<Instant> TimestampExportFormat = InstantPattern.CreateWithInvariantCulture("g");
        public static IPattern<LocalDate> DateExportFormat = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");
        
        // We create a composite pattern that only shows the two most significant things
        // eg. if we have something with nonzero day component, we show <x>d <x>h, but if it's
        // a smaller duration we may only bother with showing <x>h <x>m or <x>m <x>s
        public static IPattern<Duration> DurationFormat = new CompositePatternBuilder<Duration>
        {
            {DurationPattern.CreateWithInvariantCulture("s's'"), d => true},
            {DurationPattern.CreateWithInvariantCulture("m'm' s's'"), d => d.Minutes > 0},
            {DurationPattern.CreateWithInvariantCulture("H'h' m'm'"), d => d.Hours > 0},
            {DurationPattern.CreateWithInvariantCulture("D'd' h'h'"), d => d.Days > 0}
        }.Build();
        
        public static IPattern<LocalDateTime> LocalDateTimeFormat = LocalDateTimePattern.CreateWithInvariantCulture("yyyy-MM-dd HH:mm:ss");
        public static IPattern<ZonedDateTime> ZonedDateTimeFormat = ZonedDateTimePattern.CreateWithInvariantCulture("yyyy-MM-dd HH:mm:ss x", DateTimeZoneProviders.Tzdb);
    }
    public static class InitUtils
    {
        public static IConfigurationBuilder BuildConfiguration(string[] args) => new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("pluralkit.conf", true)
            .AddEnvironmentVariables()
            .AddCommandLine(args);

        public static void Init()
        {
            InitDatabase();
        }
        
        private static void InitDatabase()
        {
            // Dapper by default tries to pass ulongs to Npgsql, which rejects them since PostgreSQL technically
            // doesn't support unsigned types on its own.
            // Instead we add a custom mapper to encode them as signed integers instead, converting them back and forth.
            SqlMapper.RemoveTypeMap(typeof(ulong));
            SqlMapper.AddTypeHandler<ulong>(new UlongEncodeAsLongHandler());
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            // Also, use NodaTime. it's good.
            NpgsqlConnection.GlobalTypeMapper.UseNodaTime();
            // With the thing we add above, Npgsql already handles NodaTime integration
            // This makes Dapper confused since it thinks it has to convert it anyway and doesn't understand the types
            // So we add a custom type handler that literally just passes the type through to Npgsql
            SqlMapper.AddTypeHandler(new PassthroughTypeHandler<Instant>());
            SqlMapper.AddTypeHandler(new PassthroughTypeHandler<LocalDate>());

            // Add global type mapper for ProxyTag compound type in Postgres
            NpgsqlConnection.GlobalTypeMapper.MapComposite<ProxyTag>("proxy_tag");
        }

        public static ILogger InitLogger(CoreConfig config, string component)
        {
            return new LoggerConfiguration()
                .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
                .MinimumLevel.Debug()
                .WriteTo.File(
                    new RenderedCompactJsonFormatter(),
                    (config.LogDir ?? "logs") + $"/pluralkit.{component}.log",
                    rollingInterval: RollingInterval.Day,
                    flushToDiskInterval: TimeSpan.FromSeconds(10),
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    buffered: true)
                .WriteTo.Console(theme: AnsiConsoleTheme.Code, outputTemplate:"[{Timestamp:HH:mm:ss}] [{EventId}] {Level:u3} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        public static IMetrics InitMetrics(CoreConfig config, string onlyContext = null)
        {
            var builder = AppMetrics.CreateDefaultBuilder();
            if (config.InfluxUrl != null && config.InfluxDb != null)
                builder.Report.ToInfluxDb(config.InfluxUrl, config.InfluxDb);
            if (onlyContext != null)
                builder.Filter.ByIncludingOnlyContext(onlyContext);
            return builder.Build();
        }

        public static JsonSerializerSettings BuildSerializerSettings() => new JsonSerializerSettings().BuildSerializerSettings();

        public static JsonSerializerSettings BuildSerializerSettings(this JsonSerializerSettings settings)
        {
            settings.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            return settings;
        }
    }

    public class LoggerProvider
    {
        private CoreConfig _config;
        public ILogger RootLogger { get; }

        public LoggerProvider(CoreConfig config, string component)
        {
            _config = config;
            RootLogger = InitUtils.InitLogger(_config, component);
        }
    }

    public class UlongEncodeAsLongHandler : SqlMapper.TypeHandler<ulong>
    {
        public override ulong Parse(object value)
        {
            // Cast to long to unbox, then to ulong (???)
            return (ulong)(long)value;
        }

        public override void SetValue(IDbDataParameter parameter, ulong value)
        {
            parameter.Value = (long)value;
        }
    }

    public class PassthroughTypeHandler<T> : SqlMapper.TypeHandler<T>
    {
        public override void SetValue(IDbDataParameter parameter, T value)
        {
            parameter.Value = value;
        }

        public override T Parse(object value)
        {
            return (T) value;
        }
    }
    
    public class DbConnectionFactory
    {
        private CoreConfig _config;
        private ILogger _logger;
        private IMetrics _metrics;
        private DbConnectionCountHolder _countHolder;

        public DbConnectionFactory(CoreConfig config, DbConnectionCountHolder countHolder, ILogger logger, IMetrics metrics)
        {
            _config = config;
            _countHolder = countHolder;
            _metrics = metrics;
            _logger = logger;
        }

        public async Task<IDbConnection> Obtain()
        {
            // Mark the request (for a handle, I guess) in the metrics
            _metrics.Measure.Meter.Mark(CoreMetrics.DatabaseRequests);
            
            // Actually create and try to open the connection
            var conn = new NpgsqlConnection(_config.Database);
            await conn.OpenAsync();
            
            // Increment the count
            _countHolder.Increment();
            // Return a wrapped connection which will decrement the counter on dispose
            return new PerformanceTrackingConnection(conn, _countHolder, _logger, _metrics);
        }
    }

    public class DbConnectionCountHolder
    {
        private int _connectionCount;
        public int ConnectionCount => _connectionCount;

        public void Increment()
        {
            Interlocked.Increment(ref _connectionCount);
        }

        public void Decrement()
        {
            Interlocked.Decrement(ref _connectionCount);
        }
    } 
    
    public class PerformanceTrackingConnection: IDbConnection
    {
        // Simple delegation of everything.
        private NpgsqlConnection _impl;

        private DbConnectionCountHolder _countHolder;
        private ILogger _logger;
        private IMetrics _metrics;

        public PerformanceTrackingConnection(NpgsqlConnection impl, DbConnectionCountHolder countHolder,
            ILogger logger, IMetrics metrics)
        {
            _impl = impl;
            _countHolder = countHolder;
            _logger = logger;
            _metrics = metrics;
        }

        public void Dispose()
        {
            _impl.Dispose();
            
            _countHolder.Decrement();
        }

        public IDbTransaction BeginTransaction()
        {
            return _impl.BeginTransaction();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return _impl.BeginTransaction(il);
        }

        public void ChangeDatabase(string databaseName)
        {
            _impl.ChangeDatabase(databaseName);
        }

        public void Close()
        {
            _impl.Close();
        }

        public IDbCommand CreateCommand()
        {
            return new PerformanceTrackingCommand(_impl.CreateCommand(), _logger, _metrics);
        }

        public void Open()
        {
            _impl.Open();
        }

        public NpgsqlBinaryImporter BeginBinaryImport(string copyFromCommand)
        {
            return _impl.BeginBinaryImport(copyFromCommand);
        }

        public string ConnectionString
        {
            get => _impl.ConnectionString;
            set => _impl.ConnectionString = value;
        }

        public int ConnectionTimeout => _impl.ConnectionTimeout;

        public string Database => _impl.Database;

        public ConnectionState State => _impl.State;
    }

    public class PerformanceTrackingCommand : DbCommand
    {
        private NpgsqlCommand _impl;
        private ILogger _logger;
        private IMetrics _metrics;

        public PerformanceTrackingCommand(NpgsqlCommand impl, ILogger logger, IMetrics metrics)
        {
            _impl = impl;
            _metrics = metrics;
            _logger = logger;
        }

        public override void Cancel()
        {
            _impl.Cancel();
        }

        public override int ExecuteNonQuery()
        {
            return _impl.ExecuteNonQuery();
        }

        public override object ExecuteScalar()
        {
            return _impl.ExecuteScalar();
        }

        public override void Prepare()
        {
            _impl.Prepare();
        }

        public override string CommandText
        {
            get => _impl.CommandText;
            set => _impl.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _impl.CommandTimeout;
            set => _impl.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _impl.CommandType;
            set => _impl.CommandType = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _impl.UpdatedRowSource;
            set => _impl.UpdatedRowSource = value;
        }

        protected override DbConnection DbConnection
        {
            get => _impl.Connection;
            set => _impl.Connection = (NpgsqlConnection) value;
        }

        protected override DbParameterCollection DbParameterCollection => _impl.Parameters;

        protected override DbTransaction DbTransaction
        {
            get => _impl.Transaction;
            set => _impl.Transaction = (NpgsqlTransaction) value;
        }

        public override bool DesignTimeVisible
        {
            get => _impl.DesignTimeVisible;
            set => _impl.DesignTimeVisible = value;
        }

        protected override DbParameter CreateDbParameter()
        {
            return _impl.CreateParameter();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            return _impl.ExecuteReader(behavior);
        }
        
        private IDisposable LogQuery()
        {
            return new QueryLogger(_logger, _metrics, CommandText);
        }

        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            using (LogQuery())
                return await _impl.ExecuteReaderAsync(behavior, cancellationToken);
        }

        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            using (LogQuery())
                return await _impl.ExecuteNonQueryAsync(cancellationToken);
        }

        public override async Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            using (LogQuery())
                return await _impl.ExecuteScalarAsync(cancellationToken);
        }
    }

    public class QueryLogger : IDisposable
    {
        private ILogger _logger;
        private IMetrics _metrics;
        private string _commandText;
        private Stopwatch _stopwatch;

        public QueryLogger(ILogger logger, IMetrics metrics, string commandText)
        {
            _metrics = metrics;
            _commandText = commandText;
            _logger = logger;
            
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.Verbose("Executed query {Query} in {ElapsedTime}", _commandText, _stopwatch.Elapsed);
            
            // One tick is 100 nanoseconds
            _metrics.Provider.Timer.Instance(CoreMetrics.DatabaseQuery, new MetricTags("query", _commandText))
                .Record(_stopwatch.ElapsedTicks / 10, TimeUnit.Microseconds, _commandText);
        }
    }

    public class EventIdProvider
    {
        public Guid EventId { get; }

        public EventIdProvider()
        {
            EventId = Guid.NewGuid();
        }
    }
}
