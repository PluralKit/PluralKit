using System;
using System.Threading;
using System.Threading.Tasks;

namespace Myriad.Gateway.State
{
    public class HeartbeatWorker: IAsyncDisposable
    {
        private Task? _worker;
        private CancellationTokenSource? _workerCts;

        public TimeSpan? CurrentHeartbeatInterval { get; private set; }

        public async ValueTask Start(TimeSpan heartbeatInterval, Func<Task> callback)
        {
            if (_worker != null)
                await Stop();

            CurrentHeartbeatInterval = heartbeatInterval;
            _workerCts = new CancellationTokenSource();
            _worker = Worker(heartbeatInterval, callback, _workerCts.Token);
        }

        public async ValueTask Stop()
        {
            if (_worker == null)
                return;

            _workerCts?.Cancel();
            try
            {
                await _worker;
            }
            catch (TaskCanceledException) { }

            _worker?.Dispose();
            _workerCts?.Dispose();
            _worker = null;
            CurrentHeartbeatInterval = null;
        }

        private async Task Worker(TimeSpan heartbeatInterval, Func<Task> callback, CancellationToken ct)
        {
            var initialDelay = GetInitialHeartbeatDelay(heartbeatInterval);
            await Task.Delay(initialDelay, ct);

            while (!ct.IsCancellationRequested)
            {
                await callback();
                await Task.Delay(heartbeatInterval, ct);
            }
        }

        private static TimeSpan GetInitialHeartbeatDelay(TimeSpan heartbeatInterval) =>
            // Docs specify `heartbeat_interval * random.random()` but we'll add a lil buffer :)
            heartbeatInterval * (new Random().NextDouble() * 0.9 + 0.05);

        public async ValueTask DisposeAsync()
        {
            await Stop();
        }
    }
}