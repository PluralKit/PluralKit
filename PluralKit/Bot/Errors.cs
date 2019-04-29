namespace PluralKit.Bot {
    public static class Errors {
        // TODO: is returning constructed errors and throwing them at call site a good idea, or should these be methods that insta-throw instead?

        public static PKError NotOwnSystemError => new PKError($"You can only run this command on your own system.");
        public static PKError NotOwnMemberError => new PKError($"You can only run this command on your own member.");
        public static PKError NoSystemError => new PKError("You do not have a system registered with PluralKit. To create one, type `pk;system new`.");
        public static PKError ExistinSystemError => new PKError("You already have a system registered with PluralKit. To view it, type `pk;system`. If you'd like to delete your system and start anew, type `pk;system delete`, or if you'd like to unlink this account from it, type `pk;unlink`.");
        public static PKError MissingMemberError => new PKSyntaxError("You need to specify a member to run this command on.");

        public static PKError SystemNameTooLongError(int length) => new PKError($"System name too long ({length}/{Limits.MaxSystemNameLength} characters).");
        public static PKError SystemTagTooLongError(int length) => new PKError($"System tag too long ({length}/{Limits.MaxSystemTagLength} characters).");
        public static PKError DescriptionTooLongError(int length) => new PKError($"Description too long ({length}/{Limits.MaxDescriptionLength} characters).");
        public static PKError MemberNameTooLongError(int length) => new PKError($"Member name too long ({length}/{Limits.MaxMemberNameLength} characters).");
        public static PKError MemberPronounsTooLongError(int length) => new PKError($"Member pronouns too long ({length}/{Limits.MaxMemberNameLength} characters).");
    }
}