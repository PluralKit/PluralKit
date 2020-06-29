using System;

namespace PluralKit.Core
{
    public enum MemberPrivacySubject {
        Visibility,
        Name,
        Description,
        Avatar,
        Birthday,
        Pronouns,
        Metadata
    }
    
    public static class PrivacyUtils
    {
        public static string Name(this MemberPrivacySubject subject) => subject switch
        {
            MemberPrivacySubject.Name => "name",
            MemberPrivacySubject.Description => "description",
            MemberPrivacySubject.Avatar => "avatar",
            MemberPrivacySubject.Pronouns => "pronouns",
            MemberPrivacySubject.Birthday => "birthday",
            MemberPrivacySubject.Metadata => "metadata",
            MemberPrivacySubject.Visibility => "visibility",
            _ => throw new ArgumentOutOfRangeException($"Unknown privacy subject {subject}")
        };

        public static void SetPrivacy(this MemberPatch member, MemberPrivacySubject subject, PrivacyLevel level)
        {
            // what do you mean switch expressions can't be statements >.>
            _ = subject switch
            {
                MemberPrivacySubject.Name => member.NamePrivacy = Partial<PrivacyLevel>.Present(level),
                MemberPrivacySubject.Description => member.DescriptionPrivacy = Partial<PrivacyLevel>.Present(level),
                MemberPrivacySubject.Avatar => member.AvatarPrivacy = Partial<PrivacyLevel>.Present(level),
                MemberPrivacySubject.Pronouns => member.PronounPrivacy = Partial<PrivacyLevel>.Present(level),
                MemberPrivacySubject.Birthday => member.BirthdayPrivacy= Partial<PrivacyLevel>.Present(level),
                MemberPrivacySubject.Metadata => member.MetadataPrivacy = Partial<PrivacyLevel>.Present(level),
                MemberPrivacySubject.Visibility => member.Visibility = Partial<PrivacyLevel>.Present(level),
                _ => throw new ArgumentOutOfRangeException($"Unknown privacy subject {subject}")
            };
        }

        public static void SetAllPrivacy(this MemberPatch member, PrivacyLevel level)
        {
            member.NamePrivacy = Partial<PrivacyLevel>.Present(level);
            member.DescriptionPrivacy = Partial<PrivacyLevel>.Present(level);
            member.AvatarPrivacy = Partial<PrivacyLevel>.Present(level);
            member.PronounPrivacy = Partial<PrivacyLevel>.Present(level);
            member.BirthdayPrivacy = Partial<PrivacyLevel>.Present(level);
            member.MetadataPrivacy = Partial<PrivacyLevel>.Present(level);
            member.Visibility = Partial<PrivacyLevel>.Present(level);
        }
        
        public static bool TryParseMemberPrivacy(string input, out MemberPrivacySubject subject)
        {
            switch (input.ToLowerInvariant())
            {
                case "name":
                    subject = MemberPrivacySubject.Name;
                    break;
                case "description":
                case "desc":
                case "text":
                case "info":
                    subject = MemberPrivacySubject.Description;
                    break;
                case "avatar":
                case "pfp":
                case "pic":
                case "icon":
                    subject = MemberPrivacySubject.Avatar;
                    break;
                case "birthday":
                case "birth":
                case "bday":
                case "birthdate":
                case "bdate":
                    subject = MemberPrivacySubject.Birthday;
                    break;
                case "pronouns":
                case "pronoun":
                    subject = MemberPrivacySubject.Pronouns;
                    break;
                case "meta":
                case "metadata":
                case "created":
                    subject = MemberPrivacySubject.Metadata;
                    break;
                case "visibility":
                case "hidden": 
                case "shown":
                case "visible":
                case "list":
                    subject = MemberPrivacySubject.Visibility;
                    break;
                default:
                    subject = MemberPrivacySubject.Name;
                    return false;
            }
            return true;
        } 
    }
}