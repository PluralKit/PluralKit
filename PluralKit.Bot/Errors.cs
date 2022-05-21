using System.Net;

using Humanizer;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot;

/// <summary>
///     An exception class representing user-facing errors caused when parsing and executing commands.
/// </summary>
public class PKError: Exception
{
    public PKError(string message) : base(message) { }
}

/// <summary>
///     A subclass of <see cref="PKError" /> that represent command syntax errors, meaning they'll have their command
///     usages printed in the message.
/// </summary>
public class PKSyntaxError: PKError
{
    public PKSyntaxError(string message) : base(message) { }
}

public static class Errors
{
    // TODO: is returning constructed errors and throwing them at call site a good idea, or should these be methods that insta-throw instead?
    // or should we just like... go back to inlining them? at least for the one-time-use commands

    public static PKError NotOwnSystemError => new("You can only run this command on your own system.");
    public static PKError NotOwnMemberError => new("You can only run this command on your own member.");
    public static PKError NotOwnGroupError => new("You can only run this command on your own group.");

    public static PKError NotOwnInfo => new("You cannot look up private information of another system.");

    public static PKError NoSystemError =>
        new("You do not have a system registered with PluralKit. To create one, type `pk;system new`.");

    public static PKError ExistingSystemError => new(
        "You already have a system registered with PluralKit. To view it, type `pk;system`. If you'd like to delete your system and start anew, type `pk;system delete`, or if you'd like to unlink this account from it, type `pk;unlink`.");

    public static PKError MissingMemberError =>
        new PKSyntaxError("You need to specify a member to run this command on.");

    public static PKError ProxyMustHaveText =>
        new PKSyntaxError("Example proxy message must contain the string 'text'.");

    public static PKError ProxyMultipleText =>
        new PKSyntaxError("Example proxy message must contain the string 'text' exactly once.");

    public static PKError MemberDeleteCancelled => new($"Member deletion cancelled. Stay safe! {Emojis.ThumbsUp}");

    public static PKError AvatarInvalid =>
        new("Could not read image file - perhaps it's corrupted or the wrong format. Try a different image.");

    public static PKError UserHasNoAvatar => new("The given user has no avatar set.");

    public static PKError AccountAlreadyLinked => new("That account is already linked to your system.");
    public static PKError AccountNotLinked => new("That account isn't linked to your system.");

    public static PKError UnlinkingLastAccount => new(
        "Since this is the only account linked to this system, you cannot unlink it (as that would leave your system account-less). If you would like to delete your system, use `pk;system delete`.");

    public static PKError MemberLinkCancelled => new("Member link cancelled.");
    public static PKError MemberUnlinkCancelled => new("Member unlink cancelled.");

    public static PKError DuplicateSwitchMembers => new("Duplicate members in member list.");
    public static PKError SwitchMemberNotInSystem => new("One or more switch members aren't in your own system.");
    public static PKError SwitchTimeInFuture => new("Can't move switch to a time in the future.");
    public static PKError NoRegisteredSwitches => new("There are no registered switches for this system.");
    public static PKError SwitchMoveCancelled => new("Switch move cancelled.");
    public static PKError SwitchEditCancelled => new("Switch edit cancelled.");
    public static PKError SwitchDeleteCancelled => new("Switch deletion cancelled.");
    public static PKError TimezoneChangeCancelled => new("Time zone change cancelled.");

    public static PKError NoImportFilePassed =>
        new(
            "You must either pass an URL to a file as a command parameter, or as an attachment to the message containing the command.");

    public static PKError InvalidImportFile =>
        new(
            "Imported data file invalid. Make sure this is a .json file directly exported from PluralKit or Tupperbox.");

    public static PKError ImportCancelled => new("Import cancelled.");

    public static PKError FrontPercentTimeInFuture =>
        new("Cannot get the front percent between now and a time in the future.");

    public static PKError LookupNotAllowed => new("You do not have permission to access this information.");

    public static PKError StringTooLongError(string name, int length, int maxLength) =>
        new($"{name} too long ({length}/{maxLength} characters).");

    public static PKError MemberLimitReachedError(int limit) => new(
        $"System has reached the maximum number of members ({limit}). Please delete unused members first in order to create new ones.");

    public static PKError InvalidColorError(string color) =>
        new($"\"{color}\" is not a valid color. Color must be in 6-digit RGB hex format (eg. #ff0000).");

    public static PKError BirthdayParseError(string birthday) => new(
        $"\"{birthday}\" could not be parsed as a valid date. Try a format like \"2016-12-24\" or \"May 3 1996\".");

    public static PKError AvatarServerError(HttpStatusCode statusCode) => new(
        $"Server responded with status code {(int)statusCode}, are you sure your link is working?");

    public static PKError AvatarFileSizeLimit(long size) => new(
        $"File size too large ({size.Bytes().ToString("#.#")} > {Limits.AvatarFileSizeLimit.Bytes().ToString("#.#")}), try shrinking or compressing the image.");

    public static PKError AvatarNotAnImage(string mimeType) => new(
        $"The given link does not point to an image{(mimeType != null ? $" ({mimeType})" : "")}. Make sure you're using a direct link (ending in .jpg, .png, .gif).");

    public static PKError AvatarDimensionsTooLarge(int width, int height) => new(
        $"Image too large ({width}x{height} > {Limits.AvatarDimensionLimit}x{Limits.AvatarDimensionLimit}), try resizing the image.");

    public static PKError InvalidUrl(string url) => new("The given URL is invalid.");

    public static PKError UrlTooLong(string url) =>
        new($"The given URL is too long ({url.Length}/{Limits.MaxUriLength} characters).");

    public static PKError AccountInOtherSystem(PKSystem system) =>
        new($"The mentioned account is already linked to another system (see `pk;system {system.Hid}`).");

    public static PKError SameSwitch(ICollection<PKMember> members, LookupContext ctx)
    {
        if (members.Count == 0) return new PKError("There's already no one in front.");
        if (members.Count == 1) return new PKError($"Member {members.First().NameFor(ctx)} is already fronting.");
        return new PKError(
            $"Members {string.Join(", ", members.Select(m => m.NameFor(ctx)))} are already fronting.");
    }

    public static PKError InvalidDateTime(string str) => new(
        $"Could not parse '{str}' as a valid date/time. Try using a syntax such as \"May 21, 12:30 PM\" or \"3d12h\" (ie. 3 days, 12 hours ago).");

    public static PKError SwitchMoveBeforeSecondLast(ZonedDateTime time) => new(
        $"Can't move switch to before last switch time ({time.FormatZoned()}), as it would cause conflicts.");

    public static PKError TimezoneParseError(string timezone) => new(
        $"Could not parse timezone offset {timezone}. Offset must be a value like 'UTC+5' or 'GMT-4:30'.");

    public static PKError InvalidTimeZone(string zoneStr) => new(
        $"Invalid time zone ID '{zoneStr}'. To find your time zone ID, use the following website: <https://xske.github.io/tz>");

    public static PKError AmbiguousTimeZone(string zoneStr, int count) => new(
        $"The time zone query '{zoneStr}' resulted in **{count}** different time zone regions. Try being more specific - e.g. pass an exact time zone specifier from the following website: <https://xske.github.io/tz>");

    public static PKError MessageNotFound(ulong id) =>
        new($"Message with ID '{id}' not found. Are you sure it's a message proxied by PluralKit?");

    public static PKError DurationParseError(string durationStr) => new(
        $"Could not parse {durationStr.AsCode()} as a valid duration. Try a format such as `30d`, `1d3h` or `20m30s`.");

    public static PKError GuildNotFound(ulong guildId) => new(
        $"Guild with ID `{guildId}` not found, or I cannot access it. Note that you must be a member of the guild you are querying.");

    public static PKError DisplayNameTooLong(string displayName, int maxLength) => new(
        $"Display name too long ({displayName.Length} > {maxLength} characters). Use a shorter display name, or shorten your system tag.");

    public static PKError ProxyNameTooShort(string name) => new(
        $"The webhook's name, {name.AsCode()}, is shorter than two characters, and thus cannot be proxied. Please change the member name or use a longer system tag.");

    public static PKError ProxyNameTooLong(string name) => new(
        $"The webhook's name, {name.AsCode()}, is too long ({name.Length} > {Limits.MaxProxyNameLength} characters), and thus cannot be proxied. Please change the member name, display name or server display name, or use a shorter system tag.");

    public static PKError ProxyTagAlreadyExists(ProxyTag tagToAdd, PKMember member) => new(
        $"That member already has the proxy tag {tagToAdd.ProxyString.AsCode()}. The member currently has these tags: {member.ProxyTagsString()}");

    public static PKError ProxyTagDoesNotExist(ProxyTag tagToRemove, PKMember member) => new(
        $"That member does not have the proxy tag {tagToRemove.ProxyString.AsCode()}. The member currently has these tags: {member.ProxyTagsString()}");

    public static PKError LegacyAlreadyHasProxyTag(ProxyTag requested, PKMember member, Context ctx) => new(
        $"This member already has more than one proxy tag set: {member.ProxyTagsString()}\nConsider using the {$"pk;member {member.Reference(ctx)} proxy add {requested.ProxyString}".AsCode()} command instead.");

    public static PKError EmptyProxyTags(PKMember member, Context ctx) => new(
        $"The example proxy `text` is equivalent to having no proxy tags at all, since there are no symbols or brackets on either end. If you'd like to clear your proxy tags, use `pk;member {member.Reference(ctx)} proxy clear`.");

    public static PKError GenericCancelled() => new("Operation cancelled.");

    public static PKError AttachmentTooLarge(int mb) => new(
        $"PluralKit cannot proxy attachments over {mb} megabytes in this server (as webhooks aren't considered as having Discord Nitro) :(");

    public static PKError ChannelNotFound(string channelString) =>
        new($"Channel \"{channelString}\" not found or is not in this server.");
}