using System;
using System.Linq;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API
{
    public static class JsonModelExt
    {
        public static JObject ToJson(this PKSystem system, LookupContext ctx)
        {
            var o = new JObject();
            o.Add("id", system.Hid);
            o.Add("name", system.Name);
            o.Add("description", system.DescriptionPrivacy.CanAccess(ctx) ? system.Description : null);
            o.Add("tag", system.Tag);
            o.Add("avatar_url", system.AvatarUrl);
            o.Add("created", DateTimeFormats.TimestampExportFormat.Format(system.Created));
            o.Add("tz", system.UiTz);
            o.Add("description_privacy", ctx == LookupContext.ByOwner ? system.DescriptionPrivacy.ToJsonString() : null);
            o.Add("member_list_privacy", ctx == LookupContext.ByOwner ? system.MemberListPrivacy.ToJsonString() : null);
            o.Add("front_privacy", ctx == LookupContext.ByOwner ? system.FrontPrivacy.ToJsonString() : null);
            o.Add("front_history_privacy", ctx == LookupContext.ByOwner ? ctx == LookupContext.ByOwner ? system.FrontHistoryPrivacy.ToJsonString() : null : null);
            return o;
        }

        public static void ApplyJson(this PKSystem system, JObject o)
        {
            if (o.ContainsKey("name")) system.Name = o.Value<string>("name").NullIfEmpty().BoundsCheckField(Limits.MaxSystemNameLength, "System name");
            if (o.ContainsKey("description")) system.Description = o.Value<string>("description").NullIfEmpty().BoundsCheckField(Limits.MaxDescriptionLength, "System description");
            if (o.ContainsKey("tag")) system.Tag = o.Value<string>("tag").NullIfEmpty().BoundsCheckField(Limits.MaxSystemTagLength, "System tag");
            if (o.ContainsKey("avatar_url")) system.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty().BoundsCheckField(Limits.MaxUriLength, "System avatar URL");
            if (o.ContainsKey("tz")) system.UiTz = o.Value<string>("tz") ?? "UTC";
            
            if (o.ContainsKey("description_privacy")) system.DescriptionPrivacy = o.Value<string>("description_privacy").ParsePrivacy("description");
            if (o.ContainsKey("member_list_privacy")) system.MemberListPrivacy = o.Value<string>("member_list_privacy").ParsePrivacy("member list");
            if (o.ContainsKey("front_privacy")) system.FrontPrivacy = o.Value<string>("front_privacy").ParsePrivacy("front");
            if (o.ContainsKey("front_history_privacy")) system.FrontHistoryPrivacy = o.Value<string>("front_history_privacy").ParsePrivacy("front history");
        }
        
        public static JObject ToJson(this PKMember member, LookupContext ctx)
        {
            var o = new JObject();
            o.Add("id", member.Hid);
            o.Add("name", member.NamePrivacy.CanAccess(ctx) ? member.Name : member.DisplayName ?? member.Name);
            o.Add("color", member.ColorPrivacy.CanAccess(ctx) ? member.Color : null);
            o.Add("display_name", member.DisplayName);
            o.Add("birthday", member.BirthdayPrivacy.CanAccess(ctx) && member.Birthday.HasValue ? DateTimeFormats.DateExportFormat.Format(member.Birthday.Value) : null);
            o.Add("pronouns", member.PronounPrivacy.CanAccess(ctx) ? member.Pronouns : null);
            o.Add("avatar_url", member.AvatarUrl);
            o.Add("description", member.DescriptionPrivacy.CanAccess(ctx) ? member.Description : null);

            o.Add("visibility", ctx == LookupContext.ByOwner ? (member.MemberVisibility == PrivacyLevel.Private ? "private" : "public") : null);
            o.Add("name_privacy", ctx == LookupContext.ByOwner ? (member.NamePrivacy == PrivacyLevel.Private ? "private" : "public") : null);
            o.Add("description_privacy", ctx == LookupContext.ByOwner ? (member.DescriptionPrivacy == PrivacyLevel.Private ? "private" : "public") : null);
            o.Add("birthday_privacy", ctx == LookupContext.ByOwner ? (member.BirthdayPrivacy == PrivacyLevel.Private ? "private" : "public") : null);
            o.Add("pronouns_privacy", ctx == LookupContext.ByOwner ? (member.PronounPrivacy == PrivacyLevel.Private ? "private" : "public") : null);
            o.Add("color_privacy", ctx == LookupContext.ByOwner ? (member.ColorPrivacy == PrivacyLevel.Private ? "private" : "public") : null);
            o.Add("message_count_privacy", ctx == LookupContext.ByOwner ? (member.MessageCountPrivacy == PrivacyLevel.Private ? "private" : "public") : null);
            o.Add("created_timestamp_privacy", ctx == LookupContext.ByOwner ? (member.CreatedTimestampPrivacy == PrivacyLevel.Private ? "private" : "public") : null);
            
            var tagArray = new JArray();
            foreach (var tag in member.ProxyTags) 
                tagArray.Add(new JObject {{"prefix", tag.Prefix}, {"suffix", tag.Suffix}});
            o.Add("proxy_tags", tagArray);

            o.Add("keep_proxy", member.KeepProxy);
            if(member.CreatedTimestampPrivacy.CanAccess(ctx))
                o.Add("created", DateTimeFormats.TimestampExportFormat.Format(member.Created));
            else
                o.Add("created", null);
            

            if (member.ProxyTags.Count > 0)
            {
                // Legacy compatibility only, TODO: remove at some point
                o.Add("prefix", member.ProxyTags?.FirstOrDefault().Prefix);
                o.Add("suffix", member.ProxyTags?.FirstOrDefault().Suffix);
            }

            return o;
        }

        public static void ApplyJson(this PKMember member, JObject o)
        {
            if (o.ContainsKey("name") && o["name"].Type == JTokenType.Null) 
                throw new JsonModelParseError("Member name can not be set to null.");
            
            if (o.ContainsKey("name")) member.Name = o.Value<string>("name").BoundsCheckField(Limits.MaxMemberNameLength, "Member name");
            if (o.ContainsKey("color")) member.Color = o.Value<string>("color").NullIfEmpty()?.ToLower();
            if (o.ContainsKey("display_name")) member.DisplayName = o.Value<string>("display_name").NullIfEmpty().BoundsCheckField(Limits.MaxMemberNameLength, "Member display name");
            if (o.ContainsKey("avatar_url")) member.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty().BoundsCheckField(Limits.MaxUriLength, "Member avatar URL");
            if (o.ContainsKey("birthday"))
            {
                var str = o.Value<string>("birthday").NullIfEmpty();
                var res = DateTimeFormats.DateExportFormat.Parse(str);
                if (res.Success) member.Birthday = res.Value;
                else if (str == null) member.Birthday = null;
                else throw new JsonModelParseError("Could not parse member birthday.");
            }

            if (o.ContainsKey("pronouns")) member.Pronouns = o.Value<string>("pronouns").NullIfEmpty().BoundsCheckField(Limits.MaxPronounsLength, "Member pronouns");
            if (o.ContainsKey("description")) member.Description = o.Value<string>("description").NullIfEmpty().BoundsCheckField(Limits.MaxDescriptionLength, "Member descriptoin");
            if (o.ContainsKey("keep_proxy")) member.KeepProxy = o.Value<bool>("keep_proxy");

            if (o.ContainsKey("prefix") || o.ContainsKey("suffix") && !o.ContainsKey("proxy_tags"))
                member.ProxyTags = new[] {new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix"))};
            else if (o.ContainsKey("proxy_tags"))
            {
                member.ProxyTags = o.Value<JArray>("proxy_tags")
                    .OfType<JObject>().Select(o => new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix")))
                    .ToList();
            }
            
            if (o.ContainsKey("visibility")) member.MemberVisibility = o.Value<string>("visibility").ParsePrivacy("member");
            if (o.ContainsKey("name_privacy")) member.NamePrivacy = o.Value<string>("name_privacy").ParsePrivacy("member");
            if (o.ContainsKey("description_privacy")) member.DescriptionPrivacy = o.Value<string>("description_privacy").ParsePrivacy("member");
            if (o.ContainsKey("birthday_privacy")) member.BirthdayPrivacy = o.Value<string>("birthday_privacy").ParsePrivacy("member");
            if (o.ContainsKey("pronoun_privacy")) member.PronounPrivacy = o.Value<string>("pronoun_privacy").ParsePrivacy("member");
            if (o.ContainsKey("color_privacy")) member.ColorPrivacy = o.Value<string>("color_privacy").ParsePrivacy("member");
            if (o.ContainsKey("message_count_privacy")) member.MessageCountPrivacy = o.Value<string>("message_count_privacy").ParsePrivacy("member");
            if (o.ContainsKey("created_timestamp_privacy")) member.CreatedTimestampPrivacy = o.Value<string>("created_timestamp_privacy").ParsePrivacy("member");

        }

        private static string BoundsCheckField(this string input, int maxLength, string nameInError)
        {
            if (input != null && input.Length > maxLength)
                throw new JsonModelParseError($"{nameInError} too long ({input.Length} > {maxLength}).");
            return input;
        }

        private static string ToJsonString(this PrivacyLevel level) => level == PrivacyLevel.Private ? "private" : "public";

        private static PrivacyLevel ParsePrivacy(this string input, string errorName)
        {
            if (input == null) return PrivacyLevel.Private;
            if (input == "") return PrivacyLevel.Private;
            if (input == "private") return PrivacyLevel.Private;
            if (input == "public") return PrivacyLevel.Public;
            throw new JsonModelParseError($"Could not parse {errorName} privacy.");
        }
    }
    
    public class JsonModelParseError: Exception
    {
        public JsonModelParseError(string message): base(message) { }
    }
}