using Autofac;

using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using NodaTime;
using PluralKit.Core;

namespace PluralKit.Bot.Interactive;

public abstract class BaseInteractive
{
    protected readonly List<Button> _buttons = new();
    protected readonly Context _ctx;
    protected readonly TaskCompletionSource _tcs = new();
    protected bool _running;

    protected BaseInteractive(Context ctx)
    {
        _ctx = ctx;
    }

    protected Message _message { get; private set; }

    public Duration Timeout { get; set; } = Duration.FromMinutes(5);

    protected Button AddButton(Func<InteractionContext, Task> handler, string? label = null,
                               ButtonStyle style = ButtonStyle.Secondary, bool disabled = false)
    {
        var dispatch = _ctx.Services.Resolve<InteractionDispatchService>();
        var customId = dispatch.Register(_ctx.ShardId, handler, Timeout);

        var button = new Button
        {
            Label = label,
            Style = style,
            Disabled = disabled,
            CustomId = customId,
        };
        _buttons.Add(button);
        return button;
    }

    protected async Task Update(InteractionContext ctx, string? content = null)
    {
        await ctx.Respond(InteractionResponse.ResponseType.UpdateMessage,
            new InteractionApplicationCommandCallbackData { Components = await GetComponents(content) });
    }

    protected async Task Error(InteractionContext ctx, PKError error)
    {
        await ctx.Reply(content: $"{Emojis.Error} {error.Message}");
    }

    protected async Task Finish(InteractionContext? ctx = null, string? content = null)
    {
        foreach (var button in _buttons)
            button.Disabled = true;

        if (ctx != null)
            await Update(ctx, content);
        else
            await _ctx.Rest.EditMessage(_message.ChannelId, _message.Id,
                new MessageEditRequest { Components = await GetComponents(content) });

        _tcs.TrySetResult();
    }

    protected async Task Send(string? content = null, AllowedMentions? mentions = null)
    {
        _message = await _ctx.Rest.CreateMessage(_ctx.Channel.Id,
            new MessageRequest
            {
                AllowedMentions = mentions,
                Components = await GetComponents(content),
                Flags = Message.MessageFlags.IsComponentsV2
            });
        if (_ctx.Guild != null)
            await _ctx.Repository.AddCommandMessage(new Core.CommandMessage
            {
                Mid = _message.Id,
                Guild = _ctx.Guild!.Id,
                Channel = _ctx.Channel.Id,
                Sender = _ctx.Author.Id,
                OriginalMid = _ctx.Message.Id,
            });
    }

    public virtual async Task<MessageComponent[]> GetComponents(string? content = null)
    {
        List<MessageComponent> components = [];
        if (content != null)
        {
            components.Add(new()
            {
                Type = ComponentType.Text,
                Content = content
            });
        }

        foreach (var line in _buttons.Chunk(5))
        {
            components.Add(new()
            {
                Type = ComponentType.ActionRow,
                Components = line.Select(b => b.ToMessageComponent()).ToArray()
            });
        }

        return [.. components];
    }

    public void Setup(Context ctx)
    {
        var dispatch = ctx.Services.Resolve<InteractionDispatchService>();
        foreach (var button in _buttons)
            button.CustomId = dispatch.Register(_ctx.ShardId, button.Handler, Timeout);
    }

    public abstract Task Start();

    public async Task Run(bool exceptionOnTimeout = true)
    {
        if (_running)
            throw new InvalidOperationException("Action is already running");
        _running = true;

        await Start();

        var cts = new CancellationTokenSource(Timeout.ToTimeSpan());
        cts.Token.Register(exceptionOnTimeout ? () => _tcs.TrySetException(new TimeoutException("Action timed out")) : async () => await Finish());

        try
        {
            await _tcs.Task;
        }
        finally
        {
            Cleanup();
        }
    }

    protected void Cleanup()
    {
        var dispatch = _ctx.Services.Resolve<InteractionDispatchService>();
        foreach (var button in _buttons)
            dispatch.Unregister(button.CustomId!);
    }
}