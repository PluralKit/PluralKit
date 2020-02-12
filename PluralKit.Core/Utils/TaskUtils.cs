using System;
using System.Threading;
using System.Threading.Tasks;

namespace PluralKit.Core {
    public static class TaskUtils {
        public static async Task CatchException(this Task task, Action<Exception> handler) {
            try {
                await task;
            } catch (Exception e) {
                handler(e);
            }
        }
        
        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan? timeout) {
            // https://stackoverflow.com/a/22078975
            using (var timeoutCancellationTokenSource = new CancellationTokenSource()) {
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout ?? TimeSpan.FromMilliseconds(-1), timeoutCancellationTokenSource.Token));
                if (completedTask == task) {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                } else {
                    throw new TimeoutException();
                }
            }
        }
    }
}