using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using NodaTime;

namespace PluralKit.Core
{
    public class HandlerQueue<T>
    {
        private long _seq;
        private readonly ConcurrentDictionary<long, HandlerEntry> _handlers = new ConcurrentDictionary<long, HandlerEntry>();

        public HandlerEntry Add(Func<T, Task<bool>> handler)
        {
            var entry = new HandlerEntry {Handler = handler};

            _handlers[Interlocked.Increment(ref _seq)] = entry;
            return entry;
        }

        public async Task<T> WaitFor(Func<T, bool> predicate, Duration? timeout = null, CancellationToken ct = default)
        {
            var timeoutTask = Task.Delay(timeout?.ToTimeSpan() ?? TimeSpan.FromMilliseconds(-1), ct);
            var tcs = new TaskCompletionSource<T>();

            Task<bool> Handler(T e)
            {
                var matches = predicate(e);
                if (matches) tcs.SetResult(e);
                return Task.FromResult(matches);
            }

            var entry = new HandlerEntry {Handler = Handler};
            _handlers[Interlocked.Increment(ref _seq)] = entry;

            // Wait for either the event task or the timeout task
            // If the timeout task finishes first, raise, otherwise pass event through
            try
            {
                var theTask = await Task.WhenAny(timeoutTask, tcs.Task);
                if (theTask == timeoutTask)
                    throw new TimeoutException();
            }
            finally
            {
                entry.Remove();
            }

            return await tcs.Task;
        }

        public async Task<bool> TryHandle(T evt)
        {
            // First pass to clean up dead handlers
            foreach (var (k, entry) in _handlers)
                if (!entry.Alive)
                    _handlers.TryRemove(k, out _);

            // Now iterate and try handling until we find a good one
            var now = SystemClock.Instance.GetCurrentInstant();
            foreach (var (_, entry) in _handlers)
            {
                if (entry.Expiry < now) entry.Alive = false;
                else if (entry.Alive && await entry.Handler(evt))
                {
                    entry.Alive = false;
                    return true;
                }
            }

            return false;
        }

        public class HandlerEntry
        {
            internal Func<T, Task<bool>> Handler;
            internal bool Alive = true;
            internal Instant Expiry = SystemClock.Instance.GetCurrentInstant() + Duration.FromMinutes(30);

            public void Remove() => Alive = false;
        }
    }
}