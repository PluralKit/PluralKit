using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Autofac;

using Myriad.Builders;
using Myriad.Gateway;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot {
    public static class ContextUtils {
        public static async Task<bool> ConfirmClear(this Context ctx, string toClear)
        {
            if (!(await ctx.PromptYesNo($"{Emojis.Warn} Are you sure you want to clear {toClear}?"))) throw Errors.GenericCancelled();
            else return true;
        }

        public static async Task<bool> PromptYesNo(this Context ctx, string msgString, User user = null, Duration? timeout = null, AllowedMentions mentions = null, bool matchFlag = true)
        {
            Message message;
            if (matchFlag && ctx.MatchFlag("y", "yes")) return true;
            else message = await ctx.Reply(msgString, mentions: mentions);
            var cts = new CancellationTokenSource();
            if (user == null) user = ctx.Author;
            if (timeout == null) timeout = Duration.FromMinutes(5);
            
            if (!DiscordUtils.HasReactionPermissions(ctx)) 
                await ctx.Reply($"{Emojis.Note} PluralKit does not have permissions to add reactions in this channel. \nPlease reply with 'yes' to confirm, or 'no' to cancel.");
            else
            // "Fork" the task adding the reactions off so we don't have to wait for them to be finished to start listening for presses
            await ctx.Rest.CreateReactionsBulk(message, new[] {Emojis.Success, Emojis.Error});
            
            bool ReactionPredicate(MessageReactionAddEvent e)
            {
                if (e.ChannelId != message.ChannelId || e.MessageId != message.Id) return false;
                if (e.UserId != user.Id) return false;
                return true;
            }

            bool MessagePredicate(MessageCreateEvent e)
            {
                if (e.ChannelId != message.ChannelId) return false;
                if (e.Author.Id != user.Id) return false;

                var strings = new [] {"y", "yes", "n", "no"};
                return strings.Any(str => string.Equals(e.Content, str, StringComparison.InvariantCultureIgnoreCase));
            }

            var messageTask = ctx.Services.Resolve<HandlerQueue<MessageCreateEvent>>().WaitFor(MessagePredicate, timeout, cts.Token);
            var reactionTask = ctx.Services.Resolve<HandlerQueue<MessageReactionAddEvent>>().WaitFor(ReactionPredicate, timeout, cts.Token);
            
            var theTask = await Task.WhenAny(messageTask, reactionTask);
            cts.Cancel();

            if (theTask == messageTask)
            {
                var responseMsg = (await messageTask);
                var positives = new[] {"y", "yes"};
                return positives.Any(p => string.Equals(responseMsg.Content, p, StringComparison.InvariantCultureIgnoreCase));
            }

            if (theTask == reactionTask) 
                return (await reactionTask).Emoji.Name == Emojis.Success;

            return false;
        }

        public static async Task<MessageReactionAddEvent> AwaitReaction(this Context ctx, Message message, User user = null, Func<MessageReactionAddEvent, bool> predicate = null, Duration? timeout = null)
        {
            bool ReactionPredicate(MessageReactionAddEvent evt)
            {
                if (message.Id != evt.MessageId) return false; // Ignore reactions for different messages
                if (user != null && user.Id != evt.UserId) return false; // Ignore messages from other users if a user was defined
                if (predicate != null && !predicate.Invoke(evt)) return false; // Check predicate
                return true;
            }
            
            return await ctx.Services.Resolve<HandlerQueue<MessageReactionAddEvent>>().WaitFor(ReactionPredicate, timeout);
        }

        public static async Task<bool> ConfirmWithReply(this Context ctx, string expectedReply)
        {
            bool Predicate(MessageCreateEvent e) =>
                e.Author.Id == ctx.Author.Id && e.ChannelId == ctx.Channel.Id;
            
            var msg = await ctx.Services.Resolve<HandlerQueue<MessageCreateEvent>>()
                .WaitFor(Predicate, Duration.FromMinutes(1));
            
            return string.Equals(msg.Content, expectedReply, StringComparison.InvariantCultureIgnoreCase);
        }

        public static async Task Paginate<T>(this Context ctx, IAsyncEnumerable<T> items, int totalCount, int itemsPerPage, string title, string color, Func<EmbedBuilder, IEnumerable<T>, Task> renderer) {
            // TODO: make this generic enough we can use it in Choose<T> below

            var buffer = new List<T>();
            await using var enumerator = items.GetAsyncEnumerator();

            var pageCount = (int) Math.Ceiling(totalCount / (double) itemsPerPage);
            async Task<Embed> MakeEmbedForPage(int page)
            {
                var bufferedItemsNeeded = (page + 1) * itemsPerPage;
                while (buffer.Count < bufferedItemsNeeded && await enumerator.MoveNextAsync())
                    buffer.Add(enumerator.Current);

                var eb = new EmbedBuilder();
                eb.Title(pageCount > 1 ? $"[{page+1}/{pageCount}] {title}" : title);
                if (color != null)
                    eb.Color(color.ToDiscordColor());
                await renderer(eb, buffer.Skip(page*itemsPerPage).Take(itemsPerPage));
                return eb.Build();
            }

            try
            {
                var msg = await ctx.Reply(embed: await MakeEmbedForPage(0));
                if (pageCount <= 1) return; // If we only have one (or no) page, don't bother with the reaction/pagination logic, lol
                string[] botEmojis = { "\u23EA", "\u2B05", "\u27A1", "\u23E9", Emojis.Error };

                var _ = ctx.Rest.CreateReactionsBulk(msg, botEmojis); // Again, "fork"

                try {
                    var currentPage = 0;
                    while (true) {
                        var reaction = await ctx.AwaitReaction(msg, ctx.Author, timeout: Duration.FromMinutes(5));

                        // Increment/decrement page counter based on which reaction was clicked
                        if (reaction.Emoji.Name == "\u23EA") currentPage = 0; // <<
                        if (reaction.Emoji.Name == "\u2B05") currentPage = (currentPage - 1) % pageCount; // <
                        if (reaction.Emoji.Name == "\u27A1") currentPage = (currentPage + 1) % pageCount; // >
                        if (reaction.Emoji.Name == "\u23E9") currentPage = pageCount - 1; // >>
                        if (reaction.Emoji.Name == Emojis.Error) break; // X
                        
                        // C#'s % operator is dumb and wrong, so we fix negative numbers
                        if (currentPage < 0) currentPage += pageCount;
                        
                        // If we can, remove the user's reaction (so they can press again quickly)
                        if (ctx.BotPermissions.HasFlag(PermissionSet.ManageMessages))
                            await ctx.Rest.DeleteUserReaction(msg.ChannelId, msg.Id, reaction.Emoji, reaction.UserId);
                        
                        // Edit the embed with the new page
                        var embed = await MakeEmbedForPage(currentPage);
                        await ctx.Rest.EditMessage(msg.ChannelId, msg.Id, new MessageEditRequest {Embed = embed});
                    }
                } catch (TimeoutException) {
                    // "escape hatch", clean up as if we hit X
                }

                if (ctx.BotPermissions.HasFlag(PermissionSet.ManageMessages))
                    await ctx.Rest.DeleteAllReactions(msg.ChannelId, msg.Id);
            }
            // If we get a "NotFound" error, the message has been deleted and thus not our problem
            catch (NotFoundException) { }
            // If we get an "Unauthorized" error, we don't have permissions to remove our reaction
            // which means we probably didn't add it in the first place, or permissions changed since then
            // either way, nothing to do here
            catch (UnauthorizedException) { }
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
                    await ctx.Rest.CreateReaction(msg.ChannelId, msg.Id, new() { Name = "\u2B05" });
                    await ctx.Rest.CreateReaction(msg.ChannelId, msg.Id, new() { Name = "\u27A1" });
                    for (int i = 0; i < items.Count; i++) 
                        await ctx.Rest.CreateReaction(msg.ChannelId, msg.Id, new() { Name = indicators[i] });
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

                    var __ = ctx.Rest.DeleteUserReaction(msg.ChannelId, msg.Id, reaction.Emoji, ctx.Author.Id);
                    await ctx.Rest.EditMessage(msg.ChannelId, msg.Id,
                        new()
                        {
                            Content =
                                $"**[Page {currPage + 1}/{pageCount}]**\n{description}\n{MakeOptionList(currPage)}"
                        });
                }
            }
            else
            {
                var msg = await ctx.Reply($"{description}\n{MakeOptionList(0)}");

                // Add the relevant reactions (we don't care too much about awaiting)
                async Task AddEmojis()
                {
                    for (int i = 0; i < items.Count; i++)
                        await ctx.Rest.CreateReaction(msg.ChannelId, msg.Id, new() {Name = indicators[i]});
                }

                var _ = AddEmojis();

                // Then wait for a reaction and return whichever one we found
                var reaction = await ctx.AwaitReaction(msg, ctx.Author,rx => indicators.Contains(rx.Emoji.Name));
                return items[Array.IndexOf(indicators, reaction.Emoji.Name)];
            }
        }
        
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
            if (!DiscordUtils.HasReactionPermissions(ctx)) return await task;

            try
            {
                await Task.WhenAll(ctx.Rest.CreateReaction(ctx.Message.ChannelId, ctx.Message.Id, new() {Name = emoji}), task);
                return await task;
            }
            finally
            {
                var _ = ctx.Rest.DeleteOwnReaction(ctx.Message.ChannelId, ctx.Message.Id, new() { Name = emoji });
            }
        } 
    }
}