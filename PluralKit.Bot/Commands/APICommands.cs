using System.Threading.Tasks;
using Discord;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class APICommands
    {
        private SystemStore _systems;
        public APICommands(SystemStore systems)
        {
            _systems = systems;
        }

        public async Task GetToken(Context ctx)
        {
            ctx.CheckSystem();
            
            // Get or make a token
            var token = ctx.System.Token ?? await MakeAndSetNewToken(ctx.System);
            
            // If we're not already in a DM, reply with a reminder to check
            if (!(ctx.Channel is IDMChannel))
            {
                await ctx.Reply($"{Emojis.Success} Check your DMs!");
            }

            // DM the user a security disclaimer, and then the token in a separate message (for easy copying on mobile)
            await ctx.Author.SendMessageAsync($"{Emojis.Warn} Please note that this grants access to modify (and delete!) all your system data, so keep it safe and secure. If it leaks or you need a new one, you can invalidate this one with `pk;token refresh`.\n\nYour token is below:");
            await ctx.Author.SendMessageAsync(token);
        }

        private async Task<string> MakeAndSetNewToken(PKSystem system)
        {
            system.Token = PluralKit.Utils.GenerateToken();
            await _systems.Save(system);
            return system.Token;
        }
        
        public async Task RefreshToken(Context ctx)
        {
            ctx.CheckSystem();
            
            if (ctx.System.Token == null)
            {
                // If we don't have a token, call the other method instead
                // This does pretty much the same thing, except words the messages more appropriately for that :)
                await GetToken(ctx);
                return;
            }
            
            // Make a new token from scratch
            var token = await MakeAndSetNewToken(ctx.System);
            
            // If we're not already in a DM, reply with a reminder to check
            if (!(ctx.Channel is IDMChannel))
            {
                await ctx.Reply($"{Emojis.Success} Check your DMs!");
            }
            
            // DM the user an invalidation disclaimer, and then the token in a separate message (for easy copying on mobile)
            await ctx.Author.SendMessageAsync($"{Emojis.Warn} Your previous API token has been invalidated. You will need to change it anywhere it's currently used.\n\nYour token is below:");
            await ctx.Author.SendMessageAsync(token);
        }
    }
}