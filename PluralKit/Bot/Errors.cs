namespace PluralKit.Bot {
    public static class Errors {
        public static PKError NotOwnSystemError => new PKError($"You can only run this command on your own system.");
        public static PKError NotOwnMemberError => new PKError($"You can only run this command on your own member.");
        public static PKError NoSystemError => new PKError("You do not have a system registered with PluralKit. To create one, type `pk;system new`.");
        public static PKError ExistinSystemError => new PKError("You already have a system registered with PluralKit. To view it, type `pk;system`. If you'd like to delete your system and start anew, type `pk;system delete`, or if you'd like to unlink this account from it, type `pk;unlink`.");
        public static PKError MissingMemberError => new PKSyntaxError("You need to specify a member to run this command on.");
    }
}