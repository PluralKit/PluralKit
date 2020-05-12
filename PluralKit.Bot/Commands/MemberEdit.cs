using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Dapper;

using DSharpPlus.Entities;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class MemberEdit
    {
        private readonly IDataStore _data;
        private readonly IDatabase _db;

        public MemberEdit(IDataStore data, IDatabase db)
        {
            _data = data;
            _db = db;
        }

        public async Task Name(Context ctx, PKMember target) {
            // TODO: this method is pretty much a 1:1 copy/paste of the above creation method, find a way to clean?
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            
            var newName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a new name for the member.");

            // Hard name length cap
            if (newName.Length > Limits.MaxMemberNameLength) throw Errors.MemberNameTooLongError(newName.Length);

            // Warn if there's already a member by this name
            var existingMember = await _data.GetMemberByName(ctx.System, newName);
            if (existingMember != null) {
                var msg = await ctx.Reply($"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.Name.SanitizeMentions()}\" (`{existingMember.Hid}`). Do you want to rename this member to that name too?");
                if (!await ctx.PromptYesNo(msg)) throw new PKError("Member renaming cancelled.");
            }

            // Rename the member
            target.Name = newName;
            await _data.SaveMember(target);

            await ctx.Reply($"{Emojis.Success} Member renamed.");
            if (newName.Contains(" ")) await ctx.Reply($"{Emojis.Note} Note that this member's name now contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it.");
            if (target.DisplayName != null) await ctx.Reply($"{Emojis.Note} Note that this member has a display name set ({target.DisplayName.SanitizeMentions()}), and will be proxied using that name instead.");

            if (ctx.Guild != null)
            {
                var memberGuildConfig = await _db.Execute(c => c.QueryOrInsertMemberGuildConfig(ctx.Guild.Id, target.Id));
                if (memberGuildConfig.DisplayName != null)
                    await ctx.Reply($"{Emojis.Note} Note that this member has a server name set ({memberGuildConfig.DisplayName.SanitizeMentions()}) in this server ({ctx.Guild.Name.SanitizeMentions()}), and will be proxied using that name here.");
            }
        }

        private void CheckReadMemberPermission(Context ctx, PKMember target)
        {
            if (!target.MemberVisibility.CanAccess(ctx.LookupContextFor(target.System)))
                throw Errors.LookupNotAllowed;
        }

        private void CheckEditMemberPermission(Context ctx, PKMember target)
        {
            if (target.System != ctx.System?.Id) throw Errors.NotOwnMemberError;
        }
        
        private static bool MatchClear(Context ctx) =>
            ctx.Match("clear") || ctx.MatchFlag("c", "clear");

        public async Task Description(Context ctx, PKMember target) {
            if (MatchClear(ctx))
            {
                CheckEditMemberPermission(ctx, target);
                target.Description = null;
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Member description cleared.");
            } 
            else if (!ctx.HasNext())
            {
                CheckReadMemberPermission(ctx, target);
                if (target.Description == null)
                    if (ctx.System?.Id == target.System)
                        await ctx.Reply($"This member does not have a description set. To set one, type `pk;member {target.Hid} description <description>`.");
                    else
                        await ctx.Reply("This member does not have a description set.");
                else if (ctx.MatchFlag("r", "raw"))
                    await ctx.Reply($"```\n{target.Description.SanitizeMentions()}\n```");
                else
                    await ctx.Reply(embed: new DiscordEmbedBuilder()
                        .WithTitle("Member description")
                        .WithDescription(target.Description)
                        .AddField("\u200B", $"To print the description with formatting, type `pk;member {target.Hid} description -raw`." 
                                    + (ctx.System?.Id == target.System ? $" To clear it, type `pk;member {target.Hid} description -clear`." : ""))
                        .Build());
            }
            else
            {
                CheckEditMemberPermission(ctx, target);

                var description = ctx.RemainderOrNull().NormalizeLineEndSpacing();
                if (description.IsLongerThan(Limits.MaxDescriptionLength))
                    throw Errors.DescriptionTooLongError(description.Length);
                target.Description = description;
        
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Member description changed.");
            }
        }
        
        public async Task Pronouns(Context ctx, PKMember target) {
            if (MatchClear(ctx))
            {
                CheckEditMemberPermission(ctx, target);
                target.Pronouns = null;
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Member pronouns cleared.");
            } 
            else if (!ctx.HasNext())
            {
                CheckReadMemberPermission(ctx, target);
                if (target.Pronouns == null)
                    if (ctx.System?.Id == target.System)
                        await ctx.Reply($"This member does not have pronouns set. To set some, type `pk;member {target.Hid} pronouns <pronouns>`.");
                    else
                        await ctx.Reply("This member does not have pronouns set.");
                else
                    await ctx.Reply($"**{target.Name.SanitizeMentions()}**'s pronouns are **{target.Pronouns.SanitizeMentions()}**."
                        + (ctx.System?.Id == target.System ? $" To clear them, type `pk;member {target.Hid} pronouns -clear`." : ""));
            }
            else
            {
                CheckEditMemberPermission(ctx, target);

                var pronouns = ctx.RemainderOrNull().NormalizeLineEndSpacing();
                if (pronouns.IsLongerThan(Limits.MaxPronounsLength))
                    throw Errors.MemberPronounsTooLongError(pronouns.Length);
                target.Pronouns = pronouns;
        
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Member pronouns changed.");
            }
        }

        public async Task Color(Context ctx, PKMember target)
        {
            var color = ctx.RemainderOrNull();
            if (MatchClear(ctx))
            {
                CheckEditMemberPermission(ctx, target);
                target.Color = null;
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Member color cleared.");
            }
            else if (!ctx.HasNext())
            {
                CheckReadMemberPermission(ctx, target);

                if (target.Color == null)
                    if (ctx.System?.Id == target.System)
                        await ctx.Reply(
                            $"This member does not have a color set. To set one, type `pk;member {target.Hid} color <color>`.");
                    else
                        await ctx.Reply("This member does not have a color set.");
                else
                    await ctx.Reply(embed: new DiscordEmbedBuilder()
                        .WithTitle("Member color")
                        .WithColor(target.Color.ToDiscordColor().Value)
                        .WithThumbnailUrl($"https://fakeimg.pl/256x256/{target.Color}/?text=%20")
                        .WithDescription($"This member's color is **#{target.Color}**."
                                         + (ctx.System?.Id == target.System ? $" To clear it, type `pk;member {target.Hid} color -clear`." : ""))
                        .Build());
            }
            else
            {
                CheckEditMemberPermission(ctx, target);

                if (color.StartsWith("#")) color = color.Substring(1);
                if (!Regex.IsMatch(color, "^[0-9a-fA-F]{6}$")) throw Errors.InvalidColorError(color);
                target.Color = color.ToLower();
                await _data.SaveMember(target);

                await ctx.Reply(embed: new DiscordEmbedBuilder()
                    .WithTitle($"{Emojis.Success} Member color changed.")
                    .WithColor(target.Color.ToDiscordColor().Value)
                    .WithThumbnailUrl($"https://fakeimg.pl/256x256/{target.Color}/?text=%20")
                    .Build());
            }
        }
        public async Task Birthday(Context ctx, PKMember target)
        {
            if (MatchClear(ctx))
            {
                CheckEditMemberPermission(ctx, target);
                target.Birthday = null;
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Member birthdate cleared.");
            } 
            else if (!ctx.HasNext())
            {
                CheckReadMemberPermission(ctx, target);
                
                if (target.Birthday == null)
                    await ctx.Reply("This member does not have a birthdate set."
                        + (ctx.System?.Id == target.System ? $" To set one, type `pk;member {target.Hid} birthdate <birthdate>`." : ""));
                else
                    await ctx.Reply($"This member's birthdate is **{target.BirthdayString}**."
                                    + (ctx.System?.Id == target.System ? $" To clear it, type `pk;member {target.Hid} birthdate -clear`." : ""));
            }
            else
            {
                CheckEditMemberPermission(ctx, target);
                
                var birthdayStr = ctx.RemainderOrNull();
                var birthday = DateUtils.ParseDate(birthdayStr, true);
                if (birthday == null) throw Errors.BirthdayParseError(birthdayStr);
                target.Birthday = birthday;
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Member birthdate changed.");
            }
        }
        
        private async Task<DiscordEmbedBuilder> CreateMemberNameInfoEmbed(Context ctx, PKMember target)
        {
            MemberGuildSettings memberGuildConfig = null;
            if (ctx.Guild != null)
                memberGuildConfig = await _db.Execute(c => c.QueryOrInsertMemberGuildConfig(ctx.Guild.Id, target.Id));

            var eb = new DiscordEmbedBuilder().WithTitle($"Member names")
                .WithFooter($"Member ID: {target.Hid} | Active name in bold. Server name overrides display name, which overrides base name.");

            if (target.DisplayName == null && memberGuildConfig?.DisplayName == null)
                eb.AddField($"Name", $"**{target.Name}**");
            else
                eb.AddField("Name", target.Name);
            
            if (target.DisplayName != null && memberGuildConfig?.DisplayName == null)
                eb.AddField($"Display Name", $"**{target.DisplayName}**");
            else
                eb.AddField("Display Name", target.DisplayName ?? "*(none)*");

            if (ctx.Guild != null)
            {
                if (memberGuildConfig?.DisplayName != null)
                    eb.AddField($"Server Name (in {ctx.Guild.Name.SanitizeMentions()})", $"**{memberGuildConfig.DisplayName}**");
                else
                    eb.AddField($"Server Name (in {ctx.Guild.Name.SanitizeMentions()})", memberGuildConfig?.DisplayName ?? "*(none)*");
            }

            return eb;
        }

        public async Task DisplayName(Context ctx, PKMember target)
        {
            async Task PrintSuccess(string text)
            {
                var successStr = text;
                if (ctx.Guild != null)
                {
                    var memberGuildConfig = await _db.Execute(c => c.QueryOrInsertMemberGuildConfig(ctx.Guild.Id, target.Id));
                    if (memberGuildConfig.DisplayName != null)
                        successStr += $" However, this member has a server name set in this server ({ctx.Guild.Name.SanitizeMentions()}), and will be proxied using that name, \"{memberGuildConfig.DisplayName.SanitizeMentions()}\", here.";
                }

                await ctx.Reply(successStr);
            }
            
            if (MatchClear(ctx))
            {
                CheckEditMemberPermission(ctx, target);
                
                target.DisplayName = null;
                await _data.SaveMember(target);
                await PrintSuccess($"{Emojis.Success} Member display name cleared. This member will now be proxied using their member name \"{target.Name.SanitizeMentions()}\".");
            }
            else if (!ctx.HasNext())
            {
                // No perms check, display name isn't covered by member privacy 
                var eb = await CreateMemberNameInfoEmbed(ctx, target);
                if (ctx.System?.Id == target.System)
                    eb.WithDescription($"To change display name, type `pk;member {target.Hid} displayname <display name>`.\nTo clear it, type `pk;member {target.Hid} displayname -clear`.");
                await ctx.Reply(embed: eb.Build());
            }
            else
            {
                CheckEditMemberPermission(ctx, target);
                
                var newDisplayName = ctx.RemainderOrNull();
                target.DisplayName = newDisplayName;
                await _data.SaveMember(target);

                await PrintSuccess($"{Emojis.Success} Member display name changed. This member will now be proxied using the name \"{newDisplayName.SanitizeMentions()}\".");
            }
        }
        
        public async Task ServerName(Context ctx, PKMember target)
        {
            ctx.CheckGuildContext();
            
            if (MatchClear(ctx))
            {
                CheckEditMemberPermission(ctx, target);

                await _db.Execute(c =>
                    c.ExecuteAsync("update member_guild set display_name = null where member = @member and guild = @guild",
                        new {member = target.Id, guild = ctx.Guild.Id}));

                if (target.DisplayName != null)
                    await ctx.Reply($"{Emojis.Success} Member server name cleared. This member will now be proxied using their global display name \"{target.DisplayName.SanitizeMentions()}\" in this server ({ctx.Guild.Name.SanitizeMentions()}).");
                else
                    await ctx.Reply($"{Emojis.Success} Member server name cleared. This member will now be proxied using their member name \"{target.Name.SanitizeMentions()}\" in this server ({ctx.Guild.Name.SanitizeMentions()}).");
            }
            else if (!ctx.HasNext())
            {
                // No perms check, server name isn't covered by member privacy 
                var eb = await CreateMemberNameInfoEmbed(ctx, target);
                if (ctx.System?.Id == target.System)
                    eb.WithDescription($"To change server name, type `pk;member {target.Hid} servername <server name>`.\nTo clear it, type `pk;member {target.Hid} servername -clear`.");
                await ctx.Reply(embed: eb.Build());
            }
            else
            {
                CheckEditMemberPermission(ctx, target);
                
                var newServerName = ctx.RemainderOrNull();
                 
                await _db.Execute(c =>
                    c.ExecuteAsync("update member_guild set display_name = @newServerName where member = @member and guild = @guild",
                        new {member = target.Id, guild = ctx.Guild.Id, newServerName}));    

                await ctx.Reply($"{Emojis.Success} Member server name changed. This member will now be proxied using the name \"{newServerName.SanitizeMentions()}\" in this server ({ctx.Guild.Name.SanitizeMentions()}).");
            }
        }
        
        public async Task KeepProxy(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            bool newValue;
            if (ctx.Match("on", "enabled", "true", "yes")) newValue = true;
            else if (ctx.Match("off", "disabled", "false", "no")) newValue = false;
            else if (ctx.HasNext()) throw new PKSyntaxError("You must pass either \"on\" or \"off\".");
            else
            {
                if (target.KeepProxy)
                    await ctx.Reply("This member has keepproxy **enabled**, which means proxy tags will be **included** in the resulting message when proxying.");
                else
                    await ctx.Reply("This member has keepproxy **disabled**, which means proxy tags will **not** be included in the resulting message when proxying.");
                return;
            };

            target.KeepProxy = newValue;
            await _data.SaveMember(target);
            
            if (newValue)
                await ctx.Reply($"{Emojis.Success} Member proxy tags will now be included in the resulting message when proxying.");
            else
                await ctx.Reply($"{Emojis.Success} Member proxy tags will now not be included in the resulting message when proxying.");
        }

        public async Task Privacy(Context ctx, PKMember target, PrivacyLevel? newValueFromCommand)
        {
            // if (ctx.System == null) throw Errors.NoSystemError;
            // if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            // PrivacyLevel newValue;
            // if (ctx.Match("private", "hide", "hidden", "on", "enable", "yes")) newValue = PrivacyLevel.Private;
            // else if (ctx.Match("public", "show", "shown", "displayed", "off", "disable", "no")) newValue = PrivacyLevel.Public;
            // else if (ctx.HasNext()) throw new PKSyntaxError("You must pass either \"private\" or \"public\".");
            // // If we're getting a value from command (eg. "pk;m <name> private" == always private, "pk;m <name> public == always public"), use that instead of parsing
            // else if (newValueFromCommand != null) newValue = newValueFromCommand.Value;
            // else
            // {
            //     if (target.MemberPrivacy == PrivacyLevel.Public)
            //         await ctx.Reply("This member's privacy is currently set to **public**. This member will show up in member lists and will return all information when queried by other accounts.");
            //     else
            //         await ctx.Reply("This member's privacy is currently set to **private**. This member will not show up in member lists and will return limited information when queried by other accounts.");

            //     return;
            // }

            // target.MemberPrivacy = newValue;
            // await _data.SaveMember(target);

            // if (newValue == PrivacyLevel.Private)
            //     await ctx.Reply($"{Emojis.Success} Member privacy set to **private**. This member will no longer show up in member lists and will return limited information when queried by other accounts.");
            // else
            //     await ctx.Reply($"{Emojis.Success} Member privacy set to **public**. This member will now show up in member lists and will return all information when queried by other accounts.");
            if (!ctx.HasNext())
            {
                string PrivacyLevelString(PrivacyLevel level) => level switch
                {
                    PrivacyLevel.Private => "**Private** (visible only when queried by you)",
                    PrivacyLevel.Public => "**Public** (visible to everyone)",
                    _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
                };

                var eb = new DiscordEmbedBuilder()
                    .WithTitle($"Current privacy settings for {target.Name}")
                    .AddField("Name",PrivacyLevelString(target.NamePrivacy))
                    .AddField("Description", PrivacyLevelString(target.DescriptionPrivacy))
                    .AddField("Birthday", PrivacyLevelString(target.BirthdayPrivacy))
                    .AddField("Pronouns", PrivacyLevelString(target.PronounPrivacy))
                    .AddField("Color", PrivacyLevelString(target.ColorPrivacy))
                    .AddField("MessageCount", PrivacyLevelString(target.CreatedTimestampPrivacy))
                    .AddField("Visibility", PrivacyLevelString(target.MemberVisibility))
                    .WithDescription("To edit privacy settings, use the command:\n`pk;member <member> privacy <subject> <level>`\n\n- `subject` is one of `description`, `list`, `front` or `fronthistory`\n- `level` is either `public` or `private`.");
                await ctx.Reply(embed: eb.Build());
                return;
            }
        }
        
        public async Task Delete(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            
            await ctx.Reply($"{Emojis.Warn} Are you sure you want to delete \"{target.Name.SanitizeMentions()}\"? If so, reply to this message with the member's ID (`{target.Hid}`). __***This cannot be undone!***__");
            if (!await ctx.ConfirmWithReply(target.Hid)) throw Errors.MemberDeleteCancelled;
            await _data.DeleteMember(target);
            await ctx.Reply($"{Emojis.Success} Member deleted.");
        }
    }
}