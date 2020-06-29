using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;


using Dapper;

using DSharpPlus.Entities;

using NodaTime;

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
                var msg = await ctx.Reply($"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.NameFor(ctx)}\" (`{existingMember.Hid}`). Do you want to rename this member to that name too?");
                if (!await ctx.PromptYesNo(msg)) throw new PKError("Member renaming cancelled.");
            }

            // Rename the member
            var patch = new MemberPatch {Name = Partial<string>.Present(newName)};
            await _db.Execute(conn => conn.UpdateMember(target.Id, patch));

            await ctx.Reply($"{Emojis.Success} Member renamed.");
            if (newName.Contains(" ")) await ctx.Reply($"{Emojis.Note} Note that this member's name now contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it.");
            if (target.DisplayName != null) await ctx.Reply($"{Emojis.Note} Note that this member has a display name set ({target.DisplayName}), and will be proxied using that name instead.");

            if (ctx.Guild != null)
            {
                var memberGuildConfig = await _db.Execute(c => c.QueryOrInsertMemberGuildConfig(ctx.Guild.Id, target.Id));
                if (memberGuildConfig.DisplayName != null)
                    await ctx.Reply($"{Emojis.Note} Note that this member has a server name set ({memberGuildConfig.DisplayName}) in this server ({ctx.Guild.Name}), and will be proxied using that name here.");
            }
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

                var patch = new MemberPatch {Description = Partial<string>.Null()};
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));
                await ctx.Reply($"{Emojis.Success} Member description cleared.");
            } 
            else if (!ctx.HasNext())
            {
                if (!target.DescriptionPrivacy.CanAccess(ctx.LookupContextFor(target.System)))
                    throw Errors.LookupNotAllowed;
                if (target.Description == null)
                    if (ctx.System?.Id == target.System)
                        await ctx.Reply($"This member does not have a description set. To set one, type `pk;member {target.Hid} description <description>`.");
                    else
                        await ctx.Reply("This member does not have a description set.");
                else if (ctx.MatchFlag("r", "raw"))
                    await ctx.Reply($"```\n{target.Description}\n```");
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
        
                var patch = new MemberPatch {Description = Partial<string>.Present(description)};
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));
                
                await ctx.Reply($"{Emojis.Success} Member description changed.");
            }
        }
        
        public async Task Pronouns(Context ctx, PKMember target) {
            if (MatchClear(ctx))
            {
                CheckEditMemberPermission(ctx, target);
                var patch = new MemberPatch {Pronouns = Partial<string>.Null()};
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));
                await ctx.Reply($"{Emojis.Success} Member pronouns cleared.");
            } 
            else if (!ctx.HasNext())
            {
                if (!target.PronounPrivacy.CanAccess(ctx.LookupContextFor(target.System)))
                    throw Errors.LookupNotAllowed;
                if (target.Pronouns == null)
                    if (ctx.System?.Id == target.System)
                        await ctx.Reply($"This member does not have pronouns set. To set some, type `pk;member {target.Hid} pronouns <pronouns>`.");
                    else
                        await ctx.Reply("This member does not have pronouns set.");
                else
                    await ctx.Reply($"**{target.NameFor(ctx)}**'s pronouns are **{target.Pronouns}**."
                        + (ctx.System?.Id == target.System ? $" To clear them, type `pk;member {target.Hid} pronouns -clear`." : ""));
            }
            else
            {
                CheckEditMemberPermission(ctx, target);

                var pronouns = ctx.RemainderOrNull().NormalizeLineEndSpacing();
                if (pronouns.IsLongerThan(Limits.MaxPronounsLength))
                    throw Errors.MemberPronounsTooLongError(pronouns.Length);
                
                var patch = new MemberPatch {Pronouns = Partial<string>.Present(pronouns)};
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));
                
                await ctx.Reply($"{Emojis.Success} Member pronouns changed.");
            }
        }

        public async Task Color(Context ctx, PKMember target)
        {
            var color = ctx.RemainderOrNull();
            if (MatchClear(ctx))
            {
                CheckEditMemberPermission(ctx, target);
                
                var patch = new MemberPatch {Color = Partial<string>.Null()};
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));
                
                await ctx.Reply($"{Emojis.Success} Member color cleared.");
            }
            else if (!ctx.HasNext())
            {
                // if (!target.ColorPrivacy.CanAccess(ctx.LookupContextFor(target.System)))
                //     throw Errors.LookupNotAllowed;

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
                        .WithThumbnail($"https://fakeimg.pl/256x256/{target.Color}/?text=%20")
                        .WithDescription($"This member's color is **#{target.Color}**."
                                         + (ctx.System?.Id == target.System ? $" To clear it, type `pk;member {target.Hid} color -clear`." : ""))
                        .Build());
            }
            else
            {
                CheckEditMemberPermission(ctx, target);

                if (color.StartsWith("#")) color = color.Substring(1);
                if (!Regex.IsMatch(color, "^[0-9a-fA-F]{6}$")) throw Errors.InvalidColorError(color);
                
                var patch = new MemberPatch {Color = Partial<string>.Present(color.ToLowerInvariant())};
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));

                await ctx.Reply(embed: new DiscordEmbedBuilder()
                    .WithTitle($"{Emojis.Success} Member color changed.")
                    .WithColor(color.ToDiscordColor().Value)
                    .WithThumbnail($"https://fakeimg.pl/256x256/{target.Color}/?text=%20")
                    .Build());
            }
        }
        public async Task Birthday(Context ctx, PKMember target)
        {
            if (MatchClear(ctx))
            {
                CheckEditMemberPermission(ctx, target);
                
                var patch = new MemberPatch {Birthday = Partial<LocalDate?>.Null()};
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));

                await ctx.Reply($"{Emojis.Success} Member birthdate cleared.");
            } 
            else if (!ctx.HasNext())
            {
                if (!target.BirthdayPrivacy.CanAccess(ctx.LookupContextFor(target.System)))
                    throw Errors.LookupNotAllowed;
                
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
                
                var patch = new MemberPatch {Birthday = Partial<LocalDate?>.Present(birthday)};
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));

                await ctx.Reply($"{Emojis.Success} Member birthdate changed.");
            }
        }
        
        private async Task<DiscordEmbedBuilder> CreateMemberNameInfoEmbed(Context ctx, PKMember target)
        {
            var lcx = ctx.LookupContextFor(target);
            
            MemberGuildSettings memberGuildConfig = null;
            if (ctx.Guild != null)
                memberGuildConfig = await _db.Execute(c => c.QueryOrInsertMemberGuildConfig(ctx.Guild.Id, target.Id));

            var eb = new DiscordEmbedBuilder().WithTitle($"Member names")
                .WithFooter($"Member ID: {target.Hid} | Active name in bold. Server name overrides display name, which overrides base name.");

            if (target.DisplayName == null && memberGuildConfig?.DisplayName == null)
                eb.AddField("Name", $"**{target.NameFor(ctx)}**");
            else
                eb.AddField("Name", target.NameFor(ctx));

            if (target.NamePrivacy.CanAccess(lcx))
            {
                if (target.DisplayName != null && memberGuildConfig?.DisplayName == null)
                    eb.AddField("Display Name", $"**{target.DisplayName}**");
                else
                    eb.AddField("Display Name", target.DisplayName ?? "*(none)*");
            }

            if (ctx.Guild != null)
            {
                if (memberGuildConfig?.DisplayName != null)
                    eb.AddField($"Server Name (in {ctx.Guild.Name})", $"**{memberGuildConfig.DisplayName}**");
                else
                    eb.AddField($"Server Name (in {ctx.Guild.Name})", memberGuildConfig?.DisplayName ?? "*(none)*");
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
                        successStr += $" However, this member has a server name set in this server ({ctx.Guild.Name}), and will be proxied using that name, \"{memberGuildConfig.DisplayName}\", here.";
                }

                await ctx.Reply(successStr);
            }
            
            if (MatchClear(ctx))
            {
                CheckEditMemberPermission(ctx, target);
                
                var patch = new MemberPatch {DisplayName = Partial<string>.Null()};
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));

                await PrintSuccess($"{Emojis.Success} Member display name cleared. This member will now be proxied using their member name \"{target.NameFor(ctx)}\".");
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
                
                var patch = new MemberPatch {DisplayName = Partial<string>.Present(newDisplayName)};
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));

                await PrintSuccess($"{Emojis.Success} Member display name changed. This member will now be proxied using the name \"{newDisplayName}\".");
            }
        }
        
        public async Task ServerName(Context ctx, PKMember target)
        {
            ctx.CheckGuildContext();
            
            if (MatchClear(ctx))
            {
                CheckEditMemberPermission(ctx, target);

                var patch = new MemberGuildPatch {DisplayName = null};
                await _db.Execute(conn => conn.UpsertMemberGuild(target.Id, ctx.Guild.Id, patch));

                if (target.DisplayName != null)
                    await ctx.Reply($"{Emojis.Success} Member server name cleared. This member will now be proxied using their global display name \"{target.DisplayName}\" in this server ({ctx.Guild.Name}).");
                else
                    await ctx.Reply($"{Emojis.Success} Member server name cleared. This member will now be proxied using their member name \"{target.NameFor(ctx)}\" in this server ({ctx.Guild.Name}).");
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
                
                var patch = new MemberGuildPatch {DisplayName = newServerName};
                await _db.Execute(conn => conn.UpsertMemberGuild(target.Id, ctx.Guild.Id, patch));

                await ctx.Reply($"{Emojis.Success} Member server name changed. This member will now be proxied using the name \"{newServerName}\" in this server ({ctx.Guild.Name}).");
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

            var patch = new MemberPatch {KeepProxy = Partial<bool>.Present(newValue)};
            await _db.Execute(conn => conn.UpdateMember(target.Id, patch));
            
            if (newValue)
                await ctx.Reply($"{Emojis.Success} Member proxy tags will now be included in the resulting message when proxying.");
            else
                await ctx.Reply($"{Emojis.Success} Member proxy tags will now not be included in the resulting message when proxying.");
        }

        private DiscordEmbed CreatePrivacyEmbed(Context ctx, PKMember member)
        {
            string PrivacyLevelString(PrivacyLevel level) => level switch
            {
                PrivacyLevel.Private => "**Private** (visible only when queried by you)",
                PrivacyLevel.Public => "**Public** (visible to everyone)",
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
            };

            var eb = new DiscordEmbedBuilder()
                .WithTitle($"Current privacy settings for {member.NameFor(ctx)}")
                .AddField("Name (replaces name with display name if member has one)",PrivacyLevelString(member.NamePrivacy))
                .AddField("Description", PrivacyLevelString(member.DescriptionPrivacy))
                .AddField("Avatar", PrivacyLevelString(member.AvatarPrivacy))
                .AddField("Birthday", PrivacyLevelString(member.BirthdayPrivacy))
                .AddField("Pronouns", PrivacyLevelString(member.PronounPrivacy))
                // .AddField("Color", PrivacyLevelString(target.ColorPrivacy))
                .AddField("Meta (message count, last front, last message)", PrivacyLevelString(member.MetadataPrivacy))
                .AddField("Visibility", PrivacyLevelString(member.MemberVisibility))
                .WithDescription("To edit privacy settings, use the command:\n`pk;member <member> privacy <subject> <level>`\n\n- `subject` is one of `name`, `description`, `avatar`, `birthday`, `pronouns`, `created`, `messages`, `visibility`, or `all`\n- `level` is either `public` or `private`."); 
            return eb.Build();
        }

        public async Task Privacy(Context ctx, PKMember target, PrivacyLevel? newValueFromCommand)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            // Display privacy settings
            if (!ctx.HasNext() && newValueFromCommand == null)
            {
                await ctx.Reply(embed: CreatePrivacyEmbed(ctx, target));
                return;
            }
            
            // Get guild settings (mostly for warnings and such)
            MemberGuildSettings guildSettings = null;
            if (ctx.Guild != null)
                guildSettings = await _db.Execute(c => c.QueryOrInsertMemberGuildConfig(ctx.Guild.Id, target.Id));

            // Set Privacy Settings
            PrivacyLevel PopPrivacyLevel(string subjectName)
            {
                if (ctx.Match("public", "show", "shown", "visible"))
                    return PrivacyLevel.Public;

                if (ctx.Match("private", "hide", "hidden"))
                    return PrivacyLevel.Private;

                if (!ctx.HasNext())
                    throw new PKSyntaxError($"You must pass a privacy level for `{subjectName}` (`public` or `private`)");
                throw new PKSyntaxError($"Invalid privacy level `{ctx.PopArgument()}` (must be `public` or `private`).");
            }
            
            // See if we have a subject given
            PrivacyLevel newLevel;
            if (PrivacyUtils.TryParseMemberPrivacy(ctx.PeekArgument(), out var subject))
            {
                // We peeked before, pop it now
                ctx.PopArgument();
                
                // Read the privacy level from args
                newLevel = PopPrivacyLevel(subject.Name());
                
                // Set the level on the given subject
                var patch = new MemberPatch();
                patch.SetPrivacy(subject, newLevel);
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));

                // Print response
                var explanation = (subject, newLevel) switch
                {
                    (MemberPrivacySubject.Name, PrivacyLevel.Private) => "This member's name is now hidden from other systems, and will be replaced by the member's display name.",
                    (MemberPrivacySubject.Description, PrivacyLevel.Private) => "This member's description is now hidden from other systems.",
                    (MemberPrivacySubject.Avatar, PrivacyLevel.Private) => "This member's avatar is now hidden from other systems.",
                    (MemberPrivacySubject.Birthday, PrivacyLevel.Private) => "This member's birthday is now hidden from other systems.",
                    (MemberPrivacySubject.Pronouns, PrivacyLevel.Private) => "This member's pronouns are now hidden from other systems.",
                    (MemberPrivacySubject.Metadata, PrivacyLevel.Private) => "This member's metadata (eg. created timestamp, message count, etc) is now hidden from other systems.",
                    (MemberPrivacySubject.Visibility, PrivacyLevel.Private) => "This member is now hidden from member lists.",
                    
                    (MemberPrivacySubject.Name, PrivacyLevel.Public) => "This member's name is no longer hidden from other systems.",
                    (MemberPrivacySubject.Description, PrivacyLevel.Public) => "This member's description is no longer hidden from other systems.",
                    (MemberPrivacySubject.Avatar, PrivacyLevel.Public) => "This member's avatar is no longer hidden from other systems.",
                    (MemberPrivacySubject.Birthday, PrivacyLevel.Public) => "This member's birthday is no longer hidden from other systems.",
                    (MemberPrivacySubject.Pronouns, PrivacyLevel.Public) => "This member's pronouns are no longer hidden other systems.",
                    (MemberPrivacySubject.Metadata, PrivacyLevel.Public) => "This member's metadata (eg. created timestamp, message count, etc) is no longer hidden from other systems.",
                    (MemberPrivacySubject.Visibility, PrivacyLevel.Public) => "This member is no longer hidden from member lists.",
                    
                    _ => throw new InvalidOperationException($"Invalid subject/level tuple ({subject}, {newLevel})")
                };
                
                await ctx.Reply($"{Emojis.Success} {target.NameFor(ctx)}'s {subject.Name()} has been set to **{newLevel.LevelName()}**. {explanation}");
            }
            else if (ctx.Match("all") || newValueFromCommand != null)
            {
                newLevel = newValueFromCommand ?? PopPrivacyLevel("all");
                
                var patch = new MemberPatch();
                patch.SetAllPrivacy(newLevel);
                await _db.Execute(conn => conn.UpdateMember(target.Id, patch));
                
                if(newLevel == PrivacyLevel.Private)
                    await ctx.Reply($"All {target.NameFor(ctx)}'s privacy settings have been set to **{newLevel.LevelName()}**. Other accounts will now see nothing on the member card.");
                else 
                    await ctx.Reply($"All {target.NameFor(ctx)}'s privacy settings have been set to **{newLevel.LevelName()}**. Other accounts will now see everything on the member card.");
            }
            else
            {
                var subjectList = "`name`, `description`, `avatar`, `birthday`, `pronouns`, `metadata`, `visibility`, or `all`";
                throw new PKSyntaxError($"Invalid privacy subject `{ctx.PopArgument()}` (must be {subjectList}).");
            }
            
            // Name privacy only works given a display name
            if (subject == MemberPrivacySubject.Name && newLevel == PrivacyLevel.Private && target.DisplayName == null)
                await ctx.Reply($"{Emojis.Warn} This member does not have a display name set, and name privacy **will not take effect**.");
            // Avatar privacy doesn't apply when proxying if no server avatar is set
            if (subject == MemberPrivacySubject.Avatar && newLevel == PrivacyLevel.Private &&
                guildSettings?.AvatarUrl == null)
                await ctx.Reply($"{Emojis.Warn} This member does not have a server avatar set, so *proxying* will **still show the member avatar**. If you want to hide your avatar when proxying here, set a server avatar: `pk;member {target.Hid} serveravatar`");
        }
        
        public async Task Delete(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            
            await ctx.Reply($"{Emojis.Warn} Are you sure you want to delete \"{target.NameFor(ctx)}\"? If so, reply to this message with the member's ID (`{target.Hid}`). __***This cannot be undone!***__");
            if (!await ctx.ConfirmWithReply(target.Hid)) throw Errors.MemberDeleteCancelled;
            
            await _db.Execute(conn => conn.DeleteMember(target.Id));
            
            await ctx.Reply($"{Emojis.Success} Member deleted.");
        }
    }
}