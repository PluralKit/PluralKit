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
    }

    public static class Emojis {
        public static readonly string Warn = "\u26A0";
        public static readonly string Success = "\u2705";
        public static readonly string Error = "\u274C";
        public static readonly string Note = "\u2757";
    }
}