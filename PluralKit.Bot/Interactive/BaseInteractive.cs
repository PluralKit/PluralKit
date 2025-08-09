using Autofac;

using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using NodaTime;

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

    protected async Task Update(InteractionContext ctx)
    {
        await ctx.Respond(InteractionResponse.ResponseType.UpdateMessage,
            new InteractionApplicationCommandCallbackData { Components = GetComponents() });
    }

    protected async Task Finish(InteractionContext? ctx = null)
    {
        foreach (var button in _buttons)
            button.Disabled = true;

        if (ctx != null)
            await Update(ctx);
        else
            await _ctx.Rest.EditMessage(_message.ChannelId, _message.Id,
                new MessageEditRequest { Components = GetComponents() });

        _tcs.TrySetResult();
    }

    protected async Task Send(string? content = null, Embed? embed = null, AllowedMentions? mentions = null)
    {
        _message = await _ctx.Rest.CreateMessage(_ctx.Channel.Id,
            new MessageRequest
            {
                Content = content,
                Embeds = embed != null ? new[] { embed } : null,
                AllowedMentions = mentions,
                Components = GetComponents()
            });
    }

    public MessageComponent[] GetComponents()
    {
        return new MessageComponent[]
        {
            new()
            {
                Type = ComponentType.ActionRow,
                Components = _buttons.Select(b => b.ToMessageComponent()).ToArray()
            }
        };
    }

    public void Setup(Context ctx)
    {
        var dispatch = ctx.Services.Resolve<InteractionDispatchService>();
        foreach (var button in _buttons)
            button.CustomId = dispatch.Register(_ctx.ShardId, button.Handler, Timeout);
    }

    public abstract Task Start();

    public async Task Run()
    {
        if (_running)
            throw new InvalidOperationException("Action is already running");
        _running = true;

        await Start();

        var cts = new CancellationTokenSource(Timeout.ToTimeSpan());
        cts.Token.Register(() => _tcs.TrySetException(new TimeoutException("Action timed out")));

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