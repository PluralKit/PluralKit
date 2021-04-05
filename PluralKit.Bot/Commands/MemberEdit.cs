using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;

using Myriad.Builders;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class MemberEdit
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;

        public MemberEdit(IDatabase db, ModelRepository repo)
        {
            _db = db;
            _repo = repo;
        }

        public async Task Name(Context ctx, PKMember target) 
        {
            ctx.CheckSystem().CheckOwnMember(target);            
            
            var newName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a new name for the member.");

            // Hard name length cap
            if (newName.Length > Limits.MaxMemberNameLength) throw Errors.MemberNameTooLongError(newName.Length);

            // Warn if there's already a member by this name
            var existingMember = await _db.Execute(conn => _repo.GetMemberByName(conn, ctx.System.Id, newName));
            if (existingMember != null && existingMember.Id != target.Id) 
            {
                var msg = $"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.NameFor(ctx)}\" (`{existingMember.Hid}`). Do you want to rename this member to that name too?";
                if (!await ctx.PromptYesNo(msg)) throw new PKError("Member renaming cancelled.");
            }

            // Rename the member
            var patch = new MemberPatch {Name = Partial<string>.Present(newName)};
            await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));

            await ctx.Reply($"{Emojis.Success} Member renamed.");
            if (newName.Contains(" ")) await ctx.Reply($"{Emojis.Note} Note that this member's name now contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it.");
            if (target.DisplayName != null) await ctx.Reply($"{Emojis.Note} Note that this member has a display name set ({target.DisplayName}), and will be proxied using that name instead.");

            if (ctx.Guild != null)
            {
                var memberGuildConfig = await _db.Execute(c => _repo.GetMemberGuild(c, ctx.Guild.Id, target.Id));
                if (memberGuildConfig.DisplayName != null)
                    await ctx.Reply($"{Emojis.Note} Note that this member has a server name set ({memberGuildConfig.DisplayName}) in this server ({ctx.Guild.Name}), and will be proxied using that name here.");
            }
        }

        public async Task Description(Context ctx, PKMember target) {
            if (await ctx.MatchClear("this member's description"))
            {
                ctx.CheckOwnMember(target);

                var patch = new MemberPatch {Description = Partial<string>.Null()};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));
                await ctx.Reply($"{Emojis.Success} Member description cleared.");
            } 
            else if (!ctx.HasNext())
            {
                if (!target.DescriptionPrivacy.CanAccess(ctx.LookupContextFor(target.System)))
                    throw Errors.LookupNotAllowed;
                if (target.Description == null)
                    if (ctx.System?.Id == target.System)
                        await ctx.Reply($"This member does not have a description set. To set one, type `pk;member {target.Reference()} description <description>`.");
                    else
                        await ctx.Reply("This member does not have a description set.");
                else if (ctx.MatchFlag("r", "raw"))
                    await ctx.Reply($"```\n{target.Description}\n```");
                else
                    await ctx.Reply(embed: new EmbedBuilder()
                        .Title("Member description")
                        .Description(target.Description)
                        .Field(new("\u200B", $"To print the description with formatting, type `pk;member {target.Reference()} description -raw`." 
                                    + (ctx.System?.Id == target.System ? $" To clear it, type `pk;member {target.Reference()} description -clear`." : "")))
                        .Build());
            }
            else
            {
                ctx.CheckOwnMember(target);

                var description = ctx.RemainderOrNull().NormalizeLineEndSpacing();
                if (description.IsLongerThan(Limits.MaxDescriptionLength))
                    throw Errors.DescriptionTooLongError(description.Length);
        
                var patch = new MemberPatch {Description = Partial<string>.Present(description)};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));
                
                await ctx.Reply($"{Emojis.Success} Member description changed.");
            }
        }
        
        public async Task Pronouns(Context ctx, PKMember target) {
            if (await ctx.MatchClear("this member's pronouns"))
            {
                ctx.CheckOwnMember(target);

                var patch = new MemberPatch {Pronouns = Partial<string>.Null()};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));
                await ctx.Reply($"{Emojis.Success} Member pronouns cleared.");
            } 
            else if (!ctx.HasNext())
            {
                if (!target.PronounPrivacy.CanAccess(ctx.LookupContextFor(target.System)))
                    throw Errors.LookupNotAllowed;
                if (target.Pronouns == null)
                    if (ctx.System?.Id == target.System)
                        await ctx.Reply($"This member does not have pronouns set. To set some, type `pk;member {target.Reference()} pronouns <pronouns>`.");
                    else
                        await ctx.Reply("This member does not have pronouns set.");
                else
                    await ctx.Reply($"**{target.NameFor(ctx)}**'s pronouns are **{target.Pronouns}**."
                        + (ctx.System?.Id == target.System ? $" To clear them, type `pk;member {target.Reference()} pronouns -clear`." : ""));
            }
            else
            {
                ctx.CheckOwnMember(target);

                var pronouns = ctx.RemainderOrNull().NormalizeLineEndSpacing();
                if (pronouns.IsLongerThan(Limits.MaxPronounsLength))
                    throw Errors.MemberPronounsTooLongError(pronouns.Length);
                
                var patch = new MemberPatch {Pronouns = Partial<string>.Present(pronouns)};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));
                
                await ctx.Reply($"{Emojis.Success} Member pronouns changed.");
            }
        }
        public async Task BannerImage(Context ctx, PKMember target)
        {
            ctx.CheckOwnMember(target);

            async Task ClearBannerImage()
            {
                await _db.Execute(c => _repo.UpdateMember(c, target.Id, new MemberPatch {BannerImage = null}));
                await ctx.Reply($"{Emojis.Success} Member banner image cleared.");
            }

            async Task SetBannerImage(ParsedImage img)
            {
                await AvatarUtils.VerifyAvatarOrThrow(img.Url, true);

                await _db.Execute(c => _repo.UpdateMember(c, target.Id, new MemberPatch {BannerImage = img.Url}));

                var msg = img.Source switch
                {
                    AvatarSource.Url => $"{Emojis.Success} Member banner image changed to the image at the given URL.",
                    AvatarSource.Attachment => $"{Emojis.Success} Member banner image changed to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the banner image will stop working.",
                    _ => throw new ArgumentOutOfRangeException()
                };

                // The attachment's already right there, no need to preview it.
                var hasEmbed = img.Source != AvatarSource.Attachment;
                await (hasEmbed 
                    ? ctx.Reply(msg, embed: new EmbedBuilder().Image(new(img.Url)).Build()) 
                    : ctx.Reply(msg));
            }

            async Task ShowBannerImage()
            {
                if ((target.BannerImage?.Trim() ?? "").Length > 0)
                {
                    var eb = new EmbedBuilder()
                        .Title($"{target.NameFor(ctx)}'s banner image")
                        .Image(new(target.BannerImage))
                        .Description($"To clear, use `pk;member {target.Hid} banner clear`.");
                    await ctx.Reply(embed: eb.Build());
                }
                else
                    throw new PKSyntaxError("This member does not have a banner image set. Set one by attaching an image to this command, or by passing an image URL or @mention.");
            }

            if (await ctx.MatchClear("this member's banner image"))
                await ClearBannerImage();
            else if (await ctx.MatchImage(true) is {} img)
                await SetBannerImage(img);
            else
                await ShowBannerImage();
        }
        

        public async Task Color(Context ctx, PKMember target)
        {
            var color = ctx.RemainderOrNull();
            if (await ctx.MatchClear())
            {
                ctx.CheckOwnMember(target);
                
                var patch = new MemberPatch {Color = Partial<string>.Null()};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));
                
                await ctx.Reply($"{Emojis.Success} Member color cleared.");
            }
            else if (!ctx.HasNext())
            {
                // if (!target.ColorPrivacy.CanAccess(ctx.LookupContextFor(target.System)))
                //     throw Errors.LookupNotAllowed;

                if (target.Color == null)
                    if (ctx.System?.Id == target.System)
                        await ctx.Reply(
                            $"This member does not have a color set. To set one, type `pk;member {target.Reference()} color <color>`.");
                    else
                        await ctx.Reply("This member does not have a color set.");
                else
                    await ctx.Reply(embed: new EmbedBuilder()
                        .Title("Member color")
                        .Color(target.Color.ToDiscordColor())
                        .Thumbnail(new($"https://fakeimg.pl/256x256/{target.Color}/?text=%20"))
                        .Description($"This member's color is **#{target.Color}**."
                                         + (ctx.System?.Id == target.System ? $" To clear it, type `pk;member {target.Reference()} color -clear`." : ""))
                        .Build());
            }
            else
            {
                ctx.CheckOwnMember(target);

                if (color.StartsWith("#")) color = color.Substring(1);
                if (!Regex.IsMatch(color, "^[0-9a-fA-F]{6}$")) throw Errors.InvalidColorError(color);
                
                var patch = new MemberPatch {Color = Partial<string>.Present(color.ToLowerInvariant())};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));

                await ctx.Reply(embed: new EmbedBuilder()
                    .Title($"{Emojis.Success} Member color changed.")
                    .Color(color.ToDiscordColor())
                    .Thumbnail(new($"https://fakeimg.pl/256x256/{color}/?text=%20"))
                    .Build());
            }
        }
        public async Task Birthday(Context ctx, PKMember target)
        {
            if (await ctx.MatchClear("this member's birthday"))
            {
                ctx.CheckOwnMember(target);
                
                var patch = new MemberPatch {Birthday = Partial<LocalDate?>.Null()};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));

                await ctx.Reply($"{Emojis.Success} Member birthdate cleared.");
            } 
            else if (!ctx.HasNext())
            {
                if (!target.BirthdayPrivacy.CanAccess(ctx.LookupContextFor(target.System)))
                    throw Errors.LookupNotAllowed;
                
                if (target.Birthday == null)
                    await ctx.Reply("This member does not have a birthdate set."
                        + (ctx.System?.Id == target.System ? $" To set one, type `pk;member {target.Reference()} birthdate <birthdate>`." : ""));
                else
                    await ctx.Reply($"This member's birthdate is **{target.BirthdayString}**."
                                    + (ctx.System?.Id == target.System ? $" To clear it, type `pk;member {target.Reference()} birthdate -clear`." : ""));
            }
            else
            {
                ctx.CheckOwnMember(target);
                
                var birthdayStr = ctx.RemainderOrNull();
                var birthday = DateUtils.ParseDate(birthdayStr, true);
                if (birthday == null) throw Errors.BirthdayParseError(birthdayStr);
                
                var patch = new MemberPatch {Birthday = Partial<LocalDate?>.Present(birthday)};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));

                await ctx.Reply($"{Emojis.Success} Member birthdate changed.");
            }
        }
        
        private async Task<EmbedBuilder> CreateMemberNameInfoEmbed(Context ctx, PKMember target)
        {
            var lcx = ctx.LookupContextFor(target);
            
            MemberGuildSettings memberGuildConfig = null;
            if (ctx.Guild != null)
                memberGuildConfig = await _db.Execute(c => _repo.GetMemberGuild(c, ctx.Guild.Id, target.Id));

            var eb = new EmbedBuilder()
                .Title($"Member names")
                .Footer(new($"Member ID: {target.Hid} | Active name in bold. Server name overrides display name, which overrides base name."));

            if (target.DisplayName == null && memberGuildConfig?.DisplayName == null)
                eb.Field(new("Name", $"**{target.NameFor(ctx)}**"));
            else
                eb.Field(new("Name", target.NameFor(ctx)));

            if (target.NamePrivacy.CanAccess(lcx))
            {
                if (target.DisplayName != null && memberGuildConfig?.DisplayName == null)
                    eb.Field(new("Display Name", $"**{target.DisplayName}**"));
                else
                    eb.Field(new("Display Name", target.DisplayName ?? "*(none)*"));
            }

            if (ctx.Guild != null)
            {
                if (memberGuildConfig?.DisplayName != null)
                    eb.Field(new($"Server Name (in {ctx.Guild.Name})", $"**{memberGuildConfig.DisplayName}**"));
                else
                    eb.Field(new($"Server Name (in {ctx.Guild.Name})", memberGuildConfig?.DisplayName ?? "*(none)*"));
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
                    var memberGuildConfig = await _db.Execute(c => _repo.GetMemberGuild(c, ctx.Guild.Id, target.Id));
                    if (memberGuildConfig.DisplayName != null)
                        successStr += $" However, this member has a server name set in this server ({ctx.Guild.Name}), and will be proxied using that name, \"{memberGuildConfig.DisplayName}\", here.";
                }

                await ctx.Reply(successStr);
            }
            
            if (await ctx.MatchClear("this member's display name"))
            {
                ctx.CheckOwnMember(target);
                
                var patch = new MemberPatch {DisplayName = Partial<string>.Null()};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));

                await PrintSuccess($"{Emojis.Success} Member display name cleared. This member will now be proxied using their member name \"{target.NameFor(ctx)}\".");
            }
            else if (!ctx.HasNext())
            {
                // No perms check, display name isn't covered by member privacy 
                var eb = await CreateMemberNameInfoEmbed(ctx, target);
                if (ctx.System?.Id == target.System)
                    eb.Description($"To change display name, type `pk;member {target.Reference()} displayname <display name>`.\nTo clear it, type `pk;member {target.Reference()} displayname -clear`.");
                await ctx.Reply(embed: eb.Build());
            }
            else
            {
                ctx.CheckOwnMember(target);
                
                var newDisplayName = ctx.RemainderOrNull();
                
                var patch = new MemberPatch {DisplayName = Partial<string>.Present(newDisplayName)};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));

                await PrintSuccess($"{Emojis.Success} Member display name changed. This member will now be proxied using the name \"{newDisplayName}\".");
            }
        }
        
        public async Task ServerName(Context ctx, PKMember target)
        {
            ctx.CheckGuildContext();
            
            if (await ctx.MatchClear("this member's server name"))
            {
                ctx.CheckOwnMember(target);

                var patch = new MemberGuildPatch {DisplayName = null};
                await _db.Execute(conn => _repo.UpsertMemberGuild(conn, target.Id, ctx.Guild.Id, patch));

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
                    eb.Description($"To change server name, type `pk;member {target.Reference()} servername <server name>`.\nTo clear it, type `pk;member {target.Reference()} servername -clear`.");
                await ctx.Reply(embed: eb.Build());
            }
            else
            {
                ctx.CheckOwnMember(target);
                
                var newServerName = ctx.RemainderOrNull();
                
                var patch = new MemberGuildPatch {DisplayName = newServerName};
                await _db.Execute(conn => _repo.UpsertMemberGuild(conn, target.Id, ctx.Guild.Id, patch));

                await ctx.Reply($"{Emojis.Success} Member server name changed. This member will now be proxied using the name \"{newServerName}\" in this server ({ctx.Guild.Name}).");
            }
        }
        
        public async Task KeepProxy(Context ctx, PKMember target)
        {
            ctx.CheckSystem().CheckOwnMember(target);

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
            await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));
            
            if (newValue)
                await ctx.Reply($"{Emojis.Success} Member proxy tags will now be included in the resulting message when proxying.");
            else
                await ctx.Reply($"{Emojis.Success} Member proxy tags will now not be included in the resulting message when proxying.");
        }

        public async Task MemberAutoproxy(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            bool newValue;
            if (ctx.Match("on", "enabled", "true", "yes") || ctx.MatchFlag("on", "enabled", "true", "yes")) newValue = true;
            else if (ctx.Match("off", "disabled", "false", "no") || ctx.MatchFlag("off", "disabled", "false", "no")) newValue = false;
            else if (ctx.HasNext()) throw new PKSyntaxError("You must pass either \"on\" or \"off\".");
            else
            {
                if (target.AllowAutoproxy)
                    await ctx.Reply("Latch/front autoproxy are **enabled** for this member. This member will be automatically proxied when autoproxy is set to latch or front mode.");
                else
                    await ctx.Reply("Latch/front autoproxy are **disabled** for this member. This member will not be automatically proxied when autoproxy is set to latch or front mode.");
                return;
            };

            var patch = new MemberPatch {AllowAutoproxy = Partial<bool>.Present(newValue)};
            await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));

            if (newValue)
                await ctx.Reply($"{Emojis.Success} Latch / front autoproxy have been **enabled** for this member.");
            else
                await ctx.Reply($"{Emojis.Success} Latch / front autoproxy have been **disabled** for this member.");
        }

        public async Task Privacy(Context ctx, PKMember target, PrivacyLevel? newValueFromCommand)
        {
            ctx.CheckSystem().CheckOwnMember(target);

            // Display privacy settings
            if (!ctx.HasNext() && newValueFromCommand == null)
            {
                await ctx.Reply(embed: new EmbedBuilder()
                    .Title($"Current privacy settings for {target.NameFor(ctx)}")
                    .Field(new("Name (replaces name with display name if member has one)",target.NamePrivacy.Explanation()))
                    .Field(new("Description", target.DescriptionPrivacy.Explanation()))
                    .Field(new("Avatar", target.AvatarPrivacy.Explanation()))
                    .Field(new("Birthday", target.BirthdayPrivacy.Explanation()))
                    .Field(new("Pronouns", target.PronounPrivacy.Explanation()))
                    .Field(new("Meta (message count, last front, last message)",target.MetadataPrivacy.Explanation()))
                    .Field(new("Visibility", target.MemberVisibility.Explanation()))
                    .Description("To edit privacy settings, use the command:\n`pk;member <member> privacy <subject> <level>`\n\n- `subject` is one of `name`, `description`, `avatar`, `birthday`, `pronouns`, `created`, `messages`, `visibility`, or `all`\n- `level` is either `public` or `private`.")
                    .Build()); 
                return;
            }
            
            // Get guild settings (mostly for warnings and such)
            MemberGuildSettings guildSettings = null;
            if (ctx.Guild != null)
                guildSettings = await _db.Execute(c => _repo.GetMemberGuild(c, ctx.Guild.Id, target.Id));

            async Task SetAll(PrivacyLevel level)
            {
                await _db.Execute(c => _repo.UpdateMember(c, target.Id, new MemberPatch().WithAllPrivacy(level)));
                
                if (level == PrivacyLevel.Private)
                    await ctx.Reply($"{Emojis.Success} All {target.NameFor(ctx)}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see nothing on the member card.");
                else 
                    await ctx.Reply($"{Emojis.Success} All {target.NameFor(ctx)}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see everything on the member card.");
            }

            async Task SetLevel(MemberPrivacySubject subject, PrivacyLevel level)
            {
                await _db.Execute(c => _repo.UpdateMember(c, target.Id, new MemberPatch().WithPrivacy(subject, level)));
                
                var subjectName = subject switch
                {
                    MemberPrivacySubject.Name => "name privacy",
                    MemberPrivacySubject.Description => "description privacy",
                    MemberPrivacySubject.Avatar => "avatar privacy",
                    MemberPrivacySubject.Pronouns => "pronoun privacy",
                    MemberPrivacySubject.Birthday => "birthday privacy",
                    MemberPrivacySubject.Metadata => "metadata privacy",
                    MemberPrivacySubject.Visibility => "visibility",
                    _ => throw new ArgumentOutOfRangeException($"Unknown privacy subject {subject}")
                };
                
                var explanation = (subject, level) switch
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
                    
                    _ => throw new InvalidOperationException($"Invalid subject/level tuple ({subject}, {level})")
                };
                
                await ctx.Reply($"{Emojis.Success} {target.NameFor(ctx)}'s **{subjectName}** has been set to **{level.LevelName()}**. {explanation}");
                
                // Name privacy only works given a display name
                if (subject == MemberPrivacySubject.Name && level == PrivacyLevel.Private && target.DisplayName == null)
                    await ctx.Reply($"{Emojis.Warn} This member does not have a display name set, and name privacy **will not take effect**.");
                
                // Avatar privacy doesn't apply when proxying if no server avatar is set
                if (subject == MemberPrivacySubject.Avatar && level == PrivacyLevel.Private && guildSettings?.AvatarUrl == null)
                    await ctx.Reply($"{Emojis.Warn} This member does not have a server avatar set, so *proxying* will **still show the member avatar**. If you want to hide your avatar when proxying here, set a server avatar: `pk;member {target.Reference()} serveravatar`");
            }

            if (ctx.Match("all") || newValueFromCommand != null)
                await SetAll(newValueFromCommand ?? ctx.PopPrivacyLevel());
            else
                await SetLevel(ctx.PopMemberPrivacySubject(), ctx.PopPrivacyLevel());
        }
        
        public async Task Delete(Context ctx, PKMember target)
        {
            ctx.CheckSystem().CheckOwnMember(target);
            
            await ctx.Reply($"{Emojis.Warn} Are you sure you want to delete \"{target.NameFor(ctx)}\"? If so, reply to this message with the member's ID (`{target.Hid}`). __***This cannot be undone!***__");
            if (!await ctx.ConfirmWithReply(target.Hid)) throw Errors.MemberDeleteCancelled;
            
            await _db.Execute(conn => _repo.DeleteMember(conn, target.Id));
            
            await ctx.Reply($"{Emojis.Success} Member deleted.");
        }
    }
}
