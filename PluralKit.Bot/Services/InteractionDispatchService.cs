using System.Collections.Concurrent;

using Myriad.Cache;

using NodaTime;

using Serilog;

namespace PluralKit.Bot;

public class InteractionDispatchService: IDisposable
{
    private static readonly Duration DefaultExpiry = Duration.FromMinutes(15);
    private readonly Task _cleanupWorker;
    private readonly IClock _clock;
    private readonly CancellationTokenSource _cts = new();

    private readonly ConcurrentDictionary<Guid, RegisteredInteraction> _handlers = new();
    private readonly ILogger _logger;

    private readonly IDiscordCache _cache;

    public InteractionDispatchService(IClock clock, ILogger logger, IDiscordCache cache)
    {
        _clock = clock;
        _cache = cache;
        _logger = logger.ForContext<InteractionDispatchService>();

        _cleanupWorker = CleanupLoop(_cts.Token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    public async ValueTask<bool> Dispatch(string customId, InteractionContext context)
    {
        if (!Guid.TryParse(customId, out var customIdGuid))
            return false;

        if (!_handlers.TryGetValue(customIdGuid, out var handler))
            return false;

        await handler.Callback.Invoke(context);
        return true;
    }

    public void Unregister(string customId)
    {
        if (!Guid.TryParse(customId, out var customIdGuid))
            return;

        _handlers.TryRemove(customIdGuid, out _);
    }

    public string Register(int shardId, Func<InteractionContext, Task> callback, Duration? expiry = null)
    {
        var key = Guid.NewGuid();

        // if http_cache, return RegisterRemote
        // not awaited here, it's probably fine
        if (_cache is HttpDiscordCache)
            (_cache as HttpDiscordCache).AwaitInteraction(shardId, key.ToString(), expiry);

        var handler = new RegisteredInteraction
        {
            Callback = callback,
            Expiry = _clock.GetCurrentInstant() + (expiry ?? DefaultExpiry)
        };

        _handlers[key] = handler;
        return key.ToString();
    }

    private async Task CleanupLoop(CancellationToken ct)
    {
        while (true)
        {
            DoCleanup();
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    private void DoCleanup()
    {
        var now = _clock.GetCurrentInstant();
        var removedCount = 0;
        foreach (var (key, value) in _handlers.ToArray())
            if (value.Expiry < now)
            {
                _handlers.TryRemove(key, out _);
                removedCount++;
            }

        _logger.Debug("Removed {ExpiredInteractions} expired interactions", removedCount);
    }

    private struct RegisteredInteraction
    {
        public Instant Expiry;
        public Func<InteractionContext, Task> Callback;
    }
}