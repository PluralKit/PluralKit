using System.Net;
using Humanizer;

namespace PluralKit.Bot {
    public static class Errors {
        // TODO: is returning constructed errors and throwing them at call site a good idea, or should these be methods that insta-throw instead?

        public static PKError NotOwnSystemError => new PKError($"You can only run this command on your own system.");
        public static PKError NotOwnMemberError => new PKError($"You can only run this command on your own member.");
        public static PKError NoSystemError => new PKError("You do not have a system registered with PluralKit. To create one, type `pk;system new`.");
        public static PKError ExistingSystemError => new PKError("You already have a system registered with PluralKit. To view it, type `pk;system`. If you'd like to delete your system and start anew, type `pk;system delete`, or if you'd like to unlink this account from it, type `pk;unlink`.");
        public static PKError MissingMemberError => new PKSyntaxError("You need to specify a member to run this command on.");

        public static PKError SystemNameTooLongError(int length) => new PKError($"System name too long ({length}/{Limits.MaxSystemNameLength} characters).");
        public static PKError SystemTagTooLongError(int length) => new PKError($"System tag too long ({length}/{Limits.MaxSystemTagLength} characters).");
        public static PKError DescriptionTooLongError(int length) => new PKError($"Description too long ({length}/{Limits.MaxDescriptionLength} characters).");
        public static PKError MemberNameTooLongError(int length) => new PKError($"Member name too long ({length}/{Limits.MaxMemberNameLength} characters).");
        public static PKError MemberPronounsTooLongError(int length) => new PKError($"Member pronouns too long ({length}/{Limits.MaxMemberNameLength} characters).");
        
        public static PKError InvalidColorError(string color) => new PKError($"\"{color}\" is not a valid color. Color must be in hex format (eg. #ff0000).");
        public static PKError BirthdayParseError(string birthday) => new PKError($"\"{birthday}\" could not be parsed as a valid date. Try a format like \"2016-12-24\" or \"May 3 1996\".");
        public static PKError ProxyMustHaveText => new PKSyntaxError("Example proxy message must contain the string 'text'.");
        public static PKError ProxyMultipleText => new PKSyntaxError("Example proxy message must contain the string 'text' exactly once.");
        
        public static PKError MemberDeleteCancelled => new PKError($"Member deletion cancelled. Stay safe! {Emojis.ThumbsUp}");
        public static PKError AvatarServerError(HttpStatusCode statusCode) => new PKError($"Server responded with status code {(int) statusCode}, are you sure your link is working?");
        public static PKError AvatarFileSizeLimit(long size) => new PKError($"File size too large ({size.Bytes().ToString("#.#")} > {Limits.AvatarFileSizeLimit.Bytes().ToString("#.#")}), try shrinking or compressing the image.");
        public static PKError AvatarNotAnImage(string mimeType) => new PKError($"The given link does not point to an image{(mimeType != null ? $" ({mimeType})" : "")}. Make sure you're using a direct link (ending in .jpg, .png, .gif).");
        public static PKError AvatarDimensionsTooLarge(int width, int height) => new PKError($"Image too large ({width}x{height} > {Limits.AvatarDimensionLimit}x{Limits.AvatarDimensionLimit}), try resizing the image.");
        public static PKError InvalidUrl(string url) => new PKError($"The given URL is invalid.");
        
        public static PKError AccountAlreadyLinked => new PKError("That account is already linked to your system.");
        public static PKError AccountNotLinked => new PKError("That account isn't linked to your system.");
        public static PKError AccountInOtherSystem(PKSystem system) => new PKError($"The mentioned account is already linked to another system (see `pk;system {system.Hid}`).");
        public static PKError UnlinkingLastAccount => new PKError("Since this is the only account linked to this system, you cannot unlink it (as that would leave your system account-less).");
        public static PKError MemberLinkCancelled => new PKError("Member link cancelled.");
        public static PKError MemberUnlinkCancelled => new PKError("Member unlink cancelled.");
    }
}