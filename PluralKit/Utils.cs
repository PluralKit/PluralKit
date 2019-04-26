using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Discord;
using Discord.Commands;
using Discord.Commands.Builders;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace PluralKit
{
    public static class Utils
    {
        public static string GenerateHid()
        {
            var rnd = new Random();
            var charset = "abcdefghijklmnopqrstuvwxyz";
            string hid = "";
            for (int i = 0; i < 5; i++)
            {
                hid += charset[rnd.Next(charset.Length)];
            }
            return hid;
        }

        public static string Truncate(this string str, int maxLength, string ellipsis = "...") {
            if (str.Length < maxLength) return str;
            return str.Substring(0, maxLength - ellipsis.Length) + ellipsis;
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

    public static class Emojis {
        public static readonly string Warn = "\u26A0";
        public static readonly string Success = "\u2705";
        public static readonly string Error = "\u274C";
    }
}