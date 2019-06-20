using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace PluralKit.Bot.Commands
{
    [Group("token")]
    public class APICommands: ModuleBase<PKCommandContext>
    {
        public SystemStore Systems { get; set; }
        
        [Command]
        [MustHaveSystem]
        [Remarks("token")]
        public async Task GetToken()
        {
            // Get or make a token
            var token = Context.SenderSystem.Token ?? await MakeAndSetNewToken();
            
            // If we're not already in a DM, reply with a reminder to check
            if (!(Context.Channel is IDMChannel))
            {
                await Context.Channel.SendMessageAsync($"{Emojis.Success} Check your DMs!");
            }

            // DM the user a security disclaimer, and then the token in a separate message (for easy copying on mobile)
            await Context.User.SendMessageAsync($"{Emojis.Warn} Please note that this grants access to modify (and delete!) all your system data, so keep it safe and secure. If it leaks or you need a new one, you can invalidate this one with `pk;token refresh`.\n\nYour token is below:");
            await Context.User.SendMessageAsync(token);
        }

        private async Task<string> MakeAndSetNewToken()
        {
            Context.SenderSystem.Token = PluralKit.Utils.GenerateToken();
            await Systems.Save(Context.SenderSystem);
            return Context.SenderSystem.Token;
        }

        [Command("refresh")]
        [MustHaveSystem]
        [Alias("expire", "invalidate", "update", "new")]
        [Remarks("token refresh")]
        public async Task RefreshToken()
        {
            if (Context.SenderSystem.Token == null)
            {
                // If we don't have a token, call the other method instead
                // This does pretty much the same thing, except words the messages more appropriately for that :)
                await GetToken();
                return;
            }
            
            // Make a new token from scratch
            var token = await MakeAndSetNewToken();
            
            // If we're not already in a DM, reply with a reminder to check
            if (!(Context.Channel is IDMChannel))
            {
                await Context.Channel.SendMessageAsync($"{Emojis.Success} Check your DMs!");
            }
            
            // DM the user an invalidation disclaimer, and then the token in a separate message (for easy copying on mobile)
            await Context.User.SendMessageAsync($"{Emojis.Warn} Your previous API token has been invalidated. You will need to change it anywhere it's currently used.\n\nYour token is below:");
            await Context.User.SendMessageAsync(token);
        }
    }
}