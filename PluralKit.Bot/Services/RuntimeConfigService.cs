using Serilog;

using StackExchange.Redis;

using PluralKit.Core;

namespace PluralKit.Bot;

public class RuntimeConfigService
{
    private readonly RedisService _redis;
    private readonly ILogger _logger;

    private Dictionary<string, string> settings = new();

    private string RedisKey;

    public RuntimeConfigService(ILogger logger, RedisService redis, BotConfig config)
    {
        _logger = logger.ForContext<RuntimeConfigService>();
        _redis = redis;

        var clusterId = config.Cluster?.NodeIndex ?? 0;
        RedisKey = $"remote_config:dotnet_bot:{clusterId}";
    }

    public async Task LoadConfig()
    {
        var redisConfig = await _redis.Connection.GetDatabase().HashGetAllAsync(RedisKey);
        foreach (var entry in redisConfig)
            settings.Add(entry.Name, entry.Value);
    }

    public async Task Set(string key, string value)
    {
        await _redis.Connection.GetDatabase().HashSetAsync(RedisKey, new[] { new HashEntry(key, new RedisValue(value)) });
        settings.Add(key, value);
    }

    public object? Get(string key) => settings.GetValueOrDefault(key);

    public bool Exists(string key) => settings.ContainsKey(key);

    public Dictionary<string, string> GetAll() => settings;
}