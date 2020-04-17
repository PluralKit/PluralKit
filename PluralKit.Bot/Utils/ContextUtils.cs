using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;

using PluralKit.Core;

namespace PluralKit.Bot {
    public static class ContextUtils {
        public static async Task<bool> PromptYesNo(this Context ctx, DiscordMessage message, DiscordUser user = null, TimeSpan? timeout = null) {
            // "Fork" the task adding the reactions off so we don't have to wait for them to be finished to start listening for presses
            var _ = message.CreateReactionsBulk(new[] {Emojis.Success, Emojis.Error});
            var reaction = await ctx.AwaitReaction(message, user ?? ctx.Author, r => r.Emoji.Name == Emojis.Success || r.Emoji.Name == Emojis.Error, timeout ?? TimeSpan.FromMinutes(1));
            return reaction.Emoji.Name == Emojis.Success;
        }

        public static async Task<MessageReactionAddEventArgs> AwaitReaction(this Context ctx, DiscordMessage message, DiscordUser user = null, Func<MessageReactionAddEventArgs, bool> predicate = null, TimeSpan? timeout = null) {
            var tcs = new TaskCompletionSource<MessageReactionAddEventArgs>();
            Task Inner(MessageReactionAddEventArgs args) {
                if (message.Id != args.Message.Id) return Task.CompletedTask; // Ignore reactions for different messages
                if (user != null && user.Id != args.User.Id) return Task.CompletedTask; // Ignore messages from other users if a user was defined
                if (predicate != null && !predicate.Invoke(args)) return Task.CompletedTask; // Check predicate
                tcs.SetResult(args);
                return Task.CompletedTask;
            }

            ctx.Shard.MessageReactionAdded += Inner;
            try {
                return await tcs.Task.TimeoutAfter(timeout);
            } finally {
                ctx.Shard.MessageReactionAdded -= Inner;
            }
        }

        public static async Task<DiscordMessage> AwaitMessage(this Context ctx, DiscordChannel channel, DiscordUser user = null, Func<DiscordMessage, bool> predicate = null, TimeSpan? timeout = null) {
            var tcs = new TaskCompletionSource<DiscordMessage>();
            Task Inner(MessageCreateEventArgs args)
            {
                var msg = args.Message;
                if (channel != msg.Channel) return Task.CompletedTask; // Ignore messages in a different channel
                if (user != null && user != msg.Author) return Task.CompletedTask; // Ignore messages from other users
                if (predicate != null && !predicate.Invoke(msg)) return Task.CompletedTask; // Check predicate
                tcs.SetResult(msg);
                return Task.CompletedTask;
            }

            ctx.Shard.MessageCreated += Inner;
            try
            {
                return await (tcs.Task.TimeoutAfter(timeout));
            }
            finally
            {
                ctx.Shard.MessageCreated -= Inner;
            }
        }
        
        public static async Task<bool> ConfirmWithReply(this Context ctx, string expectedReply)
        {
            var msg = await ctx.AwaitMessage(ctx.Channel, ctx.Author, timeout: TimeSpan.FromMinutes(1));
            return string.Equals(msg.Content, expectedReply, StringComparison.InvariantCultureIgnoreCase);
        }

        public static async Task Paginate<T>(this Context ctx, IAsyncEnumerable<T> items, int totalCount, int itemsPerPage, string title, Func<DiscordEmbedBuilder, IEnumerable<T>, Task> renderer) {
            // TODO: make this generic enough we can use it in Choose<T> below

            var buffer = new List<T>();
            await using var enumerator = items.GetAsyncEnumerator();
            
            var pageCount = (totalCount / itemsPerPage) + 1;
            async Task<DiscordEmbed> MakeEmbedForPage(int page)
            {
                var bufferedItemsNeeded = (page + 1) * itemsPerPage;
                while (buffer.Count < bufferedItemsNeeded && await enumerator.MoveNextAsync())
                    buffer.Add(enumerator.Current);

                var eb = new DiscordEmbedBuilder();
                eb.Title = pageCount > 1 ? $"[{page+1}/{pageCount}] {title}" : title;
                await renderer(eb, buffer.Skip(page*itemsPerPage).Take(itemsPerPage));
                return eb.Build();
            }

            try
            {
                var msg = await ctx.Reply(embed: await MakeEmbedForPage(0));
                if (pageCount == 1) return; // If we only have one page, don't bother with the reaction/pagination logic, lol
                string[] botEmojis = { "\u23EA", "\u2B05", "\u27A1", "\u23E9", Emojis.Error };
                
                var _ = msg.CreateReactionsBulk(botEmojis); // Again, "fork"

                try {
                    var currentPage = 0;
                    while (true) {
                        var reaction = await ctx.AwaitReaction(msg, ctx.Author, timeout: TimeSpan.FromMinutes(5));

                        // Increment/decrement page counter based on which reaction was clicked
                        if (reaction.Emoji.Name == "\u23EA") currentPage = 0; // <<
                        if (reaction.Emoji.Name == "\u2B05") currentPage = (currentPage - 1) % pageCount; // <
                        if (reaction.Emoji.Name == "\u27A1") currentPage = (currentPage + 1) % pageCount; // >
                        if (reaction.Emoji.Name == "\u23E9") currentPage = pageCount - 1; // >>
                        if (reaction.Emoji.Name == Emojis.Error) break; // X
                        
                        // C#'s % operator is dumb and wrong, so we fix negative numbers
                        if (currentPage < 0) currentPage += pageCount;
                        
                        // If we can, remove the user's reaction (so they can press again quickly)
                        if (ctx.BotHasPermission(Permissions.ManageMessages)) await msg.DeleteReactionAsync(reaction.Emoji, reaction.User);
                        
                        // Edit the embed with the new page
                        var embed = await MakeEmbedForPage(currentPage);
                        await msg.ModifyAsync(embed: embed);
                    }
                } catch (TimeoutException) {
                    // "escape hatch", clean up as if we hit X
                }

                if (ctx.BotHasPermission(Permissions.ManageMessages)) await msg.DeleteAllReactionsAsync();
            }
            // If we get a "NotFound" error, the message has been deleted and thus not our problem
            catch (NotFoundException) { }
        }
        
        public static async Task<T> Choose<T>(this Context ctx, string description, IList<T> items, Func<T, string> display = null)
        {
            // Generate a list of :regional_indicator_?: emoji surrogate pairs (starting at codepoint 0x1F1E6)
            // We just do 7 (ABCDEFG), this amount is arbitrary (although sending a lot of emojis takes a while)
            var pageSize = 7;
            var indicators = new string[pageSize];
            for (var i = 0; i < pageSize; i++) indicators[i] = char.ConvertFromUtf32(0x1F1E6 + i);

            // Default to x.ToString()
            if (display == null) display = x => x.ToString();

            string MakeOptionList(int page)
            {
                var makeOptionList = string.Join("\n", items
                    .Skip(page * pageSize)
                    .Take(pageSize)
                    .Select((x, i) => $"{indicators[i]} {display(x)}"));
                return makeOptionList;
            }

            // If we have more items than the page size, we paginate as appropriate
            if (items.Count > pageSize)
            {
                var currPage = 0;
                var pageCount = (items.Count-1) / pageSize + 1;
                
                // Send the original message
                var msg = await ctx.Reply($"**[Page {currPage + 1}/{pageCount}]**\n{description}\n{MakeOptionList(currPage)}");
                
                // Add back/forward reactions and the actual indicator emojis
                async Task AddEmojis()
                {
                    await msg.CreateReactionAsync(DiscordEmoji.FromUnicode("\u2B05"));
                    await msg.CreateReactionAsync(DiscordEmoji.FromUnicode("\u27A1"));
                    for (int i = 0; i < items.Count; i++) await msg.CreateReactionAsync(DiscordEmoji.FromUnicode(indicators[i]));
                }

                var _ = AddEmojis(); // Not concerned about awaiting
                
                while (true)
                {
                    // Wait for a reaction
                    var reaction = await ctx.AwaitReaction(msg, ctx.Author);
                    
                    // If it's a movement reaction, inc/dec the page index
                    if (reaction.Emoji.Name == "\u2B05") currPage -= 1; // <
                    if (reaction.Emoji.Name == "\u27A1") currPage += 1; // >
                    if (currPage < 0) currPage += pageCount;
                    if (currPage >= pageCount) currPage -= pageCount;

                    // If it's an indicator emoji, return the relevant item
                    if (indicators.Contains(reaction.Emoji.Name))
                    {
                        var idx = Array.IndexOf(indicators, reaction.Emoji.Name) + pageSize * currPage;
                        // only if it's in bounds, though
                        // eg. 8 items, we're on page 2, and I hit D (3 + 1*7 = index 10 on an 8-long list) = boom 
                        if (idx < items.Count) return items[idx];
                    }

                    var __ = msg.DeleteReactionAsync(reaction.Emoji, ctx.Author); // don't care about awaiting
                    await msg.ModifyAsync($"**[Page {currPage + 1}/{pageCount}]**\n{description}\n{MakeOptionList(currPage)}");
                }
            }
            else
            {
                var msg = await ctx.Reply($"{description}\n{MakeOptionList(0)}");

                // Add the relevant reactions (we don't care too much about awaiting)
                async Task AddEmojis()
                {
                    for (int i = 0; i < items.Count; i++) await msg.CreateReactionAsync(DiscordEmoji.FromUnicode(indicators[i]));
                }

                var _ = AddEmojis();

                // Then wait for a reaction and return whichever one we found
                var reaction = await ctx.AwaitReaction(msg, ctx.Author,rx => indicators.Contains(rx.Emoji.Name));
                return items[Array.IndexOf(indicators, reaction.Emoji.Name)];
            }
        }

        public static Permissions BotPermissions(this Context ctx) => ctx.Channel.BotPermissions();

        public static bool BotHasPermission(this Context ctx, Permissions permission) =>
            ctx.Channel.BotHasPermission(permission);

        public static async Task BusyIndicator(this Context ctx, Func<Task> f, string emoji = "\u23f3" /* hourglass */)
        {
            await ctx.BusyIndicator<object>(async () =>
            {
                await f();
                return null;
            }, emoji);
        }

        public static async Task<T> BusyIndicator<T>(this Context ctx, Func<Task<T>> f, string emoji = "\u23f3" /* hourglass */)
        {
            var task = f();

            // If we don't have permission to add reactions, don't bother, and just await the task normally.
            var neededPermissions = Permissions.AddReactions | Permissions.ReadMessageHistory;
            if ((ctx.BotPermissions() & neededPermissions) != neededPermissions) return await task;

            try
            {
                await Task.WhenAll(ctx.Message.CreateReactionAsync(DiscordEmoji.FromUnicode(emoji)), task);
                return await task;
            }
            finally
            {
                var _ = ctx.Message.DeleteReactionAsync(DiscordEmoji.FromUnicode(emoji), ctx.Shard.CurrentUser);
            }
        } 
    }
}