using Autofac;

using Myriad.Types;

namespace PluralKit.Bot.Interactive;

public class ListInteractive(Context ctx): BaseInteractive(ctx)
{
    public bool? Result { get; private set; }
    public ulong? User { get; set; } = ctx.Author.Id;
    public ButtonStyle Style { get; set; } = ButtonStyle.Primary;
    public int Page { get; set; } = 0;
    public int PageCount { get; set; } = 1;
    public Func<int, (MessageComponent, MessageComponent, List<MessageComponent>)> ContentBuilder { get; init; }
    public Func<ApplicationCommandInteractionData?, Task<int>> Renderer { get; init; }
    public string? Color { get; set; }
    public Func<MessageComponent[]>? OptionsModal { get; init; }

    public override async Task Start()
    {
        if (PageCount > 1)
        {
            AddButton(ctx => OnButtonClick(ctx, "first"), "\u23EA", Style); // <<
            AddButton(ctx => OnButtonClick(ctx, "previous"), "\u2B05", Style); // <
            AddButton(ctx => OnButtonClick(ctx, "next"), "\u27A1", Style); // >
            AddButton(ctx => OnButtonClick(ctx, "last"), "\u23E9", Style); // >>
            AddButton(ctx => OnButtonClick(ctx, "input_page"), "\uD83D\uDD22", Style); // 1234
        }

        if (OptionsModal != null)
            AddButton(ctx => OnButtonClick(ctx, "options"), "List Options", ButtonStyle.Secondary);
        AddButton(ctx => OnButtonClick(ctx, "finish"), "\u2714  Done with List", ButtonStyle.Success);
        AddButton(ctx => OnButtonClick(ctx, "delete"), "\u2716  Delete List Message", ButtonStyle.Danger);

        await Send();
    }

    private async Task OnButtonClick(InteractionContext ctx, string buttonAction)
    {
        if (ctx.User.Id != User)
        {
            // todo make silent instead of error?
            await Error(ctx, Errors.InteractionWrongAccount(User ?? 0));
            return;
        }

        switch (buttonAction)
        {
            case "first":
                Page = 0;
                await Update(ctx);
                break;
            case "previous":
                Page = (Page - 1) % PageCount;
                if (Page < 0) Page += PageCount;
                await Update(ctx);
                break;
            case "next":
                Page = (Page + 1) % PageCount;
                await Update(ctx);
                break;
            case "last":
                Page = PageCount - 1;
                await Update(ctx);
                break;
            case "finish":
                await Finish(ctx);
                break;
            case "delete":
                await ctx.Rest.DeleteMessage(_message.ChannelId, _message.Id);
                _tcs.TrySetResult();
                break;
            case "input_page":
                await InputPage(ctx);
                break;
            case "options":
                await CreateOptionsModal(ctx);
                break;
        }
    }

    // The generic method we're overriding has a parameter
    // But we'll never need it here
    public async override Task<MessageComponent[]> GetComponents(string? _)
    {
        List<MessageComponent> components = [];

        var (header, footer, generatedComponents) = ContentBuilder(Page);

        components.Add(header);
        components.AddRange(generatedComponents);

        foreach (var line in _buttons.Chunk(5))
        {
            components.Add(new()
            {
                Type = ComponentType.ActionRow,
                Components = [.. line.Select(b => b.ToMessageComponent())]
            });
        }

        components.Add(footer);

        return
        [
            new MessageComponent()
            {
                Type = ComponentType.Container,
                AccentColor = Color.ToDiscordColor() ?? null,
                Components = components.ToArray()
            },
        ];
    }

    private async Task CreateOptionsModal(InteractionContext ctx)
    {
        async Task OptionsModalHandler(InteractionContext ctx)
        {
            PageCount = await Renderer(ctx.Event.Data);
            if (Page > PageCount) Page = 0;
            await Update(ctx);
        }

        var dispatch = _ctx.Services.Resolve<InteractionDispatchService>();
        var customId = dispatch.Register(_ctx.ShardId, OptionsModalHandler, Timeout);

        await ctx.Respond(InteractionResponse.ResponseType.Modal,
        new InteractionApplicationCommandCallbackData { Title = "List Options", CustomId = customId, Components = OptionsModal() });
    }

    private async Task InputPage(InteractionContext ctx)
    {
        async Task PageModalHandler(InteractionContext ctx)
        {
            var input = ctx.Event.Data.Components[0].Component.Value;

            if (int.TryParse(input, out var page))
            {
                if (page < 1)
                    throw new PKError("Page must be at least 1");
                else if (page > PageCount)
                    throw new PKError("Page can't be greater than total page count");

                Page = page - 1;
                await Update(ctx);
            }
            else
            {
                throw new PKError("Invalid page number");
            }
        }

        var dispatch = _ctx.Services.Resolve<InteractionDispatchService>();
        var customId = dispatch.Register(_ctx.ShardId, PageModalHandler, Timeout);

        MessageComponent[] components = [new MessageComponent()
        {
            Type = ComponentType.Label,
            Label = "Page Number",
            Component = new MessageComponent()
            {
                Type = ComponentType.TextInput,
                Placeholder = "Page Number",
                Style = ButtonStyle.Primary,
                MinLength = 1,
                CustomId = "page"
            }
        }];
        await ctx.Respond(InteractionResponse.ResponseType.Modal,
        new InteractionApplicationCommandCallbackData { Title = "Enter Page Number", CustomId = customId, Components = components });
    }

}