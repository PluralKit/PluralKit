using System.IO;

using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;

using NodaTime;
using NodaTime.Serialization.JsonNet;

namespace PluralKit.Core {
    public static class InitUtils
    {
        public static void InitStatic()
        {
            Database.InitStatic();
        }
        
        public static IConfigurationBuilder BuildConfiguration(string[] args) => new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("pluralkit.conf", true)
            .AddEnvironmentVariables()
            .AddCommandLine(args);
        
        public static JsonSerializerSettings BuildSerializerSettings() => new JsonSerializerSettings().BuildSerializerSettings();

        public static JsonSerializerSettings BuildSerializerSettings(this JsonSerializerSettings settings)
        {
            settings.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            return settings;
        }
    }
}