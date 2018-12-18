from typing import Tuple


class PluralKitError(Exception):
    def __init__(self, message):
        self.message = message
        self.help_page = None

    def with_help(self, help_page: Tuple[str, str]):
        self.help_page = help_page


class ExistingSystemError(PluralKitError):
    def __init__(self):
        super().__init__(
            "You already have a system registered. To delete your system, use `pk;system delete`, or to unlink your system from this account, use `pk;system unlink`.")


class DescriptionTooLongError(PluralKitError):
    def __init__(self):
        super().__init__("You can't have a description longer than 1024 characters.")


class TagTooLongError(PluralKitError):
    def __init__(self):
        super().__init__("You can't have a system tag longer than 32 characters.")


class TagTooLongWithMembersError(PluralKitError):
    def __init__(self, member_names):
        super().__init__(
            "The maximum length of a name plus the system tag is 32 characters. The following members would exceed the limit: {}. Please reduce the length of the tag, or rename the members.".format(
                ", ".join(member_names)))
        self.member_names = member_names


class CustomEmojiError(PluralKitError):
    def __init__(self):
        super().__init__(
            "Due to a Discord limitation, custom emojis aren't supported. Please use a standard emoji instead.")


class InvalidAvatarURLError(PluralKitError):
    def __init__(self):
        super().__init__("Invalid image URL.")


class AccountInOwnSystemError(PluralKitError):
    def __init__(self):
        super().__init__("That account is already linked to your own system.")


class AccountAlreadyLinkedError(PluralKitError):
    def __init__(self, existing_system):
        super().__init__("The mentioned account is already linked to a system (`{}`)".format(existing_system.hid))
        self.existing_system = existing_system


class UnlinkingLastAccountError(PluralKitError):
    def __init__(self):
        super().__init__("This is the only account on your system, so you can't unlink it.")


class MemberNameTooLongError(PluralKitError):
    def __init__(self, tag_present: bool):
        if tag_present:
            super().__init__(
                "The maximum length of a name plus the system tag is 32 characters. Please reduce the length of the tag, or choose a shorter member name.")
        else:
            super().__init__("The maximum length of a member name is 32 characters.")


class InvalidColorError(PluralKitError):
    def __init__(self):
        super().__init__("Color must be a valid hex color. (eg. #ff0000)")


class InvalidDateStringError(PluralKitError):
    def __init__(self):
        super().__init__("Invalid date string. Date must be in ISO-8601 format (YYYY-MM-DD, eg. 1999-07-25).")


class MembersAlreadyFrontingError(PluralKitError):
    def __init__(self, members: "List[Member]"):
        if len(members) == 0:
            super().__init__("There are already no members fronting.")
        elif len(members) == 1:
            super().__init__("Member {} is already fronting.".format(members[0].name))
        else:
            super().__init__("Members {} are already fronting.".format(", ".join([member.name for member in members])))


class DuplicateSwitchMembersError(PluralKitError):
    def __init__(self):
        super().__init__("Duplicate members in member list.")


class InvalidTimeZoneError(PluralKitError):
    def __init__(self, tz_name: str):
        super().__init__("Invalid time zone designation \"{}\".\n\nFor a list of valid time zone designations, see the `TZ database name` column here: <https://en.wikipedia.org/wiki/List_of_tz_database_time_zones#List>.".format(tz_name))
