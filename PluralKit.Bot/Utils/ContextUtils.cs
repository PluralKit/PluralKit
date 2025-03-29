using Autofac;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Gateway;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using NodaTime;

using PluralKit.Bot.Interactive;
using PluralKit.Core;

namespace PluralKit.Bot;

public static class ContextUtils
{
    public static async Task<bool> ConfirmClear(this Context ctx, string toClear)
    {
        if (!await ctx.PromptYesNo($"{Emojis.Warn} Are you sure you want to clear {toClear}?", "Clear"))
            throw Errors.GenericCancelled();
        return true;
    }

    public static async Task<bool> PromptYesNo(this Context ctx, string msgString, string acceptButton,
                                               User user = null, bool matchFlag = true)
    {
        if (matchFlag && ctx.MatchFlag("y", "yes")) return true;

        var prompt = new YesNoPrompt(ctx)
        {
            Message = msgString,
            AcceptLabel = acceptButton,
            User = user?.Id ?? ctx.Author.Id
        };

        await prompt.Run();

        return prompt.Result == true;
    }

    public static async Task<MessageReactionAddEvent> AwaitReaction(this Context ctx, Message message,
            User user, Func<MessageReactionAddEvent, bool> predicate = null, Duration? timeout = null)
    {
        // check if http gateway and set listener
        if (ctx.Cache is HttpDiscordCache)
            await (ctx.Cache as HttpDiscordCache).AwaitReaction(ctx.Guild?.Id ?? 0, message.Id, user!.Id, timeout);

        bool ReactionPredicate(MessageReactionAddEvent evt)
        {
            if (message.Id != evt.MessageId) return false; // Ignore reactions for different messages
            if (user != null && user.Id != evt.UserId)
                return false; // Ignore messages from other users if a user was defined
            if (predicate != null && !predicate.Invoke(evt)) return false; // Check predicate
            return true;
        }

        return await ctx.Services.Resolve<HandlerQueue<MessageReactionAddEvent>>()
            .WaitFor(ReactionPredicate, timeout);
    }

    public static async Task<bool> ConfirmWithReply(this Context ctx, string expectedReply, bool treatAsHid = false)
    {
        var timeout = Duration.FromMinutes(1);

        // check if http gateway and set listener
        if (ctx.Cache is HttpDiscordCache)
            await (ctx.Cache as HttpDiscordCache).AwaitMessage(ctx.Guild?.Id ?? 0, ctx.Channel.Id, ctx.Author.Id, timeout);

        bool Predicate(MessageCreateEvent e) =>
            e.Author.Id == ctx.Author.Id && e.ChannelId == ctx.Channel.Id;

        var msg = await ctx.Services.Resolve<HandlerQueue<MessageCreateEvent>>()
            .WaitFor(Predicate, timeout);

        var content = msg.Content;
        if (treatAsHid)
            content = content.ToLower().Replace("-", null);

        return string.Equals(content, expectedReply, StringComparison.InvariantCultureIgnoreCase);
    }

    public static async Task Paginate<T>(this Context ctx, IAsyncEnumerable<T> items, int totalCount,
        int itemsPerPage, string title, string color, Func<EmbedBuilder, IEnumerable<T>, Task> renderer)
    {
        // TODO: make this generic enough we can use it in Choose<T> below

        var buffer = new List<T>();
        await using var enumerator = items.GetAsyncEnumerator();

        var pageCount = (int)Math.Ceiling(totalCount / (double)itemsPerPage);

        async Task<Embed> MakeEmbedForPage(int page)
        {
            var bufferedItemsNeeded = (page + 1) * itemsPerPage;
            while (buffer.Count < bufferedItemsNeeded && await enumerator.MoveNextAsync())
                buffer.Add(enumerator.Current);

            var eb = new EmbedBuilder();
            eb.Title(pageCount > 1 ? $"[{page + 1}/{pageCount}] {title}" : title);
            if (color != null)
                eb.Color(color.ToDiscordColor());
            await renderer(eb, buffer.Skip(page * itemsPerPage).Take(itemsPerPage));
            return eb.Build();
        }

        async Task<int> PromptPageNumber()
        {
            var timeout = Duration.FromMinutes(0.5);

            // check if http gateway and set listener
            if (ctx.Cache is HttpDiscordCache)
                await (ctx.Cache as HttpDiscordCache).AwaitMessage(ctx.Guild?.Id ?? 0, ctx.Channel.Id, ctx.Author.Id, timeout);

            bool Predicate(MessageCreateEvent e) =>
                e.Author.Id == ctx.Author.Id && e.ChannelId == ctx.Channel.Id;

            var msg = await ctx.Services.Resolve<HandlerQueue<MessageCreateEvent>>()
                .WaitFor(Predicate, timeout);

            int.TryParse(msg.Content, out var num);

            return num;
        }

        try
        {
            var msg = await ctx.Reply(embed: await MakeEmbedForPage(0));

            // If we only have one (or no) page, don't bother with the reaction/pagination logic, lol
            if (pageCount <= 1) return;

            string[] botEmojis = { "\u23EA", "\u2B05", "\u27A1", "\u23E9", "\uD83D\uDD22", Emojis.Error };

            var _ = ctx.Rest.CreateReactionsBulk(msg, botEmojis); // Again, "fork"

            try
            {
                var currentPage = 0;
                while (true)
                {
                    var reaction = await ctx.AwaitReaction(msg, ctx.Author, timeout: Duration.FromMinutes(5));

                    // Increment/decrement page counter based on which reaction was clicked
                    if (reaction.Emoji.Name == "\u23EA") currentPage = 0; // <<
                    else if (reaction.Emoji.Name == "\u2B05") currentPage = (currentPage - 1) % pageCount; // <
                    else if (reaction.Emoji.Name == "\u27A1") currentPage = (currentPage + 1) % pageCount; // >
                    else if (reaction.Emoji.Name == "\u23E9") currentPage = pageCount - 1; // >>
                    else if (reaction.Emoji.Name == Emojis.Error) break; // X

                    else if (reaction.Emoji.Name == "\u0031\uFE0F\u20E3") currentPage = 0;
                    else if (reaction.Emoji.Name == "\u0032\uFE0F\u20E3") currentPage = 1;
                    else if (reaction.Emoji.Name == "\u0033\uFE0F\u20E3") currentPage = 2;
                    else if (reaction.Emoji.Name == "\u0034\uFE0F\u20E3" && pageCount >= 3) currentPage = 3;
                    else if (reaction.Emoji.Name == "\u0035\uFE0F\u20E3" && pageCount >= 4) currentPage = 4;
                    else if (reaction.Emoji.Name == "\u0036\uFE0F\u20E3" && pageCount >= 5) currentPage = 5;
                    else if (reaction.Emoji.Name == "\u0037\uFE0F\u20E3" && pageCount >= 6) currentPage = 6;
                    else if (reaction.Emoji.Name == "\u0038\uFE0F\u20E3" && pageCount >= 7) currentPage = 7;
                    else if (reaction.Emoji.Name == "\u0039\uFE0F\u20E3" && pageCount >= 8) currentPage = 8;
                    else if (reaction.Emoji.Name == "\U0001f51f" && pageCount >= 9) currentPage = 9;

                    else if (reaction.Emoji.Name == "\uD83D\uDD22")
                        try
                        {
                            await ctx.Reply("What page would you like to go to?");
                            var repliedNum = await PromptPageNumber();
                            if (repliedNum < 1)
                            {
                                await ctx.Reply($"{Emojis.Error} Operation canceled (invalid number).");
                                continue;
                            }

                            if (repliedNum > pageCount)
                            {
                                await ctx.Reply(
                                    $"{Emojis.Error} That page number is too high (page count is {pageCount}).");
                                continue;
                            }

                            currentPage = repliedNum - 1;
                        }
                        catch (TimeoutException)
                        {
                            await ctx.Reply($"{Emojis.Error} Operation timed out, sorry. Try again, perhaps?");
                            continue;
                        }

                    // C#'s % operator is dumb and wrong, so we fix negative numbers
                    if (currentPage < 0) currentPage += pageCount;

                    // If we can, remove the user's reaction (so they can press again quickly)
                    if ((await ctx.BotPermissions).HasFlag(PermissionSet.ManageMessages))
                        try
                        {
                            await ctx.Rest.DeleteUserReaction(msg.ChannelId, msg.Id, reaction.Emoji, reaction.UserId);
                        }
                        catch (TooManyRequestsException)
                        {
                            continue;
                        }

                    // Edit the embed with the new page
                    var embed = await MakeEmbedForPage(currentPage);
                    await ctx.Rest.EditMessage(msg.ChannelId, msg.Id, new MessageEditRequest { Embeds = new[] { embed } });
                }
            }
            catch (TimeoutException)
            {
                // "escape hatch", clean up as if we hit X
            }

            // todo: re-check
            if ((await ctx.BotPermissions).HasFlag(PermissionSet.ManageMessages))
                await ctx.Rest.DeleteAllReactions(msg.ChannelId, msg.Id);
        }
        // If we get a "NotFound" error, the message has been deleted and thus not our problem
        catch (NotFoundException) { }
        // If we get an "Unauthorized" error, we don't have permissions to remove our reaction
        // which means we probably didn't add it in the first place, or permissions changed since then
        // either way, nothing to do here
        catch (ForbiddenException) { }
    }

    public static async Task<T> Choose<T>(this Context ctx, string description, IList<T> items,
                                          Func<T, string> display = null)
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
            var pageCount = (items.Count - 1) / pageSize + 1;

            // Send the original message
            var msg = await ctx.Reply(
                $"**[Page {currPage + 1}/{pageCount}]**\n{description}\n{MakeOptionList(currPage)}");

            // Add back/forward reactions and the actual indicator emojis
            async Task AddEmojis()
            {
                await ctx.Rest.CreateReaction(msg.ChannelId, msg.Id, new Emoji { Name = "\u2B05" });
                await ctx.Rest.CreateReaction(msg.ChannelId, msg.Id, new Emoji { Name = "\u27A1" });
                for (var i = 0; i < items.Count; i++)
                    await ctx.Rest.CreateReaction(msg.ChannelId, msg.Id, new Emoji { Name = indicators[i] });
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
                    new MessageEditRequest
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
                for (var i = 0; i < items.Count; i++)
                    await ctx.Rest.CreateReaction(msg.ChannelId, msg.Id, new Emoji { Name = indicators[i] });
            }

            var _ = AddEmojis();

            // Then wait for a reaction and return whichever one we found
            var reaction = await ctx.AwaitReaction(msg, ctx.Author, rx => indicators.Contains(rx.Emoji.Name));
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

    public static async Task<T> BusyIndicator<T>(this Context ctx, Func<Task<T>> f,
                                                 string emoji = "\u23f3" /* hourglass */)
    {
        var task = f();

        // If we don't have permission to add reactions, don't bother, and just await the task normally.
        if (!await DiscordUtils.HasReactionPermissions(ctx)) return await task;

        try
        {
            await Task.WhenAll(
                ctx.Rest.CreateReaction(ctx.Message.ChannelId, ctx.Message.Id, new Emoji { Name = emoji }),
                task
            );
            return await task;
        }
        finally
        {
            var _ = ctx.Rest.DeleteOwnReaction(ctx.Message.ChannelId, ctx.Message.Id, new Emoji { Name = emoji });
        }
    }
}