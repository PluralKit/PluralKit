using System.Threading.Tasks;

using Myriad.Types;

namespace PluralKit.Bot.Interactive
{
    public class YesNoPrompt: BaseInteractive
    {
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
            await Send(Message);
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

        public YesNoPrompt(Context ctx): base(ctx)
        {
            User = ctx.Author.Id;
        }
    }
}