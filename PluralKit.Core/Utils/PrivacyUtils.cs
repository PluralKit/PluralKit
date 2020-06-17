using System;

namespace PluralKit.Core
{
    public enum MemberPrivacySubject {
        Visibility,
        Name,
        Description,
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
            MemberPrivacySubject.Pronouns => "pronouns",
            MemberPrivacySubject.Birthday => "birthday",
            MemberPrivacySubject.Metadata => "metadata",
            MemberPrivacySubject.Visibility => "visibility",
            _ => throw new ArgumentOutOfRangeException($"Unknown privacy subject {subject}")
        };

        public static void SetPrivacy(this PKMember member, MemberPrivacySubject subject, PrivacyLevel level)
        {
            // what do you mean switch expressions can't be statements >.>
            _ = subject switch
            {
                MemberPrivacySubject.Name => member.NamePrivacy = level,
                MemberPrivacySubject.Description => member.DescriptionPrivacy = level,
                MemberPrivacySubject.Pronouns => member.PronounPrivacy = level,
                MemberPrivacySubject.Birthday => member.BirthdayPrivacy= level,
                MemberPrivacySubject.Metadata => member.MetadataPrivacy = level,
                MemberPrivacySubject.Visibility => member.MemberVisibility = level,
                _ => throw new ArgumentOutOfRangeException($"Unknown privacy subject {subject}")
            };
        }

        public static void SetAllPrivacy(this PKMember member, PrivacyLevel level)
        {
            member.NamePrivacy = level;
            member.DescriptionPrivacy = level;
            member.PronounPrivacy = level;
            member.BirthdayPrivacy = level;
            member.MetadataPrivacy = level;
            member.MemberVisibility = level;
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
                case "birthday":
                case "birth":
                case "bday":
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