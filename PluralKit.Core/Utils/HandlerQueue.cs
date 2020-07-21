using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using NodaTime;

namespace PluralKit.Core
{
    public class HandlerQueue<T>
    {
        private readonly List<HandlerEntry> _handlers = new List<HandlerEntry>();

        public HandlerEntry Add(Func<T, Task<bool>> handler)
        {
            var entry = new HandlerEntry {Handler = handler};
            _handlers.Add(entry);
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
            _handlers.Add(entry);

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
            // Saw spurious NREs in prod indicating `he` is null, add a special check for that for now
            _handlers.RemoveAll(he => he == null || !he.Alive);

            var now = SystemClock.Instance.GetCurrentInstant();
            foreach (var entry in _handlers)
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