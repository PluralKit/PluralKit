using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot {
    public static class ContextUtils {
        public static async Task<bool> PromptYesNo(this Context ctx, IUserMessage message, IUser user = null, TimeSpan? timeout = null) {
            await message.AddReactionsAsync(new[] {new Emoji(Emojis.Success), new Emoji(Emojis.Error)});
            var reaction = await ctx.AwaitReaction(message, user ?? ctx.Author, (r) => r.Emote.Name == Emojis.Success || r.Emote.Name == Emojis.Error, timeout ?? TimeSpan.FromMinutes(1));
            return reaction.Emote.Name == Emojis.Success;
        }

        public static async Task<SocketReaction> AwaitReaction(this Context ctx, IUserMessage message, IUser user = null, Func<SocketReaction, bool> predicate = null, TimeSpan? timeout = null) {
            var tcs = new TaskCompletionSource<SocketReaction>();
            Task Inner(Cacheable<IUserMessage, ulong> _message, ISocketMessageChannel _channel, SocketReaction reaction) {
                if (message.Id != _message.Id) return Task.CompletedTask; // Ignore reactions for different messages
                if (user != null && user.Id != reaction.UserId) return Task.CompletedTask; // Ignore messages from other users if a user was defined
                if (predicate != null && !predicate.Invoke(reaction)) return Task.CompletedTask; // Check predicate
                tcs.SetResult(reaction);
                return Task.CompletedTask;
            }

            ((BaseSocketClient) ctx.Shard).ReactionAdded += Inner;
            try {
                return await (tcs.Task.TimeoutAfter(timeout));
            } finally {
                ((BaseSocketClient) ctx.Shard).ReactionAdded -= Inner;
            }
        }

        public static async Task<IUserMessage> AwaitMessage(this Context ctx, IMessageChannel channel, IUser user = null, Func<SocketMessage, bool> predicate = null, TimeSpan? timeout = null) {
            var tcs = new TaskCompletionSource<IUserMessage>();
            Task Inner(SocketMessage msg) {
                if (channel != msg.Channel) return Task.CompletedTask; // Ignore messages in a different channel
                if (user != null && user != msg.Author) return Task.CompletedTask; // Ignore messages from other users
                if (predicate != null && !predicate.Invoke(msg)) return Task.CompletedTask; // Check predicate

                ((BaseSocketClient) ctx.Shard).MessageReceived -= Inner;
                tcs.SetResult(msg as IUserMessage);
                
                return Task.CompletedTask;
            }

            ((BaseSocketClient) ctx.Shard).MessageReceived += Inner;
            return await (tcs.Task.TimeoutAfter(timeout));
        }
        
        public static async Task<bool> ConfirmWithReply(this Context ctx, string expectedReply)
        {
            var msg = await ctx.AwaitMessage(ctx.Channel, ctx.Author, timeout: TimeSpan.FromMinutes(1));
            return string.Equals(msg.Content, expectedReply, StringComparison.InvariantCultureIgnoreCase);
        }

        public static async Task Paginate<T>(this Context ctx, ICollection<T> items, int itemsPerPage, string title, Action<EmbedBuilder, IEnumerable<T>> renderer) {
            // TODO: make this generic enough we can use it in Choose<T> below
            
            var pageCount = (items.Count / itemsPerPage) + 1;
            Embed MakeEmbedForPage(int page) {
                var eb = new EmbedBuilder();
                eb.Title = pageCount > 1 ? $"[{page+1}/{pageCount}] {title}" : title;
                renderer(eb, items.Skip(page*itemsPerPage).Take(itemsPerPage));
                return eb.Build();
            }

            var msg = await ctx.Channel.SendMessageAsync(embed: MakeEmbedForPage(0));
            if (pageCount == 1) return; // If we only have one page, don't bother with the reaction/pagination logic, lol
            var botEmojis = new[] { new Emoji("\u23EA"), new Emoji("\u2B05"), new Emoji("\u27A1"), new Emoji("\u23E9"), new Emoji(Emojis.Error) };
            await msg.AddReactionsAsync(botEmojis);

            try {
                var currentPage = 0;
                while (true) {
                    var reaction = await ctx.AwaitReaction(msg, ctx.Author, timeout: TimeSpan.FromMinutes(5));

                    // Increment/decrement page counter based on which reaction was clicked
                    if (reaction.Emote.Name == "\u23EA") currentPage = 0; // <<
                    if (reaction.Emote.Name == "\u2B05") currentPage = (currentPage - 1) % pageCount; // <
                    if (reaction.Emote.Name == "\u27A1") currentPage = (currentPage + 1) % pageCount; // >
                    if (reaction.Emote.Name == "\u23E9") currentPage = pageCount - 1; // >>
                    if (reaction.Emote.Name == Emojis.Error) break; // X
                    
                    // C#'s % operator is dumb and wrong, so we fix negative numbers
                    if (currentPage < 0) currentPage += pageCount;
                    
                    // If we can, remove the user's reaction (so they can press again quickly)
                    if (await ctx.HasPermission(ChannelPermission.ManageMessages) && reaction.User.IsSpecified) await msg.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                    
                    // Edit the embed with the new page
                    await msg.ModifyAsync((mp) => mp.Embed = MakeEmbedForPage(currentPage));
                }
            } catch (TimeoutException) {
                // "escape hatch", clean up as if we hit X
            }

            if (await ctx.HasPermission(ChannelPermission.ManageMessages)) await msg.RemoveAllReactionsAsync();
            else await msg.RemoveReactionsAsync(ctx.Shard.CurrentUser, botEmojis);
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
                var msg = await ctx.Channel.SendMessageAsync($"**[Page {currPage + 1}/{pageCount}]**\n{description}\n{MakeOptionList(currPage)}");
                
                // Add back/forward reactions and the actual indicator emojis
                async Task AddEmojis()
                {
                    await msg.AddReactionAsync(new Emoji("\u2B05"));
                    await msg.AddReactionAsync(new Emoji("\u27A1"));
                    for (int i = 0; i < items.Count; i++) await msg.AddReactionAsync(new Emoji(indicators[i]));
                }

                var _ = AddEmojis(); // Not concerned about awaiting


                while (true)
                {
                    // Wait for a reaction
                    var reaction = await ctx.AwaitReaction(msg, ctx.Author);
                    
                    // If it's a movement reaction, inc/dec the page index
                    if (reaction.Emote.Name == "\u2B05") currPage -= 1; // <
                    if (reaction.Emote.Name == "\u27A1") currPage += 1; // >
                    if (currPage < 0) currPage += pageCount;
                    if (currPage >= pageCount) currPage -= pageCount;

                    // If it's an indicator emoji, return the relevant item
                    if (indicators.Contains(reaction.Emote.Name))
                    {
                        var idx = Array.IndexOf(indicators, reaction.Emote.Name) + pageSize * currPage;
                        // only if it's in bounds, though
                        // eg. 8 items, we're on page 2, and I hit D (3 + 1*7 = index 10 on an 8-long list) = boom 
                        if (idx < items.Count) return items[idx];
                    }

                    var __ = msg.RemoveReactionAsync(reaction.Emote, ctx.Author); // don't care about awaiting
                    await msg.ModifyAsync(mp => mp.Content = $"**[Page {currPage + 1}/{pageCount}]**\n{description}\n{MakeOptionList(currPage)}");
                }
            }
            else
            {
                var msg = await ctx.Channel.SendMessageAsync($"{description}\n{MakeOptionList(0)}");

                // Add the relevant reactions (we don't care too much about awaiting)
                async Task AddEmojis()
                {
                    for (int i = 0; i < items.Count; i++) await msg.AddReactionAsync(new Emoji(indicators[i]));
                }

                var _ = AddEmojis();

                // Then wait for a reaction and return whichever one we found
                var reaction = await ctx.AwaitReaction(msg, ctx.Author,rx => indicators.Contains(rx.Emote.Name));
                return items[Array.IndexOf(indicators, reaction.Emote.Name)];
            }
        }

        public static async Task<ChannelPermissions> Permissions(this Context ctx) {
            if (ctx.Channel is IGuildChannel) {
                var gu = await ctx.Guild.GetCurrentUserAsync();
                return gu.GetPermissions(ctx.Channel as IGuildChannel);
            }
            return ChannelPermissions.DM;
        }

        public static async Task<bool> HasPermission(this Context ctx, ChannelPermission permission) => (await Permissions(ctx)).Has(permission);

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
            if (!await ctx.HasPermission(ChannelPermission.AddReactions)) return await task;

            try
            {
                await Task.WhenAll(ctx.Message.AddReactionAsync(new Emoji(emoji)), task);
                return await task;
            }
            finally
            {
                var _ = ctx.Message.RemoveReactionAsync(new Emoji(emoji), ctx.Shard.CurrentUser);
            }
        } 
    }
}