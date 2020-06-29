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
            o.Add("description", system.DescriptionFor(ctx));
            o.Add("tag", system.Tag);
            o.Add("avatar_url", system.AvatarUrl);
            o.Add("created", system.Created.FormatExport());
            o.Add("tz", system.UiTz);
            o.Add("description_privacy", ctx == LookupContext.ByOwner ? system.DescriptionPrivacy.ToJsonString() : null);
            o.Add("member_list_privacy", ctx == LookupContext.ByOwner ? system.MemberListPrivacy.ToJsonString() : null);
            o.Add("front_privacy", ctx == LookupContext.ByOwner ? system.FrontPrivacy.ToJsonString() : null);
            o.Add("front_history_privacy", ctx == LookupContext.ByOwner ? system.FrontHistoryPrivacy.ToJsonString() : null);
            return o;
        }

        public static SystemPatch ToSystemPatch(JObject o)
        {
            var patch = new SystemPatch();
            if (o.ContainsKey("name")) patch.Name = o.Value<string>("name").NullIfEmpty().BoundsCheckField(Limits.MaxSystemNameLength, "System name");
            if (o.ContainsKey("description")) patch.Description = o.Value<string>("description").NullIfEmpty().BoundsCheckField(Limits.MaxDescriptionLength, "System description");
            if (o.ContainsKey("tag")) patch.Tag = o.Value<string>("tag").NullIfEmpty().BoundsCheckField(Limits.MaxSystemTagLength, "System tag");
            if (o.ContainsKey("avatar_url")) patch.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty().BoundsCheckField(Limits.MaxUriLength, "System avatar URL");
            if (o.ContainsKey("tz")) patch.UiTz = o.Value<string>("tz") ?? "UTC";
            
            if (o.ContainsKey("description_privacy")) patch.DescriptionPrivacy = o.Value<string>("description_privacy").ParsePrivacy("description");
            if (o.ContainsKey("member_list_privacy")) patch.MemberListPrivacy = o.Value<string>("member_list_privacy").ParsePrivacy("member list");
            if (o.ContainsKey("front_privacy")) patch.FrontPrivacy = o.Value<string>("front_privacy").ParsePrivacy("front");
            if (o.ContainsKey("front_history_privacy")) patch.FrontHistoryPrivacy = o.Value<string>("front_history_privacy").ParsePrivacy("front history");
            return patch;
        }
        
        public static JObject ToJson(this PKMember member, LookupContext ctx)
        {
            var includePrivacy = ctx == LookupContext.ByOwner;
            
            var o = new JObject();
            o.Add("id", member.Hid);
            o.Add("name", member.NameFor(ctx));
            // o.Add("color", member.ColorPrivacy.CanAccess(ctx) ? member.Color : null);
            o.Add("color", member.Color);
            o.Add("display_name", member.NamePrivacy.CanAccess(ctx) ? member.DisplayName : null);
            o.Add("birthday", member.BirthdayFor(ctx)?.FormatExport());
            o.Add("pronouns", member.PronounsFor(ctx));
            o.Add("avatar_url", member.AvatarFor(ctx));
            o.Add("description", member.DescriptionFor(ctx));
            
            var tagArray = new JArray();
            foreach (var tag in member.ProxyTags) 
                tagArray.Add(new JObject {{"prefix", tag.Prefix}, {"suffix", tag.Suffix}});
            o.Add("proxy_tags", tagArray);

            o.Add("keep_proxy", member.KeepProxy);
            
            o.Add("privacy", includePrivacy ? (member.MemberVisibility.LevelName()) : null);

            o.Add("visibility", includePrivacy ? (member.MemberVisibility.LevelName()) : null);
            o.Add("name_privacy", includePrivacy ? (member.NamePrivacy.LevelName()) : null);
            o.Add("description_privacy", includePrivacy ? (member.DescriptionPrivacy.LevelName()) : null);
            o.Add("birthday_privacy", includePrivacy ? (member.BirthdayPrivacy.LevelName()) : null);
            o.Add("pronoun_privacy", includePrivacy ? (member.PronounPrivacy.LevelName()) : null);
            o.Add("avatar_privacy", includePrivacy ? (member.AvatarPrivacy.LevelName()) : null);
            // o.Add("color_privacy", ctx == LookupContext.ByOwner ? (member.ColorPrivacy.LevelName()) : null);
            o.Add("metadata_privacy", includePrivacy ? (member.MetadataPrivacy.LevelName()) : null);

            o.Add("created", member.CreatedFor(ctx)?.FormatExport());

            if (member.ProxyTags.Count > 0)
            {
                // Legacy compatibility only, TODO: remove at some point
                o.Add("prefix", member.ProxyTags?.FirstOrDefault().Prefix);
                o.Add("suffix", member.ProxyTags?.FirstOrDefault().Suffix);
            }

            return o;
        }

        public static MemberPatch ToMemberPatch(JObject o)
        {
            var patch = new MemberPatch();

            if (o.ContainsKey("name") && o["name"].Type == JTokenType.Null) 
                throw new JsonModelParseError("Member name can not be set to null.");
            
            if (o.ContainsKey("name")) patch.Name = o.Value<string>("name").BoundsCheckField(Limits.MaxMemberNameLength, "Member name");
            if (o.ContainsKey("color")) patch.Color = o.Value<string>("color").NullIfEmpty()?.ToLower();
            if (o.ContainsKey("display_name")) patch.DisplayName = o.Value<string>("display_name").NullIfEmpty().BoundsCheckField(Limits.MaxMemberNameLength, "Member display name");
            if (o.ContainsKey("avatar_url")) patch.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty().BoundsCheckField(Limits.MaxUriLength, "Member avatar URL");
            if (o.ContainsKey("birthday"))
            {
                var str = o.Value<string>("birthday").NullIfEmpty();
                var res = DateTimeFormats.DateExportFormat.Parse(str);
                if (res.Success) patch.Birthday = res.Value;
                else if (str == null) patch.Birthday = null;
                else throw new JsonModelParseError("Could not parse member birthday.");
            }

            if (o.ContainsKey("pronouns")) patch.Pronouns = o.Value<string>("pronouns").NullIfEmpty().BoundsCheckField(Limits.MaxPronounsLength, "Member pronouns");
            if (o.ContainsKey("description")) patch.Description = o.Value<string>("description").NullIfEmpty().BoundsCheckField(Limits.MaxDescriptionLength, "Member descriptoin");
            if (o.ContainsKey("keep_proxy")) patch.KeepProxy = o.Value<bool>("keep_proxy");

            if (o.ContainsKey("prefix") || o.ContainsKey("suffix") && !o.ContainsKey("proxy_tags"))
                patch.ProxyTags = new[] {new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix"))};
            else if (o.ContainsKey("proxy_tags"))
            {
                patch.ProxyTags = o.Value<JArray>("proxy_tags")
                    .OfType<JObject>().Select(o => new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix")))
                    .ToArray();
            }
            if(o.ContainsKey("privacy")) //TODO: Deprecate this completely in api v2
            {
                var plevel = o.Value<string>("privacy").ParsePrivacy("member");
                                
                patch.Visibility = plevel;
                patch.NamePrivacy = plevel;
                patch.AvatarPrivacy = plevel;
                patch.DescriptionPrivacy = plevel;
                patch.BirthdayPrivacy = plevel;
                patch.PronounPrivacy = plevel;
                // member.ColorPrivacy = plevel;
                patch.MetadataPrivacy = plevel;
            }
            else
            {
                if (o.ContainsKey("visibility")) patch.Visibility = o.Value<string>("visibility").ParsePrivacy("member");
                if (o.ContainsKey("name_privacy")) patch.NamePrivacy = o.Value<string>("name_privacy").ParsePrivacy("member");
                if (o.ContainsKey("description_privacy")) patch.DescriptionPrivacy = o.Value<string>("description_privacy").ParsePrivacy("member");
                if (o.ContainsKey("avatar_privacy")) patch.AvatarPrivacy = o.Value<string>("avatar_privacy").ParsePrivacy("member");
                if (o.ContainsKey("birthday_privacy")) patch.BirthdayPrivacy = o.Value<string>("birthday_privacy").ParsePrivacy("member");
                if (o.ContainsKey("pronoun_privacy")) patch.PronounPrivacy = o.Value<string>("pronoun_privacy").ParsePrivacy("member");
                // if (o.ContainsKey("color_privacy")) member.ColorPrivacy = o.Value<string>("color_privacy").ParsePrivacy("member");
                if (o.ContainsKey("metadata_privacy")) patch.MetadataPrivacy = o.Value<string>("metadata_privacy").ParsePrivacy("member");
            }

            return patch;
        }

        private static string BoundsCheckField(this string input, int maxLength, string nameInError)
        {
            if (input != null && input.Length > maxLength)
                throw new JsonModelParseError($"{nameInError} too long ({input.Length} > {maxLength}).");
            return input;
        }

        private static string ToJsonString(this PrivacyLevel level) => level.LevelName();

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