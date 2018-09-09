class PluralKitError(Exception):
    pass


class ExistingSystemError(PluralKitError):
    pass


class DescriptionTooLongError(PluralKitError):
    pass


class TagTooLongError(PluralKitError):
    pass


class TagTooLongWithMembersError(PluralKitError):
    def __init__(self, member_names):
        self.member_names = member_names


class CustomEmojiError(PluralKitError):
    pass


class InvalidAvatarURLError(PluralKitError):
    pass


class AccountAlreadyLinkedError(PluralKitError):
    def __init__(self, existing_system):
        self.existing_system = existing_system


class UnlinkingLastAccountError(PluralKitError):
    pass
