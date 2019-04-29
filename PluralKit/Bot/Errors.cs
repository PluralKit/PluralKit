namespace PluralKit.Bot {
    public static class Errors {
        // TODO: is returning constructed errors and throwing them at call site a good idea, or should these be methods that insta-throw instead?
        
        public static PKError NotOwnSystemError => new PKError($"You can only run this command on your own system.");
        public static PKError NotOwnMemberError => new PKError($"You can only run this command on your own member.");
        public static PKError NoSystemError => new PKError("You do not have a system registered with PluralKit. To create one, type `pk;system new`.");
        public static PKError ExistinSystemError => new PKError("You already have a system registered with PluralKit. To view it, type `pk;system`. If you'd like to delete your system and start anew, type `pk;system delete`, or if you'd like to unlink this account from it, type `pk;unlink`.");
        public static PKError MissingMemberError => new PKSyntaxError("You need to specify a member to run this command on.");

        public static PKError SystemNameTooLongError(int nameLength) => new PKError($"Your chosen system name is too long ({nameLength}/{Limits.MaxSystemNameLength} characters).");
        public static PKError SystemTagTooLongError(int nameLength) => new PKError($"Your chosen system tag is too long ({nameLength}/{Limits.MaxSystemTagLength} characters).");
        public static PKError DescriptionTooLongError(int nameLength) => new PKError($"Your chosen description is too long ({nameLength}/{Limits.MaxDescriptionLength} characters).");
        public static PKError MemberNameTooLongError(int nameLength) => new PKError($"Your chosen member name is too long ({nameLength}/{Limits.MaxMemberNameLength} characters).");
    }
}