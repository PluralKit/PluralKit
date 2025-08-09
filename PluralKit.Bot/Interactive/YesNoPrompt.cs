using Autofac;

using Myriad.Cache;
using Myriad.Gateway;
using Myriad.Rest.Types;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot.Interactive;

public class YesNoPrompt: BaseInteractive
{
    public YesNoPrompt(Context ctx) : base(ctx)
    {
        User = ctx.Author.Id;
    }

    public bool? Result { get; private set; }
    public ulong? User { get; set; }
    public string Message { get; set; } = "Are you sure?";

    public string AcceptLabel { get; set; } = "OK";
    public ButtonStyle AcceptStyle { get; set; } = ButtonStyle.Primary;

    public string CancelLabel { get; set; } = "Cancel";
    public ButtonStyle CancelStyle { get; set; } = ButtonStyle.Secondary;

    public override async Task Start()
    {
        AddButton(ctx => OnButtonClick(ctx, true), AcceptLabel, AcceptStyle);
        AddButton(ctx => OnButtonClick(ctx, false), CancelLabel, CancelStyle);

        AllowedMentions mentions = null;

        if (User != _ctx.Author.Id)
            mentions = new AllowedMentions { Users = new[] { User!.Value } };

        await Send(Message, mentions: mentions);
    }

    private async Task OnButtonClick(InteractionContext ctx, bool result)
    {
        if (ctx.User.Id != User)
        {
            await Update(ctx);
            return;
        }

        Result = result;
        await Finish(ctx);
    }

    private bool MessagePredicate(MessageCreateEvent e)
    {
        if (e.ChannelId != _ctx.Channel.Id) return false;
        if (e.Author.Id != User) return false;

        var response = e.Content.ToLowerInvariant();

        if (response == "y" || response == "yes")
        {
            Result = true;
            return true;
        }

        if (response == "n" || response == "no")
        {
            Result = false;
            return true;
        }

        // no need to reawait message
        // gateway will already have sent us only matching messages

        return false;
    }

    public new async Task Run()
    {
        // todo: can we split this up somehow so it doesn't need to be *completely* copied from BaseInteractive?

        var cts = new CancellationTokenSource(Timeout.ToTimeSpan());

        if (_running)
            throw new InvalidOperationException("Action is already running");
        _running = true;

        var queue = _ctx.Services.Resolve<HandlerQueue<MessageCreateEvent>>();

        async Task WaitForMessage()
        {
            try
            {
                // check if http gateway and set listener
                // todo: this one needs to handle options for message
                if (_ctx.Cache is HttpDiscordCache)
                    await (_ctx.Cache as HttpDiscordCache).AwaitMessage(
                        _ctx.Guild?.Id ?? 0,
                        _ctx.Channel.Id,
                        _ctx.Author.Id,
                        Timeout,
                        options: new[] { "yes", "y", "no", "n" }
                    );

                await queue.WaitFor(MessagePredicate, Timeout, cts.Token);
            }
            catch (TimeoutException e)
            {
                if (e.Message != "HandlerQueue#WaitFor timed out")
                    throw;
            }
        }

        await Start();

        var messageDispatch = WaitForMessage();

        cts.Token.Register(() => _tcs.TrySetException(new TimeoutException("YesNoPrompt timed out")));

        try
        {
            var doneTask = await Task.WhenAny(_tcs.Task, messageDispatch);
        }
        finally
        {
            await Finish();
            Cleanup();
        }
    }
}