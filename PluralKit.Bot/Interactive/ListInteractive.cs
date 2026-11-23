using Autofac;

using Myriad.Types;

namespace PluralKit.Bot.Interactive;

public class ListInteractive(Context ctx): BaseInteractive(ctx)
{
    public ulong? User { get; set; } = ctx.Author.Id;
    public ButtonStyle Style { get; set; } = ButtonStyle.Secondary;
    public int Page { get; set; } = 0;
    public int PageCount { get; set; } = 1;
    public Func<int, (MessageComponent, MessageComponent, List<MessageComponent>)> ContentBuilder { get; init; }
    public Func<int, int, ApplicationCommandInteractionData?, int> Renderer { get; init; }
    public string? Color { get; set; }
    public (string Label, Func<MessageComponent[]> Modal)[]? OptionsModal { get; init; }

    private int _additionalCharacters;
    // TODO: Figure out how to assign this dynamically
    // This is the number of components the list itself uses not including the pages
    private readonly int _additionalComponents = 11;

    public override async Task Start()
    {
        // We want to make sure that the pages we make account for the button characters for character count
        // But we don't want to add page flip buttons if we only have one page
        // So a pending list
        List<(string value, string label)> pendingButtons = [];
        pendingButtons.Add(("first", "\u23EA")); // <<
        pendingButtons.Add(("previous", "\u2B05")); // <
        pendingButtons.Add(("next", "\u27A1")); // >
        pendingButtons.Add(("last", "\u23E9")); // >>
        pendingButtons.Add(("input_page", "\uD83D\uDD22")); // 1234

        if (OptionsModal != null && OptionsModal.Length > 0)
            foreach (var (Label, Modal) in OptionsModal)
                AddButton(ctx => OnButtonClick(ctx, $"options-{Label}"), Label, ButtonStyle.Secondary);
        // AddButton(ctx => OnButtonClick(ctx, "finish"), "\u2714  Done with List", ButtonStyle.Success);
        // AddButton(ctx => OnButtonClick(ctx, "delete"), "\u2716  Delete List Message", ButtonStyle.Danger);

        _additionalCharacters = _buttons.Sum(b => b.Label.Length) + pendingButtons.Sum(b => b.label.Length);
        PageCount = Renderer(_additionalCharacters, _additionalComponents, null);

        if (PageCount > 1)
            foreach (var (value, label) in pendingButtons)
                AddButton(ctx => OnButtonClick(ctx, value), label, Style, innerButton: false);

        await Send();
    }

    private async Task OnButtonClick(InteractionContext ctx, string buttonAction)
    {
        if (ctx.User.Id != User)
        {
            // TODO: make silent instead of error?
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
            case { } s when s.StartsWith("options"):
                await CreateOptionsModal(ctx, OptionsModal.First(x => x.Label == string.Concat(buttonAction.Skip("options-".Length))).Modal);
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

        components = AddInnerButtons(components);

        if (footer != null) components.Add(footer);

        if (!(_buttons.Any(b => b.InnerButton) || footer != null))
            components = [.. components.SkipLast(1)];

        return AddOuterButtons(components, Color).ToArray();
    }

    private async Task CreateOptionsModal(InteractionContext ctx, Func<MessageComponent[]> modal)
    {
        async Task OptionsModalHandler(InteractionContext ctx)
        {
            PageCount = Renderer(_additionalCharacters, _additionalComponents, ctx.Event.Data);
            if (Page > PageCount) Page = 0;
            await Update(ctx);
        }

        var dispatch = _ctx.Services.Resolve<InteractionDispatchService>();
        var customId = dispatch.Register(_ctx.ShardId, OptionsModalHandler, Timeout);

        await ctx.Respond(InteractionResponse.ResponseType.Modal,
        new InteractionApplicationCommandCallbackData { Title = "List Options", CustomId = customId, Components = modal() });
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