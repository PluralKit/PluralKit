using System.IO;

using Dapper;

using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;

using NodaTime;
using NodaTime.Serialization.JsonNet;

using Npgsql;

namespace PluralKit.Core {
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
        
        public static JsonSerializerSettings BuildSerializerSettings() => new JsonSerializerSettings().BuildSerializerSettings();

        public static JsonSerializerSettings BuildSerializerSettings(this JsonSerializerSettings settings)
        {
            settings.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            return settings;
        }
    }
}