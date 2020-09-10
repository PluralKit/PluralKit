using System.Threading.Tasks;

using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Token
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        public Token(IDatabase db, ModelRepository repo)
        {
            _db = db;
            _repo = repo;
        }

        public async Task GetToken(Context ctx)
        {
            ctx.CheckSystem();

            // Get or make a token
            var token = ctx.System.Token ?? await MakeAndSetNewToken(ctx.System);
            
            try
            {
                // DM the user a security disclaimer, and then the token in a separate message (for easy copying on mobile)
                var dm = await ctx.Rest.CreateDmAsync(ctx.Author.Id);
                await dm.SendMessageFixedAsync(
                    $"{Emojis.Warn} Please note that this grants access to modify (and delete!) all your system data, so keep it safe and secure. If it leaks or you need a new one, you can invalidate this one with `pk;token refresh`.\n\nYour token is below:");
                await dm.SendMessageFixedAsync(token);

                // If we're not already in a DM, reply with a reminder to check
                if (!(ctx.Channel is DiscordDmChannel)) 
                    await ctx.Reply($"{Emojis.Success} Check your DMs!");
            }
            catch (UnauthorizedException)
            {
                // Can't check for permission errors beforehand, so have to handle here :/
                if (!(ctx.Channel is DiscordDmChannel))
                    await ctx.Reply($"{Emojis.Error} Could not send token in DMs. Are your DMs closed?");
            }
        }

        private async Task<string> MakeAndSetNewToken(PKSystem system)
        {
            var patch = new SystemPatch {Token = StringUtils.GenerateToken()};
            system = await _db.Execute(conn => _repo.UpdateSystem(conn, system.Id, patch));
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

            try {
                // DM the user an invalidation disclaimer, and then the token in a separate message (for easy copying on mobile)
                var dm = await ctx.Rest.CreateDmAsync(ctx.Author.Id);
                await dm.SendMessageFixedAsync($"{Emojis.Warn} Your previous API token has been invalidated. You will need to change it anywhere it's currently used.\n\nYour token is below:");
                
                // Make the new token after sending the first DM; this ensures if we can't DM, we also don't end up
                // breaking their existing token as a side effect :)
                var token = await MakeAndSetNewToken(ctx.System);
                await dm.SendMessageFixedAsync(token);
                
                // If we're not already in a DM, reply with a reminder to check
                if (!(ctx.Channel is DiscordDmChannel)) 
                    await ctx.Reply($"{Emojis.Success} Check your DMs!");
            }
            catch (UnauthorizedException)
            {
                // Can't check for permission errors beforehand, so have to handle here :/
                if (!(ctx.Channel is DiscordDmChannel))
                    await ctx.Reply($"{Emojis.Error} Could not send token in DMs. Are your DMs closed?");
            }
        }
    }
}